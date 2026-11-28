using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Media;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Verification;

/// <summary>
/// D424 and D427 (#530): Photo Verified. The member asks for a four-character code, writes it and the date on paper, and
/// sends one photo holding it; it goes through the normal upload checks (malware, known-content matching) into the staff
/// Photo Identified queue. Support or Admin compare it with the public profile photos the member had when they sent it and
/// approve (Photo Verified for 12 months, those photos recorded) or reject with a reason. It never grants or implies ID
/// Verified. Off unless `Verification:PhotoVerified:Enabled` (HostingPolicy needs Privacy Notice 0.22 for that).
/// </summary>
public static class PhotoVerificationEndpoints
{
    public const string Switch = "Verification:PhotoVerified:Enabled";

    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/me/verification/photo").RequireAuthorization();
        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            return denied ?? Results.Ok(await ViewAsync(accounts.Db!, configuration, current!.Profile!, clock.GetUtcNow(), token));
        });

        me.MapPost("/code", async (PhotoVerificationCodeRequest request, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            if (denied is not null) return denied;
            if (!configuration.GetValue(Switch, false)) return Unavailable();
            var (db, profile, now) = (accounts.Db!, current!.Profile!, clock.GetUtcNow());
            if (!PhotoVerificationPolicy.DateAccepted(request.Date, now))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Use today's date.");
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, profile.AccountId, token);
            var attempts = await db.PhotoVerificationAttempts.Where(a => a.AccountId == profile.AccountId).ToListAsync(token);
            if (attempts.Any(a => a.State == PhotoCheckState.Submitted))
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationPending, "Your photo is being checked. We'll update this page when it's done.");
            if (Left(attempts, now) == 0)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationLimit, $"You can send up to {PhotoVerificationPolicy.MaxSubmissions} photos in 30 days. Try again later.");
            if ((await PublicPhotosAsync(db, profile.AccountId, token)).Count == 0)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationNeedsPhoto, "Add a profile photo first. We compare it with the photo you send.");
            // A fresh code each time (D427); any unused one is retired.
            foreach (var open in attempts.Where(a => a.State == PhotoCheckState.Issued)) open.State = PhotoCheckState.Expired;
            await db.SaveChangesAsync(token);
            var attempt = new PhotoVerificationAttempt
            {
                Id = Guid.NewGuid(), AccountId = profile.AccountId, Code = PhotoVerificationPolicy.NewCode(), WrittenDate = request.Date, IssuedAt = now, State = PhotoCheckState.Issued,
            };
            db.PhotoVerificationAttempts.Add(attempt);
            Audit.Record(db, now, profile.AccountId, "account", "verification.photo.code-issued", "photo-verification", attempt.Id.ToString());
            await db.SaveChangesAsync(token);
            return Results.Ok(await ViewAsync(db, configuration, profile, now, token));
        });

        me.MapPost("", async (HttpRequest request, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, token);
            if (denied is not null) return denied;
            if (!configuration.GetValue(Switch, false)) return Unavailable();
            if (current!.Enforcement.NoPhotoUploads) return Problem(StatusCodes.Status403Forbidden, ProblemCodes.Restricted, "You can't send photos right now.");
            var (db, profile, now) = (accounts.Db!, current.Profile!, clock.GetUtcNow());
            if (!request.HasFormContentType) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "Send the photo as a file.");
            if (request.ContentLength > MediaPolicy.MaxUploadBytes + 64 * 1024) return Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB.");
            IFormCollection form;
            try { form = await request.ReadFormAsync(token); }
            catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
            { return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That upload couldn't be read."); }
            if (form.Files.GetFile("file") is not { } file) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "Choose a photo.");
            if (file.Length > MediaPolicy.MaxUploadBytes) return Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB.");
            var code = form["code"].ToString().Trim().ToUpperInvariant();
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, token);
            var pipeline = accounts.Services.GetRequiredService<MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, profile.AccountId, token);
            now = clock.GetUtcNow();
            var attempts = await db.PhotoVerificationAttempts.Where(a => a.AccountId == profile.AccountId).ToListAsync(token);
            // Single use and account bound: only this member's current, unexpired, unused code.
            var attempt = attempts.SingleOrDefault(a => PhotoVerificationPolicy.CodeLive(a, now));
            if (attempt is null || attempt.Code != code)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationCode, "That code isn't current. Ask for a new code and write that one down.");
            if (Left(attempts, now) == 0)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationLimit, $"You can send up to {PhotoVerificationPolicy.MaxSubmissions} photos in 30 days. Try again later.");
            var photos = await PublicPhotosAsync(db, profile.AccountId, token);
            if (photos.Count == 0)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationNeedsPhoto, "Add a profile photo first. We compare it with the photo you send.");
            // PR 608 review: the evidence row and the submission commit together, and screening starts only after that, so an
            // evidence photo never exists without the request that lets staff decide it and retention delete it. A failure
            // before the commit deletes the stored bytes too.
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            var (item, error) = await pipeline.UploadAsync(profile.AccountId, buffer.ToArray(), null, now, token, deferScreening: true, purpose: PhotoVerificationPolicy.Purpose);
            if (error != MediaUploadError.None) return error switch
            {
                MediaUploadError.TooBig => Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB."),
                MediaUploadError.UnsupportedType or MediaUploadError.Animated => Problem(StatusCodes.Status415UnsupportedMediaType, ProblemCodes.PhotoUnsupported, "Use a JPEG, PNG or WebP photo."),
                MediaUploadError.TooSmall => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, $"Photos need to be at least {MediaPolicy.MinSide} pixels on each side."),
                MediaUploadError.Malware => Problem(StatusCodes.Status422UnprocessableEntity, ProblemCodes.PhotoRefused, "That file can't be used. Choose a different photo."),
                MediaUploadError.ScanUnsupported => Problem(StatusCodes.Status422UnprocessableEntity, ProblemCodes.PhotoRefused, "That photo couldn't be checked. Choose a different photo."),
                MediaUploadError.ScanUnavailable => Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PhotoCheckUnavailable, "Photos can't be checked right now. Try again in a few minutes."),
                _ => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That photo couldn't be read."),
            };
            try
            {
                (attempt.State, attempt.MediaId, attempt.SubmittedAt, attempt.ProfilePhotos) = (PhotoCheckState.Submitted, item!.Id, now, PhotoVerificationPolicy.Snapshot(photos));
                Audit.Record(db, now, profile.AccountId, "account", "verification.photo.submitted", "photo-verification", attempt.Id.ToString());
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
            }
            catch
            {
                if (item?.ObjectKey is { } stored && accounts.Services.GetService<IMediaStore>() is { } store)
                    try { await store.DeleteAsync(stored, CancellationToken.None); } catch { /* the bytes have no row: nothing can serve them */ }
                throw;
            }
            pipeline.ScheduleScreening(item.Id);
            return Results.Ok(await ViewAsync(db, configuration, profile, now, token));
        }).DisableAntiforgery();

        var admin = app.MapGroup("/api/admin/photo-verification").RequireAuthorization();
        admin.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            // PR 610 review: the same reveal prerequisite as ordinary photo review (Q114): a current safety acknowledgement
            // before any evidence link is issued.
            if (await PhotoReviewWorkflows.AckUntilAsync(db, current!.Account!.Id, now, token) is null) return PhotoReviewWorkflows.AckRequired();
            var links = accounts.Services.GetRequiredService<MediaLinks>();
            // Waiting for a person: the evidence passed the automatic checks (Uncertain, in its own place). PR 608/610 review:
            // conflict-of-interest exclusions are filtered in the query, before the limit, so they can't hide later requests.
            var excluded = StaffAccessEndpoints.ExcludedSubjects(db, current.Account.Id);
            var rows = await (from a in db.PhotoVerificationAttempts join i in db.MediaItems on a.MediaId equals i.Id
                              join p in db.MemberProfiles on a.AccountId equals p.AccountId
                              where a.State == PhotoCheckState.Submitted && i.Status == MediaStatus.Uncertain && i.RemovedAt == null
                                    && !excluded.Contains(a.AccountId)
                              orderby a.SubmittedAt select new { Attempt = a, p.DisplayName }).Take(50).ToListAsync(token);
            var items = new List<PhotoIdentifiedItem>();
            foreach (var row in rows)
            {
                if (await StaffAccessEndpoints.ExcludedAsync(db, current!.Account!.Id, [row.Attempt.AccountId], token)) continue;
                var then = PhotoVerificationPolicy.Photos(row.Attempt.ProfilePhotos);
                var nowPhotos = await PublicPhotosAsync(db, row.Attempt.AccountId, token);
                var shown = then.Where(nowPhotos.Contains).Select(id => links.For(id, "member", now) is { } link ? new PhotoView(id, link) : null).OfType<PhotoView>().ToList();
                // The evidence link works only for the staff member it was issued to, while this view is recent (MfaEvidenceAccess).
                db.MediaReviewViews.Add(new MediaReviewView { MediaId = row.Attempt.MediaId!.Value, ActorAccountId = current.Account.Id, Purpose = "photo-identified", At = now });
                items.Add(new(row.Attempt.Id, row.Attempt.AccountId, row.DisplayName, row.Attempt.Code, row.Attempt.WrittenDate, row.Attempt.SubmittedAt!.Value,
                    links.For(row.Attempt.MediaId!.Value, "verification", now), shown, PhotoVerificationPolicy.Snapshot(nowPhotos) != row.Attempt.ProfilePhotos));
            }
            Audit.Record(db, now, current!.Account!.Id, "account", "verification.photo.queue-viewed", "photo-verification", "queue", $"items:{items.Count}");
            await db.SaveChangesAsync(token);
            return Results.Ok(items);
        });

        admin.MapPost("/{id:guid}/decision", async (Guid id, PhotoIdentifiedDecision request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token);
            if (denied is not null) return denied;
            var (db, actor) = (accounts.Db!, current!.Account!.Id);
            if (await PhotoReviewWorkflows.AckUntilAsync(db, actor, clock.GetUtcNow(), token) is null) return PhotoReviewWorkflows.AckRequired();
            var approve = request.Decision == "approve";
            if (!approve && (request.Decision != "reject" || request.Reason is null || !PhotoVerificationPolicy.Reasons.ContainsKey(request.Reason)))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidDecision, "Approve, or reject with one of the reasons.");
            var attempt = await db.PhotoVerificationAttempts.SingleOrDefaultAsync(a => a.Id == id && a.State == PhotoCheckState.Submitted, token);
            if (attempt is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That request isn't waiting for a decision.");
            if (attempt.AccountId == actor || await StaffAccessEndpoints.ExcludedAsync(db, actor, [attempt.AccountId], token)) return StaffAccessEndpoints.Conflict();
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, attempt.AccountId, token);
            await db.Entry(attempt).ReloadAsync(token);
            if (attempt.State != PhotoCheckState.Submitted) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That request isn't waiting for a decision.");
            // PR 610 review: only evidence that cleared the automatic checks and waits in its own place can be decided (the
            // same rule as the queue); never one still being screened, blocked or removed.
            var evidence = await db.MediaItems.AsNoTracking().SingleOrDefaultAsync(i => i.Id == attempt.MediaId, token);
            if (evidence is not { Status: MediaStatus.Uncertain, RemovedAt: null, Purpose: PhotoVerificationPolicy.Purpose } || evidence.ObjectKey?.StartsWith("verification/", StringComparison.Ordinal) != true)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.NotAvailable, "That photo isn't ready for a decision.");
            var now = clock.GetUtcNow();
            var matched = PhotoVerificationPolicy.Photos(attempt.ProfilePhotos);
            // D424: never an approval against profile photos that changed since the member sent it.
            if (approve && PhotoVerificationPolicy.Snapshot(await PublicPhotosAsync(db, attempt.AccountId, token)) != attempt.ProfilePhotos)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoVerificationStale, "Their profile photos changed after they sent it. Reject it as \"profile photos changed\".");
            var profile = await db.MemberProfiles.SingleAsync(p => p.AccountId == attempt.AccountId, token);
            (attempt.State, attempt.DecidedAt, attempt.DecidedBy, attempt.Reason) = (approve ? PhotoCheckState.Approved : PhotoCheckState.Rejected, now, actor, approve ? null : request.Reason);
            // PR 608 review: rejected evidence is kept for the same 30 days as approved evidence (the ordinary sweep deletes a
            // rejected photo at once unless it has a retention date); RetainAsync deletes both after that.
            var keepUntil = now + PhotoVerificationPolicy.EvidenceRetention;
            // PR 608 review: the evidence status, the matched photos and the request's outcome commit together, so a failed
            // decision leaves the request waiting (and retryable) rather than half decided.
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            await db.MediaItems.Where(i => i.Id == attempt.MediaId).ExecuteUpdateAsync(u => u.SetProperty(i => i.Status, approve ? MediaStatus.Passed : MediaStatus.Rejected)
                .SetProperty(i => i.ReviewedAt, now).SetProperty(i => i.ReviewedBy, actor).SetProperty(i => i.RejectedRetainUntil, approve ? null : keepUntil), token);
            if (approve)
            {
                (profile.PhotoStatus, profile.PhotoCheckedAt, profile.PhotoVerifiedUntil, profile.PhotoManual) = (VerificationStatus.Verified, now, PhotoVerificationPolicy.Until(now), false);
                await db.PhotoVerificationMatches.Where(m => m.AccountId == attempt.AccountId).ExecuteDeleteAsync(token);
                db.PhotoVerificationMatches.AddRange(matched.Select(media => new PhotoVerificationMatch { AccountId = attempt.AccountId, MediaId = media, VerifiedAt = now }));
            }
            Audit.Record(db, now, actor, "account", approve ? "verification.photoVerified.approved" : "verification.photoVerified.rejected", "account", attempt.AccountId.ToString(),
                approve ? $"photos:{matched.Count}" : request.Reason);
            // D454: the member hears about the decision in their notification centre, in the same commit. Ids only; the
            // outcome and its wording are on the verification page.
            db.Notifications.Add(new Notification { Id = Guid.NewGuid(), AccountId = attempt.AccountId, Kind = NotificationKind.PhotoVerified, SubjectId = attempt.Id,
                DedupeKey = "photo-verified:" + attempt.Id.ToString("N"), CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return Results.Ok(new { attempt.Id, state = approve ? "approved" : "rejected" });
        });
    }

    private static IResult Unavailable() => Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.VerificationNotConfigured, "Photo verification isn't available yet.");

    private static int Left(IEnumerable<PhotoVerificationAttempt> attempts, DateTimeOffset now) =>
        Math.Max(0, PhotoVerificationPolicy.MaxSubmissions - attempts.Count(a => a.SubmittedAt is { } at && at > now - PhotoVerificationPolicy.Window));

    /// <summary>
    /// The member's public profile photos now: approved, never album-only, in a current mode gallery and within the plan's
    /// per-mode limit (as MediaEndpoints.PhotosAsync publishes them). PR 610 review: historic `main` placements are never
    /// published, so they never count.
    /// </summary>
    public static async Task<List<Guid>> PublicPhotosAsync(SwoovrDbContext db, Guid account, CancellationToken token)
    {
        var rows = await (from p in db.MediaPlacements join i in db.MediaItems on p.MediaId equals i.Id
                          where p.AccountId == account && p.Scope != MediaPolicy.Main && i.Status == MediaStatus.Passed && i.RemovedAt == null && !i.AlbumOnly
                          orderby p.Position select new { p.Scope, p.MediaId }).ToListAsync(token);
        if (rows.Count == 0) return [];
        var limit = (await MediaEndpoints.LimitsAsync(db, [account], DateTimeOffset.UtcNow, token)).GetValueOrDefault(account, MediaPolicy.ModeLimit(PlanTier.Free));
        return rows.GroupBy(r => r.Scope).SelectMany(g => g.Take(limit)).Select(r => r.MediaId).Distinct().ToList();
    }

    public static async Task<PhotoVerificationView> ViewAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile profile, DateTimeOffset now, CancellationToken token)
    {
        var attempts = await db.PhotoVerificationAttempts.AsNoTracking().Where(a => a.AccountId == profile.AccountId).OrderByDescending(a => a.IssuedAt).ToListAsync(token);
        var hasPhoto = (await PublicPhotosAsync(db, profile.AccountId, token)).Count > 0;
        var available = configuration.GetValue(Switch, false);
        var left = Left(attempts, now);
        if (profile.PhotoStatus == VerificationStatus.Verified)
            return new(available, "verified", left, VerifiedAt: profile.PhotoCheckedAt, VerifiedUntil: profile.PhotoVerifiedUntil, HasProfilePhoto: hasPhoto);
        var latest = attempts.FirstOrDefault(a => a.State != PhotoCheckState.Expired || a.SubmittedAt is not null);
        return latest switch
        {
            { State: PhotoCheckState.Submitted } => new(available, "submitted", left, HasProfilePhoto: hasPhoto),
            { State: PhotoCheckState.Issued } a when PhotoVerificationPolicy.CodeLive(a, now) =>
                new(available, "issued", left, a.Code, a.WrittenDate, a.IssuedAt + PhotoVerificationPolicy.CodeLifetime, HasProfilePhoto: hasPhoto),
            { State: PhotoCheckState.Rejected } a => new(available, "rejected", left, Reason: PhotoVerificationPolicy.Reasons.GetValueOrDefault(a.Reason ?? "other"), HasProfilePhoto: hasPhoto),
            _ when profile.PhotoStatus == VerificationStatus.Expired => new(available, "expired", left, HasProfilePhoto: hasPhoto),
            _ => new(available, "none", left, HasProfilePhoto: hasPhoto),
        };
    }

    /// <summary>
    /// PR 608/610 review: screening blocked the evidence (a known-content match or a sealed object), so nobody will decide it
    /// in the Photo Identified queue. The request closes with the neutral "other" wording, so the member isn't stuck on
    /// "We're checking your photo"; the block's own escalation and hold are untouched. Saved with the block.
    /// </summary>
    public static async Task ClosedByScreeningAsync(SwoovrDbContext db, MediaItem item, DateTimeOffset now, CancellationToken token)
    {
        if (item.Purpose != PhotoVerificationPolicy.Purpose) return;
        foreach (var attempt in await db.PhotoVerificationAttempts.Where(a => a.MediaId == item.Id && a.State == PhotoCheckState.Submitted).ToListAsync(token))
        {
            (attempt.State, attempt.DecidedAt, attempt.DecidedBy, attempt.Reason) = (PhotoCheckState.Rejected, now, null, "other");
            Audit.Record(db, now, null, "system", "verification.photoVerified.closed-by-screening", "account", attempt.AccountId.ToString());
        }
    }

    /// <summary>
    /// D424: a matched public profile photo changed (removed, replaced, taken out of every mode or taken down), so Photo
    /// Verified ends. A manual (staff override) Photo Verified has no matched photos and is never touched here.
    /// </summary>
    public static async Task InvalidateAsync(SwoovrDbContext db, Guid account, DateTimeOffset now, CancellationToken token)
    {
        var matched = await db.PhotoVerificationMatches.Where(m => m.AccountId == account).Select(m => m.MediaId).ToListAsync(token);
        if (matched.Count == 0) return;
        var live = await PublicPhotosAsync(db, account, token);
        if (matched.All(live.Contains)) return;
        var profile = await db.MemberProfiles.SingleOrDefaultAsync(p => p.AccountId == account, token);
        if (profile is { PhotoStatus: VerificationStatus.Verified, PhotoManual: false })
            (profile.PhotoStatus, profile.PhotoVerifiedUntil) = (VerificationStatus.Expired, null);
        await db.PhotoVerificationMatches.Where(m => m.AccountId == account).ExecuteDeleteAsync(token);
        Audit.Record(db, now, null, "system", "verification.photoVerified.photos-changed", "account", account.ToString());
        await db.SaveChangesAsync(token);
    }

    /// <summary>
    /// D427, run by the retention worker: Photo Verified ends after 12 months; an evidence photo is deleted 30 days after its
    /// decision (the decision record stays); an unused code lapses after a day.
    /// </summary>
    public static async Task<int> RetainAsync(SwoovrDbContext db, MediaPipeline pipeline, DateTimeOffset now, CancellationToken token)
    {
        var dueAwards = await db.MemberProfiles.AsNoTracking().Where(p => p.PhotoStatus == VerificationStatus.Verified && !p.PhotoManual && p.PhotoVerifiedUntil != null && p.PhotoVerifiedUntil <= now)
            .Select(p => p.AccountId).ToListAsync(token);
        var expired = 0;
        foreach (var account in dueAwards)
        {
            // PR 610 review: under the same per-account media lease as approvals, and only if the award is still the one due
            // (a renewal or override since the list was read keeps its status and matched photos). One transaction each.
            await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, account, token);
            await using var transaction = await db.Database.BeginTransactionAsync(token);
            var profile = await db.MemberProfiles.SingleOrDefaultAsync(p => p.AccountId == account, token);
            if (profile is not null) await db.Entry(profile).ReloadAsync(token);
            if (profile is not { PhotoStatus: VerificationStatus.Verified, PhotoManual: false, PhotoVerifiedUntil: { } until } || until > now) continue;
            (profile.PhotoStatus, profile.PhotoVerifiedUntil) = (VerificationStatus.Expired, null);
            await db.PhotoVerificationMatches.Where(m => m.AccountId == account).ExecuteDeleteAsync(token);
            Audit.Record(db, now, null, "system", "verification.photoVerified.expired", "account", account.ToString());
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            expired++;
        }
        await db.PhotoVerificationAttempts.Where(a => a.State == PhotoCheckState.Issued && a.IssuedAt <= now - PhotoVerificationPolicy.CodeLifetime)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.State, PhotoCheckState.Expired), token);
        await db.SaveChangesAsync(token);
        // PR 608 review: batches of 200 that skip past held evidence, so held rows never keep unheld ones from their deletion.
        var deleted = 0;
        var held = new HashSet<Guid>();
        while (true)
        {
            var due = await (from a in db.PhotoVerificationAttempts join i in db.MediaItems on a.MediaId equals i.Id
                             where a.DecidedAt != null && a.DecidedAt <= now - PhotoVerificationPolicy.EvidenceRetention && a.EvidenceDeletedAt == null
                                   // A blocked one is safety evidence: its own preservation rules keep or delete it, never this.
                                   && i.Status != MediaStatus.Blocked && !held.Contains(a.Id)
                             orderby a.DecidedAt, a.Id select new { Attempt = a, Item = i }).Take(200).ToListAsync(token);
            if (due.Count == 0) break;
            foreach (var row in due)
            {
                if (row.Item.ObjectKey is not null && !await pipeline.DeleteVerificationEvidenceAsync(row.Item, now, token)) { held.Add(row.Attempt.Id); continue; } // held: kept, tried again next run
                row.Attempt.EvidenceDeletedAt = now;
                Audit.Record(db, now, null, "system", "verification.photo.evidence-deleted", "photo-verification", row.Attempt.Id.ToString());
                await db.SaveChangesAsync(token);
                deleted++;
            }
        }
        return expired + deleted;
    }
}
