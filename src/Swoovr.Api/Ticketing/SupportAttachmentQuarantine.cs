using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Media;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Ticketing;

/// <summary>
/// D336 (#221). Support files never reach the helpdesk unscanned:
/// 1. <see cref="StoreAsync"/> puts each accepted file (types/content already checked) into private quarantine.
/// 2. The message's text goes to Zammad at once; <see cref="RecordAsync"/> then records the files against it.
/// 3. <see cref="ScanDueAsync"/> scans them with ClamAV; only Clean passes. Outages keep files waiting, never released.
/// 4. <see cref="ReleaseDueAsync"/> sends a message's clean files to Zammad as a follow-up from the same author, once every
///    file has a final result. Infected, unsupported and persistently failing files never leave quarantine.
/// 5. <see cref="RequireCleanAsync"/> is checked before any download through Swoovr (Admin, member, guest): helpdesk files
///    that never passed through quarantine, such as emailed attachments, are scanned on first download.
/// </summary>
public sealed class SupportAttachmentQuarantine(SwoovrDbContext db, IServiceProvider services, IAttachmentMalwareScanner scanner, TimeProvider clock,
    ILogger<SupportAttachmentQuarantine> log)
{
    private IMediaStore Store => services.GetService<IMediaStore>() ?? throw new TicketingAccessException(503, "attachments-unavailable");

    public sealed record StoredFile(Guid Id, string Key, SupportUpload Upload, string Sha256);

    /// <summary>Puts the files into quarantine before the message is sent; nothing is recorded yet.</summary>
    public async Task<IReadOnlyList<StoredFile>> StoreAsync(IReadOnlyList<SupportUpload> files, CancellationToken ct)
    {
        var stored = new List<StoredFile>();
        try
        {
            foreach (var file in files)
            {
                var id = Guid.NewGuid();
                var key = MediaKeys.Attachment(id);
                await Store.PutAsync(key, file.Data, "application/octet-stream", ct);
                stored.Add(new(id, key, file, Convert.ToHexStringLower(SHA256.HashData(file.Data))));
            }
            return stored;
        }
        catch (Exception e) when (e is IOException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            await DiscardAsync(stored);
            log.LogWarning("Support attachments could not be put into quarantine ({ErrorType}).", e.GetType().Name);
            throw new TicketingAccessException(503, "attachments-unavailable");
        }
    }

    /// <summary>Removes quarantined copies of a message that wasn't accepted.</summary>
    public async Task DiscardAsync(IEnumerable<StoredFile> stored)
    {
        foreach (var file in stored)
            try { using var quick = new CancellationTokenSource(TimeSpan.FromSeconds(10)); await Store.DeleteAsync(file.Key, quick.Token); }
            catch (Exception e) when (e is IOException or HttpRequestException or OperationCanceledException) { log.LogWarning("A discarded quarantine copy remains; the sweep removes it."); }
    }

    /// <summary>Records the message's files against its ticket and text article. Idempotent per message (the operation ID).</summary>
    public async Task RecordAsync(Guid group, long ticket, long? article, AttachmentSource source, Guid author, bool privateNote, IReadOnlyList<StoredFile> stored, CancellationToken ct)
    {
        if (stored.Count == 0) return;
        if (await db.Set<SupportAttachmentHold>().AnyAsync(h => h.GroupId == group, ct)) { await DiscardAsync(stored); return; }
        var now = clock.GetUtcNow();
        var position = 0;
        foreach (var file in stored)
            db.Add(new SupportAttachmentHold
            {
                Id = file.Id, GroupId = group, ProviderTicketId = ticket, ProviderArticleId = article, Source = source, AuthorAccountId = author, Private = privateNote,
                Position = position++, Filename = file.Upload.Filename, ContentType = file.Upload.ContentType, Size = file.Upload.Data.LongLength, Sha256 = file.Sha256,
                ObjectKey = file.Key, State = AttachmentScanState.Pending, NextAttemptAt = now, CreatedAt = now,
            });
        Audit.Record(db, now, author, source == AttachmentSource.Staff ? "staff" : "member", "support.attachments.quarantined", "helpdesk-ticket",
            ticket.ToString(System.Globalization.CultureInfo.InvariantCulture), stored.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(ct);
        services.GetService<SupportAttachmentSignal>()?.Wake();
    }

    /// <summary>Scans up to <paramref name="limit"/> due files. Each is claimed with SKIP LOCKED, so replicas never scan the same file.</summary>
    public async Task<int> ScanDueAsync(int limit, CancellationToken ct)
    {
        var scanned = 0;
        for (var i = 0; i < limit; i++)
        {
            var now = clock.GetUtcNow();
            SupportAttachmentHold? hold;
            await using (var claim = await db.Database.BeginTransactionAsync(ct))
            {
                // Due: pending, retryable failures, and scans abandoned by a stopped process.
                hold = await db.Set<SupportAttachmentHold>().FromSqlInterpolated($@"SELECT * FROM support_attachment_hold
                    WHERE object_key IS NOT NULL AND (state = 'Pending' OR state = 'Failed' AND attempts < {AttachmentScanPolicy.MaximumAttempts} OR state = 'Scanning' AND scan_started_at < {now - TimeSpan.FromMinutes(15)})
                    AND next_attempt_at <= {now} ORDER BY next_attempt_at LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
                if (hold is null) { await claim.CommitAsync(ct); break; }
                (hold.State, hold.ScanStartedAt) = (AttachmentScanState.Scanning, now);
                await db.SaveChangesAsync(ct);
                await claim.CommitAsync(ct);
            }
            var data = await ReadAsync(hold.ObjectKey!, ct);
            var result = data is null ? new MalwareScanResult(AttachmentScanState.Failed, scanner.Name, null, null, Failure: "quarantine-unavailable")
                : Convert.ToHexStringLower(SHA256.HashData(data)) != hold.Sha256 ? new MalwareScanResult(AttachmentScanState.Failed, scanner.Name, null, null, Failure: "quarantine-mismatch")
                : await scanner.ScanAsync(new MemoryStream(data, writable: false), ct);
            Apply(hold, result, clock.GetUtcNow());
            if (result.Final) await RememberAsync(hold.Sha256, result, ct);
            await db.SaveChangesAsync(ct);
            if (result.State is AttachmentScanState.Infected or AttachmentScanState.Unsupported)
                Audit.Record(db, clock.GetUtcNow(), null, "system", result.State == AttachmentScanState.Infected ? "support.attachment.infected" : "support.attachment.unsupported",
                    "support-attachment", hold.Id.ToString(), result.Detection ?? result.Failure);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            scanned++;
        }
        return scanned;
    }

    private static void Apply(SupportAttachmentHold hold, MalwareScanResult result, DateTimeOffset now)
    {
        (hold.State, hold.Scanner, hold.EngineVersion, hold.SignatureVersion, hold.Detection, hold.FailureCode, hold.ScanCompletedAt) =
            (result.State, result.Scanner, result.EngineVersion, result.SignatureVersion, result.Detection, result.Failure, now);
        if (result.State == AttachmentScanState.Failed)
        {
            hold.Attempts++;
            hold.NextAttemptAt = now + AttachmentScanPolicy.Backoff(hold.Attempts);
            // Missing or altered quarantine copies can't recover by retrying.
            if (result.Failure is "quarantine-unavailable" or "quarantine-mismatch") hold.Attempts = AttachmentScanPolicy.MaximumAttempts;
        }
    }

    private async Task RememberAsync(string sha256, MalwareScanResult result, CancellationToken ct)
    {
        var verdict = await db.Set<AttachmentScanVerdict>().SingleOrDefaultAsync(v => v.Sha256 == sha256, ct);
        if (verdict is null) db.Add(verdict = new AttachmentScanVerdict { Sha256 = sha256 });
        (verdict.State, verdict.Scanner, verdict.EngineVersion, verdict.SignatureVersion, verdict.Detection, verdict.FailureCode, verdict.ScannedAt) =
            (result.State, result.Scanner, result.EngineVersion, result.SignatureVersion, result.Detection, result.Failure, clock.GetUtcNow());
    }

    private async Task<byte[]?> ReadAsync(string key, CancellationToken ct)
    {
        try { return await Store.GetAsync(key, ct); }
        catch (Exception e) when (e is IOException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested) { return null; }
    }

    /// <summary>Releases settled messages: clean files go to Zammad as a follow-up article from the original author.</summary>
    public async Task<int> ReleaseDueAsync(int limit, CancellationToken ct)
    {
        var released = 0;
        var due = clock.GetUtcNow();
        // Only messages whose every file is final (and not waiting after a failed send), so pending scans never starve them.
        var groups = await db.Set<SupportAttachmentHold>().AsNoTracking().Where(h => h.ReleasedAt == null).GroupBy(h => h.GroupId)
            .Where(g => g.All(h => (h.State == AttachmentScanState.Clean || h.State == AttachmentScanState.Infected || h.State == AttachmentScanState.Unsupported
                || h.State == AttachmentScanState.Failed && h.Attempts >= AttachmentScanPolicy.MaximumAttempts) && h.NextAttemptAt <= due))
            .Select(g => g.Key).Take(limit).ToListAsync(ct);
        foreach (var group in groups)
        {
            await using var lease = await AccountLifecycleLease.AcquireAsync(db, group, ct);
            var files = await db.Set<SupportAttachmentHold>().Where(h => h.GroupId == group).OrderBy(h => h.Position).ToListAsync(ct);
            if (files.Count == 0 || files.Any(f => f.ReleasedAt is not null) || !AttachmentScanPolicy.GroupSettled(files)) continue;
            var clean = files.Where(f => f.State == AttachmentScanState.Clean && f.ObjectKey is not null).ToList();
            var now = clock.GetUtcNow();
            if (clean.Count == 0)
            {
                foreach (var file in files) file.ReleasedAt = now;
                await db.SaveChangesAsync(ct);
                released++;
                continue;
            }
            var first = files[0];
            var operation = first.ReleaseOperationId ?? Guid.NewGuid();
            var retry = first.ReleaseOperationId is not null;
            foreach (var file in files) file.ReleaseOperationId = operation;
            await db.SaveChangesAsync(ct); // fixed before Zammad is asked, so a lost reply is reconciled, never sent twice
            try
            {
                var author = await AuthorAsync(first, ct);
                SupportArticle? article = null;
                if (retry)
                {
                    var actor = await author.ActorAsync(ct);
                    article = (await author.GetArticlesAsync(first.ProviderTicketId, ct)).SingleOrDefault(a => a.OperationReference == operation.ToString("D") && a.AuthorId == actor.Id);
                }
                if (article is null)
                {
                    // Issue 228: a request closed while its files were checked stays closed. The files still go to the
                    // helpdesk (staff keep the record), and the request is closed again straight after.
                    var directory = await author.DirectoryAsync(ct);
                    var ticket = await author.GetTicketAsync(first.ProviderTicketId, ct) ?? throw new TicketingProviderException("provider-not-found");
                    var closedState = directory.States.SingleOrDefault(s => s.Id == ticket.StateId && s.Kind == "closed");
                    var uploads = new List<SupportUpload>();
                    foreach (var file in clean)
                    {
                        var data = await ReadAsync(file.ObjectKey!, ct) ?? throw new TicketingProviderException("quarantine-unavailable");
                        if (Convert.ToHexStringLower(SHA256.HashData(data)) != file.Sha256) throw new TicketingProviderException("quarantine-mismatch");
                        uploads.Add(new(file.Filename, file.ContentType, data));
                    }
                    article = await author.ReplyWithOperationAsync(first.ProviderTicketId, ReleaseBody(clean.Select(f => f.Filename)), first.Private, first.Source == AttachmentSource.Member,
                        uploads, operation.ToString("D"), ct);
                    if (closedState is not null) await author.ChangeTicketAsync(first.ProviderTicketId, new(null, null, closedState.Id, null), ct);
                }
                foreach (var file in files) (file.ReleasedAt, file.ReleaseArticleId) = (now, article.Id);
                await db.SaveChangesAsync(ct);
                foreach (var file in clean)
                    try { await Store.DeleteAsync(file.ObjectKey!, ct); file.ObjectKey = null; }
                    catch (Exception e) when (e is IOException or HttpRequestException) { log.LogWarning("A released quarantine copy remains; the sweep removes it."); }
                Audit.Record(db, now, null, "system", "support.attachments.released", "helpdesk-ticket", first.ProviderTicketId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    $"{clean.Count}/{files.Count}");
                await db.SaveChangesAsync(ct);
                released++;
            }
            catch (Exception e) when (e is TicketingProviderException or TicketingAccessException or HttpRequestException)
            {
                // Waits for the helpdesk; the files stay in quarantine. Every file keeps its clean verdict.
                foreach (var file in files) file.NextAttemptAt = now + TimeSpan.FromMinutes(5);
                await db.SaveChangesAsync(ct);
                log.LogWarning("Support attachments not released yet ({Code}).", e switch { TicketingProviderException p => p.Code, TicketingAccessException a => a.Code, _ => "provider-unreachable" });
            }
        }
        return released;
    }

    /// <summary>The release article's text: Swoovr shows it as the files alone; Zammad staff see which files arrived.</summary>
    public static string ReleaseBody(IEnumerable<string> names) => "Attached files: " + string.Join(", ", names);

    private async Task<ITicketingWorkspaceProvider> AuthorAsync(SupportAttachmentHold first, CancellationToken ct)
    {
        var actors = services.GetRequiredService<TicketingActorFactory>();
        if (first.Source == AttachmentSource.Member)
        {
            var mapping = await db.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(m => m.AccountId == first.AuthorAccountId, ct);
            if (mapping is not { State: TicketingOperationState.Applied, DeactivatedAt: null, RetirementRequestedAt: null, ProviderUserId: long provider })
                throw new TicketingAccessException(404, "ticket-customer-unavailable");
            return await actors.CustomerAsync(provider, first.AuthorAccountId, false, ct);
        }
        var keys = services.GetRequiredService<ContactHistoryKeys>();
        if (await TicketingStaffSync.TokenAsync(db, keys, first.AuthorAccountId, ct) is not var (staffProvider, token)) throw new TicketingAccessException(503, "ticketing-unavailable");
        return actors.StaffFor(staffProvider, token);
    }

    /// <summary>Removes quarantined copies of refused files after 30 days; the hold row keeps the facts.</summary>
    public async Task<int> SweepAsync(CancellationToken ct)
    {
        var cutoff = clock.GetUtcNow() - AttachmentScanPolicy.RefusedCopyRetention;
        var swept = 0;
        foreach (var hold in await db.Set<SupportAttachmentHold>().Where(h => h.ObjectKey != null && (h.ReleasedAt != null && h.ReleasedAt < cutoff || h.ReleasedAt == null && h.CreatedAt < cutoff
                     && (h.State == AttachmentScanState.Infected || h.State == AttachmentScanState.Unsupported))).Take(100).ToListAsync(ct))
        {
            try { if (await Store.DeleteAsync(hold.ObjectKey!, ct)) { hold.ObjectKey = null; swept++; } }
            catch (Exception e) when (e is IOException or HttpRequestException) { break; }
        }
        await db.SaveChangesAsync(ct);
        return swept;
    }

    /// <summary>
    /// Before any download through Swoovr: the content must have a fresh Clean verdict, or be scanned clean now. Infected or
    /// unsupported files are refused (409), and an unavailable scanner refuses too (503); nothing is served on doubt.
    /// </summary>
    public async Task RequireCleanAsync(byte[] data, CancellationToken ct)
    {
        var sha = Convert.ToHexStringLower(SHA256.HashData(data));
        var now = clock.GetUtcNow();
        var verdict = await db.Set<AttachmentScanVerdict>().AsNoTracking().SingleOrDefaultAsync(v => v.Sha256 == sha, ct);
        if (AttachmentScanPolicy.Downloadable(verdict, now)) return;
        if (verdict is { State: AttachmentScanState.Infected or AttachmentScanState.Unsupported }) throw new TicketingAccessException(409, "attachment-blocked");
        var result = await scanner.ScanAsync(new MemoryStream(data, writable: false), ct);
        if (result.Final) { await RememberAsync(sha, result, ct); await db.SaveChangesAsync(ct); }
        if (result.State == AttachmentScanState.Clean) return;
        throw result.State == AttachmentScanState.Failed ? new TicketingAccessException(503, "attachment-scan-unavailable") : new TicketingAccessException(409, "attachment-blocked");
    }

    /// <summary>Held files of a ticket's messages, for the conversation (released ones appear as ordinary files instead).</summary>
    public async Task<(ILookup<long, TicketHeldFile> ByArticle, IReadOnlySet<string> ReleaseOperations)> ViewAsync(long ticket, bool staff, CancellationToken ct)
    {
        var rows = await db.Set<SupportAttachmentHold>().AsNoTracking().Where(h => h.ProviderTicketId == ticket).OrderBy(h => h.Position).ToListAsync(ct);
        var held = rows.Where(h => h.ProviderArticleId is not null && (h.ReleasedAt is null || h.State != AttachmentScanState.Clean))
            .ToLookup(h => h.ProviderArticleId!.Value, h => new TicketHeldFile(h.Filename, h.Size, Status(h), staff ? h.Detection : null));
        return (held, rows.Where(h => h.ReleaseOperationId is not null).Select(h => h.ReleaseOperationId!.Value.ToString("D")).ToHashSet());
    }

    /// <summary>"checking" while scanning or waiting to be sent; "blocked" when it will never be released.</summary>
    public static string Status(SupportAttachmentHold hold) => hold.State switch
    {
        AttachmentScanState.Infected or AttachmentScanState.Unsupported => "blocked",
        AttachmentScanState.Failed when hold.Attempts >= AttachmentScanPolicy.MaximumAttempts => "blocked",
        _ => "checking",
    };
}

/// <summary>Wakes the attachment worker when files arrive, instead of waiting for its next poll.</summary>
public sealed class SupportAttachmentSignal
{
    private readonly SemaphoreSlim signal = new(0, 1);
    public void Wake() { if (signal.CurrentCount == 0) try { signal.Release(); } catch (SemaphoreFullException) { } }
    public Task WaitAsync(TimeSpan timeout, CancellationToken ct) => signal.WaitAsync(timeout, ct);
}

/// <summary>D336: scans quarantined support files and releases settled messages. Several replicas may run it.</summary>
public sealed class SupportAttachmentWorker(IServiceScopeFactory scopes, SupportAttachmentSignal signal, ILogger<SupportAttachmentWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        var sweepAt = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var quarantine = scope.ServiceProvider.GetRequiredService<SupportAttachmentQuarantine>();
                await quarantine.ScanDueAsync(10, stoppingToken);
                await quarantine.ReleaseDueAsync(10, stoppingToken);
                if (DateTimeOffset.UtcNow > sweepAt) { await quarantine.SweepAsync(stoppingToken); sweepAt = DateTimeOffset.UtcNow.AddHours(1); }
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { log.LogError("Support attachment scanning failed ({ErrorType}); files stay in quarantine and retry.", error.GetType().Name); }
            try { await signal.WaitAsync(TimeSpan.FromSeconds(15), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
