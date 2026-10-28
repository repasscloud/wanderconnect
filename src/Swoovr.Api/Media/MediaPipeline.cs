using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Media;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Media;

/// <summary>
/// Media settings (D203). The store is R2 when its keys are configured (D201), else a private local folder in Development
/// only, else none (uploads answer 503). `Media:Screening:Simulate` and `Media:Screening:RequireKnownContentCheck=false`
/// are refused outside Development, so neither can reach a hosted environment.
/// </summary>
public sealed record MediaSettings(bool RequireKnownContentCheck, string? Simulated, bool StoreConfigured);

/// <summary>Signed links for photos: `Media:LinkKey` (base64, 32 bytes) or a key derived from the founder secret with its own label.</summary>
public sealed class MediaLinks(IConfiguration configuration, Founders founders)
{
    public byte[]? Key { get; } = configuration["Media:LinkKey"] is { Length: > 0 } configured && Convert.FromBase64String(configured) is { Length: 32 } key ? key
        : founders.CurrentVersion is { } v ? HMACSHA256.HashData(founders.Secrets[v], "swoovr:media-links:v1"u8) : null;

    public string? For(Guid media, string variant, DateTimeOffset now) =>
        Key is null ? null : "api/media/" + MediaPolicy.Link(Key, media, variant, now + MediaPolicy.LinkLifetime);
}

/// <summary>New uploads waiting for their first screening, so the worker starts at once instead of at its next sweep.</summary>
public sealed class MediaQueue
{
    private readonly Channel<Guid> channel = Channel.CreateUnbounded<Guid>();
    public void Enqueue(Guid id) => channel.Writer.TryWrite(id);
    public ChannelReader<Guid> Reader => channel.Reader;
}

/// <summary>
/// The quarantine pipeline (policy §5, §6, §10; procedure §4; D201, D203). Every upload is validated, stripped and
/// re-encoded, stored privately under quarantine/, screened, and only shown once it passes. Uncertain photos wait for an
/// admin; known abuse material is sealed into the evidence store, opens a Serious Safety Incident with a provisional legal
/// hold and is escalated with ids only, and no member of staff can ever view or download it. Audit entries carry ids only.
/// </summary>
public sealed partial class MediaPipeline(IServiceProvider services, IPhotoProcessor processor, IMediaScreening screening, MediaSettings settings,
    MediaQueue queue, ILogger<MediaPipeline> log)
{
    private SwoovrDbContext db => services.GetRequiredService<SwoovrDbContext>();
    private IMediaStore? Store => services.GetService<IMediaStore>();
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool Available => Store is not null;

    /// <param name="conversation">For a chat photo (#41): the conversation it's sent in; the item's purpose is then "attachment".</param>
    public async Task<(MediaItem? Item, MediaUploadError Error)> UploadAsync(Guid account, byte[] upload, Guid? replaces, DateTimeOffset now, CancellationToken cancellationToken,
        Guid? conversation = null, bool deferScreening = false)
    {
        var processed = processor.Process(upload);
        if (processed.Error != MediaUploadError.None) return (null, processed.Error);
        var item = new MediaItem
        {
            Id = Guid.NewGuid(), AccountId = account, Status = MediaStatus.Pending, CreatedAt = now, Sha256 = MediaPolicy.Hash(processed.Data),
            UploadSha256 = MediaPolicy.Hash(upload), ContentType = "image/jpeg", Width = processed.Width, Height = processed.Height, Bytes = processed.Data.Length,
            ReplacesId = replaces, Purpose = conversation is null ? null : AttachmentPolicy.Purpose, ConversationId = conversation,
            // The uploading host screens it straight away; the sweep only takes it if that didn't happen.
            NextScreenAt = now + TimeSpan.FromMinutes(2),
        };
        item.ObjectKey = MediaKeys.Quarantine(item.Id);
        await Store!.PutAsync(item.ObjectKey, processed.Data, item.ContentType, cancellationToken);
        db.MediaItems.Add(item);
        Audit.Record(db, now, account, "account", "media.uploaded", "media", item.Id.ToString(), replaces is not null ? "replacement" : conversation is not null ? "attachment" : null);
        await db.SaveChangesAsync(cancellationToken);
        if (!deferScreening) ScheduleScreening(item.Id);
        return (item, MediaUploadError.None);
    }

    /// <summary>Queue only after an enclosing upload transaction commits, so the worker can see its row.</summary>
    public void ScheduleScreening(Guid id) => queue.Enqueue(id);

    /// <summary>One screening attempt for a pending item. Anything unusable leaves it pending for a later attempt.</summary>
    public async Task ScreenAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var origin = await db.MediaItems.AsNoTracking().Where(i => i.Id == id).Select(i => (Guid?)i.AccountId).SingleOrDefaultAsync(cancellationToken);
        if (origin is null) return;
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, origin.Value, cancellationToken);
        var item = await db.MediaItems.FindAsync([id], cancellationToken);
        if (item is not null) await db.Entry(item).ReloadAsync(cancellationToken);
        if (item is { Status: MediaStatus.Blocked }) { await RecoverBlockedAsync(id, now, cancellationToken); return; }
        if (item is { RemovedAt: not null }) { await RemoveCoreAsync(item, now, cancellationToken); return; }
        if (item is not { Status: MediaStatus.Pending, RemovedAt: null } || item.ObjectKey is null) return;
        if (item.ObjectKey.StartsWith("evidence/", StringComparison.Ordinal)) { await BlockCoreAsync(item, null, "sealed-object-state", now, cancellationToken); return; }
        if (!await db.Accounts.AnyAsync(a => a.Id == item.AccountId && a.Status == AccountStatus.Active, cancellationToken)) return;
        byte[]? data;
        try { data = await Store!.GetAsync(item.ObjectKey, cancellationToken); }
        catch (Exception exception) when (exception is IOException or HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("Media {Id}: the store couldn't be read.", item.Id);
            data = null;
        }
        if (data is null || !MediaPolicy.Hash(data).SequenceEqual(item.Sha256))
        {
            await db.Entry(item).ReloadAsync(cancellationToken);
            if (item.RemovedAt is not null) { await RemoveCoreAsync(item, now, cancellationToken); return; }
            item.ScreenAttempts++;
            await PendingAsync(item, data is null ? "object-unavailable" : "object-changed", now, cancellationToken);
            return;
        }
        var response = await screening.ScreenAsync(data, cancellationToken);
        var decision = MediaScreeningPolicy.Decide(response, settings.RequireKnownContentCheck);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (item.Status != MediaStatus.Pending) return;
        // A known-content verdict still preserves its source after a concurrent removal intent.
        if (item.RemovedAt is not null && decision.Verdict != ScreeningVerdict.Blocked) { await RemoveCoreAsync(item, now, cancellationToken); return; }
        item.ScreenAttempts++;
        (item.Screener, item.PolicyVersion) = (screening.Name, MediaScreeningPolicy.Version);
        item.Signals = JsonSerializer.Serialize(new
        {
            decision.Reason,
            Detections = decision.Evidence.Select(d => new { d.Label, Score = Math.Round(d.Score, 3) }),
            Nudity = response.Nudity.ToString(), Nsfw = response.IsNsfw, KnownContent = response.KnownContent.ToString(),
            Classification = response.KnownContent == EngineState.Completed ? response.KnownClassification : null,
        }, Json);
        switch (decision.Verdict)
        {
            case ScreeningVerdict.Pending:
                await PendingAsync(item, decision.Reason, now, cancellationToken);
                return;
            case ScreeningVerdict.Passed:
                item.KnownContentChecked = decision.KnownContentChecked;
                await PassCoreAsync(item, null, now, cancellationToken);
                return;
            case ScreeningVerdict.Uncertain:
                (item.Status, item.ScreenedAt, item.KnownContentChecked, item.NextScreenAt, item.LastScreenError) = (MediaStatus.Uncertain, now, decision.KnownContentChecked, null, null);
                Audit.Record(db, now, null, "system", "media.uncertain", "media", item.Id.ToString(), decision.Reason);
                await db.SaveChangesAsync(cancellationToken);
                await AttachmentChangedAsync(item, now, cancellationToken);
                return;
            case ScreeningVerdict.Blocked:
                item.KnownContentChecked = true;
                await BlockCoreAsync(item, null, "known-content-match", now, cancellationToken);
                return;
        }
    }

    private async Task PendingAsync(MediaItem item, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        (item.LastScreenError, item.NextScreenAt) = (reason, MediaPolicy.NextAttempt(item.ScreenAttempts, now));
        if (item.ScreenAttempts == 1) Audit.Record(db, now, null, "system", "media.pending", "media", item.Id.ToString(), reason);
        await db.SaveChangesAsync(cancellationToken);
        log.LogInformation("Media {Id} stays pending: {Reason}.", item.Id, reason);
    }

    /// <summary>Publishes a photo: the object moves out of quarantine; a replacement takes over its original's places.</summary>
    public async Task<bool> PassAsync(MediaItem item, Guid? reviewer, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await PublicationBarrierAsync(item.Id, "before-lease", cancellationToken);
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, item.AccountId, cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (db.Entry(item).State == EntityState.Detached || item.RemovedAt is not null || item.ObjectKey is null || item.ObjectKey.StartsWith("evidence/", StringComparison.Ordinal)
            || item.Status != (reviewer is null ? MediaStatus.Pending : MediaStatus.Uncertain)) return false;
        return await PassCoreAsync(item, reviewer, now, cancellationToken);
    }

    private async Task<bool> PassCoreAsync(MediaItem item, Guid? reviewer, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var data = await Store!.GetAsync(item.ObjectKey!, cancellationToken) ?? throw new IOException("Media object missing.");
        await PublicationBarrierAsync(item.Id, "after-read", cancellationToken);
        var published = MediaKeys.Photo(item.Id);
        // A removal can be staged without waiting for the lifecycle lease.
        if (await db.MediaItems.AnyAsync(i => i.Id == item.Id && i.RemovedAt != null, cancellationToken)
            || !await db.Accounts.AnyAsync(a => a.Id == item.AccountId && a.Status == AccountStatus.Active, cancellationToken))
        {
            await db.Entry(item).ReloadAsync(cancellationToken);
            if (item.RemovedAt is not null) await RemoveCoreAsync(item, now, cancellationToken);
            return false;
        }
        await Store.PutAsync(published, data, item.ContentType, cancellationToken);
        var quarantined = item.ObjectKey!;
        (item.Status, item.ObjectKey, item.ScreenedAt, item.NextScreenAt, item.LastScreenError) = (MediaStatus.Passed, published, item.ScreenedAt ?? now, null, null);
        if (reviewer is not null) (item.ReviewedBy, item.ReviewedAt) = (reviewer, now);
        if (item.Purpose == AttachmentPolicy.Purpose)
        {
            // A chat photo never goes on a profile; the recipient sees it from now on (#41).
            Audit.Record(db, now, reviewer, reviewer is null ? "system" : "account", "media.passed", "media", item.Id.ToString(), reviewer is null ? "attachment" : "review");
            await db.SaveChangesAsync(cancellationToken);
            await DeleteSupersededCopyAsync(quarantined, cancellationToken);
            await AttachmentChangedAsync(item, now, cancellationToken);
            return true;
        }
        var places = item.ReplacesId is { } old
            ? await db.MediaPlacements.Where(p => p.AccountId == item.AccountId && p.MediaId == old).ToListAsync(cancellationToken) : [];
        foreach (var place in places) place.MediaId = item.Id;
        // The member's first approved photo becomes their main photo.
        if (places.Count == 0 && !await db.MediaPlacements.AnyAsync(p => p.AccountId == item.AccountId && p.Scope == MediaPolicy.Main, cancellationToken))
            db.MediaPlacements.Add(new MediaPlacement { AccountId = item.AccountId, Scope = MediaPolicy.Main, Position = 0, MediaId = item.Id });
        Audit.Record(db, now, reviewer, reviewer is null ? "system" : "account", "media.passed", "media", item.Id.ToString(), reviewer is null ? null : "review");
        // The original leaves in the same save that publishes its replacement, so nobody ever sees both or neither.
        var original = item.ReplacesId is { } replaced && await db.MediaItems.FindAsync([replaced], cancellationToken) is { Status: MediaStatus.Passed } found ? found : null;
        if (original is not null)
        {
            (original.Status, original.RemovedAt) = (MediaStatus.Removed, now);
            Audit.Record(db, now, item.AccountId, "account", "media.removed", "media", original.Id.ToString(), "replaced");
        }
        await db.SaveChangesAsync(cancellationToken);
        await DeleteSupersededCopyAsync(quarantined, cancellationToken);
        if (original is not null) await DeleteObjectIfAllowedAsync(original, now, cancellationToken);
        await Profiles.ProfilePrivacy.CaptureAsync(db, item.AccountId, now, cancellationToken);
        return true;
    }

    /// <summary>
    /// Known abuse material (procedure §4): sealed into the evidence store with the evidence key (no copy left in
    /// quarantine), never served to anyone, a Serious Safety Incident in the admin queue, a provisional legal hold and the
    /// escalation email with ids only. The member is told only "not approved", as for any refusal.
    /// </summary>
    public async Task<bool> BlockAsync(MediaItem item, Guid? actor, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, item.AccountId, cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (db.Entry(item).State == EntityState.Detached || item.Status != MediaStatus.Blocked
            && (item.RemovedAt is not null || item.ObjectKey is null || item.Status is not (MediaStatus.Uncertain or MediaStatus.Passed))) return false;
        await BlockCoreAsync(item, actor, reason, now, cancellationToken);
        return true;
    }

    private async Task BlockCoreAsync(MediaItem item, Guid? actor, string reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (item.Status != MediaStatus.Blocked)
        {
            (item.Status, item.ScreenedAt, item.NextScreenAt, item.LastScreenError) = (MediaStatus.Blocked, item.ScreenedAt ?? now, null, null);
            if (actor is not null) (item.ReviewedBy, item.ReviewedAt) = (actor, now);
            item.PreservationPendingAt = now; item.NextPreservationAt = now; item.PreservationLegacy = false;
            item.PreservationSourceKey = item.ObjectKey;
            await db.MediaPlacements.Where(p => p.MediaId == item.Id).ExecuteDeleteAsync(cancellationToken);
            Audit.Record(db, now, actor, actor is null ? "system" : "account", "media.blocked", "media", item.Id.ToString(), reason);
            await db.SaveChangesAsync(cancellationToken); // Durable refusal/work precedes every object/hold/email effect.
        }
        await RecoverBlockedAsync(item.Id, now, cancellationToken);
    }

    /// <summary>A reviewer's refusal: off every profile, the object deleted unless a hold needs it.</summary>
    public async Task<bool> RejectAsync(MediaItem item, Guid reviewer, string? reason, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, item.AccountId, cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (db.Entry(item).State == EntityState.Detached || item.RemovedAt is not null || item.Status is not (MediaStatus.Uncertain or MediaStatus.Passed)) return false;
        (item.Status, item.ReviewedBy, item.ReviewedAt, item.ReviewReason, item.NextScreenAt) = (MediaStatus.Rejected, reviewer, now, reason, null);
        await db.MediaPlacements.Where(p => p.MediaId == item.Id).ExecuteDeleteAsync(cancellationToken);
        Audit.Record(db, now, reviewer, "account", "media.rejected", "media", item.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        await DeleteObjectIfAllowedAsync(item, now, cancellationToken);
        await AttachmentChangedAsync(item, now, cancellationToken);
        await Profiles.ProfilePrivacy.CaptureAsync(db, item.AccountId, now, cancellationToken);
        return true;
    }

    /// <summary>
    /// The member removes a photo. Blocked and rejected items keep their status (a blocked one stays evidence); the rest
    /// become Removed. The object goes unless a hold covers the account or hold status is unknown (then the sweep retries).
    /// </summary>
    public async Task RemoveAsync(MediaItem item, Guid actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Hide immediately, even while a classifier owns the source. Physical deletion waits for that lease.
        await db.MediaItems.Where(i => i.Id == item.Id && i.RemovedAt == null).ExecuteUpdateAsync(u => u.SetProperty(i => i.RemovedAt, now), cancellationToken);
        Audit.Record(db, now, actor, "account", "media.removed", "media", item.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        queue.Enqueue(item.Id);
        await using var lease = await AccountLifecycleLease.TryAcquireMediaAsync(db, item.AccountId, cancellationToken);
        if (lease is null) return;
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (db.Entry(item).State != EntityState.Detached) await RemoveCoreAsync(item, now, cancellationToken);
    }

    private async Task RemoveCoreAsync(MediaItem item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (item.Status is MediaStatus.Pending or MediaStatus.Passed or MediaStatus.Uncertain) (item.Status, item.NextScreenAt) = (MediaStatus.Removed, null);
        await db.MediaPlacements.Where(p => p.MediaId == item.Id).ExecuteDeleteAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (item.ObjectKey is not null) await DeleteObjectIfAllowedAsync(item, now, cancellationToken);
        await Profiles.ProfilePrivacy.CaptureAsync(db, item.AccountId, now, cancellationToken);
    }

    /// <summary>A chat photo's state changed: the sender's chat shows it, and once it passed the recipient sees it (#41).</summary>
    private async Task AttachmentChangedAsync(MediaItem item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (item.Purpose != AttachmentPolicy.Purpose) return;
        try { await Connections.MessagingEndpoints.AttachmentChangedAsync(services, item, now, cancellationToken); }
        catch (Exception error) when (error is DbUpdateException or InvalidOperationException)
        {
            log.LogWarning("Media {Id}: the chat wasn't told about the photo's state.", item.Id);
        }
    }

    private async Task DeleteSupersededCopyAsync(string key, CancellationToken cancellationToken)
    {
        await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        if (!await Store!.DeleteAsync(key, cancellationToken)) throw new IOException("Superseded media copy deletion failed.");
    }

    private async Task<bool> DeleteObjectIfAllowedAsync(MediaItem item, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
        await db.Entry(item).ReloadAsync(cancellationToken);
        if (db.Entry(item).State == EntityState.Detached || item.ObjectKey is null) return false;
        var evidence = services.GetRequiredService<Evidence.EvidencePreservation>();
        if (await services.GetRequiredService<Retention.SafetyRetention>().MediaRequiredAsync(item, cancellationToken)) return false;
        var held = await evidence.TryHeldAccountsAsync(cancellationToken);
        var ownReleased = item.HoldId is { } hold && await db.LegalHolds.AnyAsync(h => h.Id == hold && h.Status == LegalHoldStatus.Released, cancellationToken);
        if (!MediaPolicy.MayDeleteObject(item, held, ownReleased) || !await Store!.DeleteAsync(item.ObjectKey!, cancellationToken)) return false;
        (item.ObjectKey, item.ObjectDeletedAt) = (null, now);
        Audit.Record(db, now, null, "system", "media.object-deleted", "media", item.Id.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// The purge sweep (`accounts purge`): objects of removed, rejected and released-blocked items go unless held; removed
    /// rows go once their object has. Returns (deleted, kept), or null when hold status is unknown (nothing deleted).
    /// </summary>
    public async Task<(int Deleted, int Kept)?> SweepAsync(DateTimeOffset now, CancellationToken cancellationToken, bool deferredOnly = false, Guid? onlyAccount = null)
    {
        if (Store is null) return (0, 0);
        var evidence = services.GetRequiredService<Evidence.EvidencePreservation>();
        if (await evidence.TryHeldAccountsAsync(cancellationToken) is null) return null;
        var (deleted, kept) = (0, 0);
        foreach (var item in await db.MediaItems.Where(i => i.ObjectKey != null && (onlyAccount == null || i.AccountId == onlyAccount)
                     && (deferredOnly ? i.RemovedAt != null && (i.Status == MediaStatus.Pending || i.Status == MediaStatus.Passed || i.Status == MediaStatus.Uncertain)
                         : i.RemovedAt != null || i.Status == MediaStatus.Removed || i.Status == MediaStatus.Rejected || i.Status == MediaStatus.Blocked)).ToListAsync(cancellationToken))
        {
            await using var lease = await AccountLifecycleLease.TryAcquireMediaAsync(db, item.AccountId, cancellationToken);
            if (lease is null) { kept++; continue; }
            await db.Entry(item).ReloadAsync(cancellationToken);
            if (db.Entry(item).State == EntityState.Detached || item.ObjectKey is null) continue;
            if (item.RemovedAt is not null && item.Status is MediaStatus.Pending or MediaStatus.Passed or MediaStatus.Uncertain)
                await RemoveCoreAsync(item, now, cancellationToken);
            if (item.ObjectKey is null || await DeleteObjectIfAllowedAsync(item, now, cancellationToken)) deleted++; else kept++;
        }
        if (!deferredOnly)
            foreach (var empty in await db.MediaItems.Where(i => i.Status == MediaStatus.Removed && i.ObjectKey == null && (onlyAccount == null || i.AccountId == onlyAccount)).ToListAsync(cancellationToken))
            {
                await using var lease = await AccountLifecycleLease.TryAcquireMediaAsync(db, empty.AccountId, cancellationToken);
                if (lease is null) continue;
                await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
                await db.Entry(empty).ReloadAsync(cancellationToken);
                if (db.Entry(empty).State == EntityState.Detached || empty.Status != MediaStatus.Removed || empty.ObjectKey != null) continue;
                var held = await evidence.TryHeldAccountsAsync(cancellationToken);
                if (held is null) return null;
                if (held.Contains(empty.AccountId) || await services.GetRequiredService<Retention.SafetyRetention>().MediaRequiredAsync(empty, cancellationToken)) continue;
                await db.MediaItems.Where(i => i.Id == empty.Id).ExecuteDeleteAsync(cancellationToken);
            }
        return (deleted, kept);
    }

    /// <summary>Final deletion (D170): every item of the account leaves its profile; objects follow in the sweep unless held.</summary>
    public static async Task RemoveAllAsync(SwoovrDbContext db, Guid account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.MediaPlacements.Where(p => p.AccountId == account).ExecuteDeleteAsync(cancellationToken);
        await db.MediaItems.Where(i => i.AccountId == account && (i.Status == MediaStatus.Pending || i.Status == MediaStatus.Passed || i.Status == MediaStatus.Uncertain))
            .ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, MediaStatus.Removed).SetProperty(i => i.RemovedAt, now).SetProperty(i => i.NextScreenAt, (DateTimeOffset?)null), cancellationToken);
        await db.MediaItems.Where(i => i.AccountId == account && i.RemovedAt == null).ExecuteUpdateAsync(u => u.SetProperty(i => i.RemovedAt, now), cancellationToken);
    }

    /// <summary>The bytes of a photo the member may export (never a refused one: those read the same as each other).</summary>
    public async Task<byte[]?> ExportBytesAsync(MediaItem item, CancellationToken cancellationToken) =>
        Store is null || item.ObjectKey is null || item.Status is MediaStatus.Blocked or MediaStatus.Rejected || item.ObjectKey.StartsWith("evidence/")
            ? null : await Store.GetAsync(item.ObjectKey, cancellationToken);

    /// <summary>Bytes for a signed link: a passed photo (member link) or an uncertain one (review link); never anything else.</summary>
    public async Task<byte[]?> ServeAsync(MediaItem item, string variant, CancellationToken cancellationToken)
    {
        // A case link (#41: a reported chat photo, for an admin with a recorded purpose) serves a passed or uncertain one,
        // even after delete for both while its object is kept; never a blocked one.
        var allowed = variant switch
        {
            "member" => item is { Status: MediaStatus.Passed, RemovedAt: null },
            "case" => MediaPolicy.CaseReviewable(item),
            _ => item is { Status: MediaStatus.Uncertain, RemovedAt: null },
        };
        if (!allowed || Store is null || item.ObjectKey is null || item.ObjectKey.StartsWith("evidence/")) return null;
        return await Store.GetAsync(item.ObjectKey, cancellationToken);
    }
}

/// <summary>Screens new uploads at once and sweeps due pending ones every 30 seconds, each in its own scope.</summary>
public sealed class MediaScreeningWorker(IServiceScopeFactory scopes, MediaQueue queue, TimeProvider clock, ILogger<MediaScreeningWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sweep = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                wait.CancelAfter(TimeSpan.FromSeconds(30));
                try { if (await queue.Reader.WaitToReadAsync(wait.Token)) while (queue.Reader.TryRead(out var id)) await OneAsync(id, stoppingToken); }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
                if (clock.GetUtcNow() - sweep < TimeSpan.FromSeconds(30)) continue;
                sweep = clock.GetUtcNow();
                List<Guid> due;
                await using (var scope = scopes.CreateAsyncScope())
                    due = await scope.ServiceProvider.GetRequiredService<SwoovrDbContext>().MediaItems
                        .Where(i => i.Status == MediaStatus.Pending && i.RemovedAt == null && i.NextScreenAt <= sweep).OrderBy(i => i.NextScreenAt).Select(i => i.Id).Take(20)
                        .ToListAsync(stoppingToken);
                foreach (var id in due) await OneAsync(id, stoppingToken);
                await using (var recovery = scopes.CreateAsyncScope())
                {
                    var pipeline = recovery.ServiceProvider.GetRequiredService<MediaPipeline>();
                    await pipeline.RecoverBlockedSweepAsync(clock.GetUtcNow(), stoppingToken);
                    await pipeline.SweepAsync(clock.GetUtcNow(), stoppingToken, deferredOnly: true);
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogError(exception, "Media screening sweep failed.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task OneAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        try { await scope.ServiceProvider.GetRequiredService<MediaPipeline>().ScreenAsync(id, clock.GetUtcNow(), cancellationToken); }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            log.LogError(exception, "Media {Id}: screening attempt failed; it stays pending.", id);
        }
    }
}

public static class MediaSetup
{
    /// <summary>Registers the store, the screener, the pipeline and (with a database) the worker. Throws on settings refused outside Development.</summary>
    public static IServiceCollection AddSwoovrMedia(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment, bool persistence)
    {
        var development = environment.IsDevelopment();
        if (!string.IsNullOrWhiteSpace(configuration["Media:Publication:BarrierDirectory"]))
        {
            if (!development) throw new InvalidOperationException("Media publication barriers are Development only.");
            if (configuration["Media:Publication:BarrierStage"] is { } stage && stage is not ("before-lease" or "after-read"))
                throw new InvalidOperationException("Unknown media publication barrier stage.");
        }
        if (configuration["Media:Preservation:Fault"] is { Length: > 0 } pause)
        {
            if (!development) throw new InvalidOperationException("Media preservation interruption hooks are Development only.");
            if (pause is not ("blocked-save" or "hold-create" or "object-sealed" or "capture-complete" or "capture-error")) throw new InvalidOperationException("Unknown media preservation interruption stage.");
        }
        var simulate = configuration["Media:Screening:Simulate"];
        var simulated = MediaScreeningPolicy.Simulated(simulate);
        if (simulated is not null && !development) throw new InvalidOperationException("Media:Screening:Simulate is for Development only.");
        var requireKnown = configuration.GetValue("Media:Screening:RequireKnownContentCheck", true);
        if (!requireKnown && !development) throw new InvalidOperationException("Media:Screening:RequireKnownContentCheck may be false in Development only.");
        var r2 = new R2Options(configuration["Media:R2:Endpoint"], configuration["Media:R2:Bucket"], configuration["Media:R2:AccessKeyId"], configuration["Media:R2:SecretAccessKey"]);
        var choice = configuration["Media:Store"] ?? (r2.Configured ? "r2" : development ? "local" : "none");
        var configured = false;
        if (choice == "r2" && r2.Configured)
        {
            services.AddSingleton(r2);
            services.AddHttpClient<IMediaStore, R2MediaStore>(http => http.Timeout = TimeSpan.FromSeconds(30));
            configured = true;
        }
        else if (choice == "local" && development)
        {
            var root = configuration["Media:Local:Path"] is { Length: > 0 } path ? path : Path.Combine(environment.ContentRootPath, ".local", "media");
            services.AddSingleton<IMediaStore>(new LocalMediaStore(root));
            configured = true;
        }
        services.AddSingleton(new MediaSettings(requireKnown, simulated is null ? null : simulate, configured));
        services.AddSingleton<IPhotoProcessor, PhotoProcessor>();
        if (simulated is { } verdict) services.AddSingleton<IMediaScreening>(new SimulatedScreening(verdict));
        else
        {
            var baseUrl = configuration["Media:Screening:BaseUrl"];
            services.AddSingleton(new ScreeningOptions(string.IsNullOrWhiteSpace(baseUrl) ? null : new Uri(baseUrl.TrimEnd('/') + "/"), configuration["Media:Screening:ApiKey"]));
            services.AddHttpClient<IMediaScreening, ImageModerationScreening>(http => http.Timeout = TimeSpan.FromSeconds(150));
        }
        services.AddSingleton<MediaQueue>();
        services.AddScoped<MediaLinks>();
        services.AddScoped<MediaPipeline>();
        if (persistence && configuration.GetValue("Media:Screening:Worker", true)) services.AddHostedService<MediaScreeningWorker>();
        return services;
    }
}
