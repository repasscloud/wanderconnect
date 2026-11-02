using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Media;

/// <summary>
/// Photos (D201, D203; policy §5, §6, §10). Members upload into quarantine and choose a main photo and per-mode photos;
/// others see approved photos only, through short-lived signed links made after the profile rules allowed the view.
/// Admins review uncertain photos (blurred by default, every view with a recorded purpose and in the sensitive-access
/// log). A blocked photo has no route at all for anyone.
/// </summary>
public static class MediaEndpoints
{
    public static void Map(WebApplication app)
    {
        PhotoReviewWorkflows.Map(app);
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapPost("/me/photos", async (HttpRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var pipeline = accounts.Services.GetRequiredService<MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            if (!request.HasFormContentType) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "Send the photo as a file.");
            if (request.ContentLength > MediaPolicy.MaxUploadBytes + 64 * 1024)
                return Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB.");
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            if (await db.MediaItems.CountAsync(i => i.AccountId == me && i.Purpose == null && i.CreatedAt > now - TimeSpan.FromHours(1), cancellationToken) >= MediaPolicy.UploadsPerHour)
                return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.UploadLimit, "You've uploaded a lot of photos in the last hour. Try again later.");
            if (await db.MediaItems.CountAsync(i => i.AccountId == me && i.Purpose == null && i.RemovedAt == null && i.Status != MediaStatus.Removed, cancellationToken) >= MediaPolicy.MaxKept)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoLimit, $"You can keep up to {MediaPolicy.MaxKept} photos. Delete one first.");
            IFormCollection form;
            try { form = await request.ReadFormAsync(cancellationToken); }
            catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
            { return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That upload couldn't be read."); }
            if (form.Files.GetFile("file") is not { } file) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "Choose a photo.");
            if (file.Length > MediaPolicy.MaxUploadBytes) return Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB.");
            Guid? replaces = null;
            if (form.TryGetValue("replaces", out var value) && value.ToString() is { Length: > 0 } text)
            {
                if (!Guid.TryParse(text, out var id) || !await db.MediaItems.AnyAsync(i => i.Id == id && i.AccountId == me && i.Purpose == null && i.Status == MediaStatus.Passed && i.RemovedAt == null, cancellationToken))
                    return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't yours to replace.");
                replaces = id;
            }
            using var buffer = new MemoryStream();
            await file.CopyToAsync(buffer, cancellationToken);
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, cancellationToken);
            if (!await db.Accounts.AsNoTracking().AnyAsync(a => a.Id == me && a.Status == AccountStatus.Active, cancellationToken)) return Results.Forbid();
            await using var libraryLease = await AccountLifecycleLease.AcquireMediaAsync(db, me, cancellationToken);
            // Recheck under the same media authority used by removal/placement; parallel uploads cannot exceed the library.
            if (await db.MediaItems.CountAsync(i => i.AccountId == me && i.Purpose == null && i.RemovedAt == null && i.Status != MediaStatus.Removed, cancellationToken) >= MediaPolicy.MaxKept)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoLimit, $"You can keep up to {MediaPolicy.MaxKept} photos. Delete one first.");
            if (await db.MediaItems.CountAsync(i => i.AccountId == me && i.Purpose == null && i.CreatedAt > now - TimeSpan.FromHours(1), cancellationToken) >= MediaPolicy.UploadsPerHour)
                return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.UploadLimit, "You've uploaded a lot of photos in the last hour. Try again later.");
            var (item, error) = await pipeline.UploadAsync(me, buffer.ToArray(), replaces, now, cancellationToken);
            return error switch
            {
                MediaUploadError.None => Results.Accepted($"/api/me/photos/{item!.Id}", View(item, [], null)),
                MediaUploadError.TooBig => Problem(StatusCodes.Status413PayloadTooLarge, ProblemCodes.PhotoTooBig, "Photos can be up to 10 MB."),
                MediaUploadError.UnsupportedType => Problem(StatusCodes.Status415UnsupportedMediaType, ProblemCodes.PhotoUnsupported, "Use a JPEG, PNG or WebP photo."),
                MediaUploadError.Animated => Problem(StatusCodes.Status415UnsupportedMediaType, ProblemCodes.PhotoUnsupported, "Animated images can't be used."),
                MediaUploadError.TooSmall => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, $"Photos need to be at least {MediaPolicy.MinSide} pixels on each side."),
                MediaUploadError.TooLarge => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That photo has too many pixels."),
                _ => Problem(StatusCodes.Status400BadRequest, ProblemCodes.PhotoInvalid, "That photo couldn't be read."),
            };
        }).DisableAntiforgery();
        api.MapGet("/me/photos", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await OwnAsync(accounts.Db!, accounts.Services.GetRequiredService<MediaLinks>(), current!.Profile!.AccountId, clock.GetUtcNow(), cancellationToken));
        });
        api.MapDelete("/me/photos/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var me = current!.Profile!.AccountId;
            var item = await accounts.Db!.MediaItems.SingleOrDefaultAsync(i => i.Id == id && i.AccountId == me && i.Purpose == null && i.RemovedAt == null, cancellationToken);
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't available.");
            var pipeline = accounts.Services.GetRequiredService<MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            await pipeline.RemoveAsync(item, me, clock.GetUtcNow(), cancellationToken);
            return Results.Ok(await OwnAsync(accounts.Db!, accounts.Services.GetRequiredService<MediaLinks>(), me, clock.GetUtcNow(), cancellationToken));
        });
        api.MapPut("/me/photos/placement", async (PhotoPlacementUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, me, now) = (accounts.Db!, current!.Profile!.AccountId, clock.GetUtcNow());
            var modes = await db.ConnectionModes.Where(m => m.Available).Select(m => m.Id).ToListAsync(cancellationToken);
            var ids = update.MediaIds ?? [];
            if (update.Scope is null || !MediaPolicy.ValidScope(update.Scope, modes) || ids.Distinct().Count() != ids.Count)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidPlacement, "Choose one mode and distinct photos from your library.");
            await using var mediaLease = await AccountLifecycleLease.AcquireMediaAsync(db, me, cancellationToken);
            var max = MediaPolicy.ModeLimit(await Moderation.Entitlements.TierAsync(db, current.Profile!, now, cancellationToken));
            var stored = await db.MediaPlacements.Where(p => p.AccountId == me && p.Scope == update.Scope).Select(p => p.MediaId).ToListAsync(cancellationToken);
            if (!MediaPolicy.PlacementAllowed(ids, stored, max) || update.MainMediaId is { } main && !ids.Contains(main))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidPlacement, $"Choose up to {max} photos, including an optional main from that gallery. Existing excess photos can be reordered or removed.");
            var approved = await db.MediaItems.Where(i => ids.Contains(i.Id) && i.AccountId == me && i.Purpose == null && i.Status == MediaStatus.Passed && i.RemovedAt == null).Select(i => i.Id).ToListAsync(cancellationToken);
            if (approved.Count != ids.Count)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoNotApproved, "Only your approved photos can be shown.");
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.MediaPlacements.Where(p => p.AccountId == me && p.Scope == update.Scope).ExecuteDeleteAsync(cancellationToken);
            db.MediaPlacements.AddRange(ids.Select((id, i) => new MediaPlacement { AccountId = me, Scope = update.Scope, Position = i, MediaId = id, IsMain = update.MainMediaId == id }));
            Audit.Record(db, now, me, "account", "media.placed", "connection-mode", update.Scope, $"photos:{ids.Count}");
            await db.SaveChangesAsync(cancellationToken);
            await Profiles.ProfilePrivacy.CaptureAsync(db, me, now, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await OwnAsync(db, accounts.Services.GetRequiredService<MediaLinks>(), me, now, cancellationToken));
        });

        // The signed link itself is the permission (made only after the viewer's access was checked); no session needed,
        // so an <img> in the browser or the app can load it. Blocked items are never served, whatever the link says.
        app.MapGet("/api/media/{token}", async (string token, HttpContext context, IServiceProvider services, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (services.GetService<SwoovrDbContext>() is not { } db) return Results.NotFound();
            var links = services.GetRequiredService<MediaLinks>();
            if (links.Key is null || MediaPolicy.ReadLink(links.Key, token, clock.GetUtcNow()) is not var (id, variant)) return Results.NotFound();
            var item = await db.MediaItems.FindAsync([id], cancellationToken);
            if (item is null || await services.GetRequiredService<MediaPipeline>().ServeAsync(item, variant, cancellationToken) is not { } bytes) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, max-age=300";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(bytes, "image/jpeg");
        });

        var admin = app.MapGroup("/api/admin/media").RequireAuthorization();
        admin.MapGet("/review", async (int? page, string? reason, string? kind, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            if (page is < 1 or > 100000 || reason?.Length > 64 || kind is not (null or "profile" or "attachment")) return Problem(400, "invalid-review-filter", "Choose a valid review filter and page.");
            var (db, now, actor) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            var eligible = PhotoReviewWorkflows.Eligible(db, actor, current.Allows(AccountRole.Admin));
            var query = eligible.Where(i => i.Status == MediaStatus.Uncertain && i.RemovedAt == null);
            if (kind is not null) query = kind == "attachment" ? query.Where(i => i.Purpose == "attachment") : query.Where(i => i.Purpose == null);
            if (reason is not null) query = query.Where(i => i.Signals != null && i.Signals.Contains("\"reason\":\"" + reason + "\""));
            var p = page ?? 1; var total = await query.CountAsync(cancellationToken);
            var items = await query.OrderBy(i => i.CreatedAt).ThenBy(i => i.Id).Skip((p - 1) * 25).Take(25).ToListAsync(cancellationToken);
            var pending = eligible.Where(i => i.Status == MediaStatus.Pending && i.RemovedAt == null);
            var since = now.AddDays(-30); var ids = eligible.Select(i => i.Id);
            var counts = await db.Set<MediaDecisionRecord>().Where(d => d.At >= since && ids.Contains(d.MediaId) && d.Decision != "escalate").GroupBy(d => d.Code).Select(g => new { Code = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Code, g => g.Count, cancellationToken);
            return Results.Ok(new MediaReviewQueue(items.Select(i => new MediaReviewItem(i.Id, i.AccountId, i.CreatedAt, SignalReason(i.Signals), Signals(i.Signals), i.Screener ?? "unknown", i.KnownContentChecked, i.Purpose)).ToList(),
                await pending.CountAsync(cancellationToken), await pending.CountAsync(i => i.CreatedAt < now.AddHours(-1), cancellationToken), MediaReviewReasons.All.Select(ReasonView).ToList(), counts, since, p, total, 25,
                await PhotoReviewWorkflows.AckUntilAsync(db, actor, now, cancellationToken), PhotoReviewPolicy.NoticeVersion, PhotoReviewPolicy.Notice));
        });
        admin.MapPost("/{id:guid}/view", async (Guid id, MediaViewRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, actor) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            if (await PhotoReviewWorkflows.AckUntilAsync(db, actor, now, cancellationToken) is null) return PhotoReviewWorkflows.AckRequired();
            if (request.Purpose?.Trim() is not { Length: > 0 and <= MediaPolicy.PurposeMax } purpose)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Say why you need to look at this photo.");
            // Only uncertain photos can be looked at; a blocked one reads as not found (procedure §4).
            var item = await PhotoReviewWorkflows.Eligible(db, actor, current.Allows(AccountRole.Admin)).SingleOrDefaultAsync(i => i.Id == id && i.Status == MediaStatus.Uncertain && i.RemovedAt == null, cancellationToken);
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't waiting for review.");
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, item.AccountId, cancellationToken);
            if (!await PhotoReviewWorkflows.Eligible(db, actor, current.Allows(AccountRole.Admin)).AnyAsync(i => i.Id == id, cancellationToken))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't waiting for review.");
            now = clock.GetUtcNow();
            if (await StaffAccessEndpoints.ExcludedAsync(db, actor, [item.AccountId], cancellationToken)) return StaffAccessEndpoints.Conflict();
            if (await PhotoReviewWorkflows.ReviewLinkAsync(db, accounts.Services.GetRequiredService<MediaLinks>(), actor, item.Id, "review", now, cancellationToken) is not { } link)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photo links aren't configured.");
            db.MediaReviewViews.Add(new MediaReviewView { MediaId = item.Id, ActorAccountId = actor, Purpose = purpose, At = now });
            Audit.Record(db, now, actor, "account", "media.viewed", "media", item.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(link);
        });
        // #41: a reported chat photo, for an admin working the case: only a photo the case's evidence names, never a blocked
        // one, with a recorded purpose (media_review_views, the audit and the sensitive-access log), conflict rules applied.
        admin.MapPost("/{id:guid}/case-view", async (Guid id, MediaCaseViewRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminOnlyAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, actor) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            if (request.Purpose?.Trim() is not { Length: > 0 and <= MediaPolicy.PurposeMax } purpose || request.CaseId is not { } caseId)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Say which case and why you need to look at this photo.");
            await using var barrier = await Retention.PreservationLease.SharedAsync(db, cancellationToken);
            var named = (await db.CaseEvidence.Where(e => e.CaseId == caseId).Select(e => e.Snapshot).ToListAsync(cancellationToken)).Any(s => s.Contains(id.ToString()));
            var item = named ? await db.MediaItems.SingleOrDefaultAsync(i => i.Id == id && i.Purpose == AttachmentPolicy.Purpose
                && i.ObjectKey != null, cancellationToken) : null;
            if (item is null || !MediaPolicy.CaseReviewable(item)) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't part of this case's evidence.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, actor, await Retention.SafetyRetention.CaseAccountsAsync(db, caseId, cancellationToken), cancellationToken)) return StaffAccessEndpoints.Conflict();
            if (accounts.Services.GetRequiredService<MediaLinks>().For(item.Id, "case", now) is not { } link)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photo links aren't configured.");
            db.MediaReviewViews.Add(new MediaReviewView { MediaId = item.Id, ActorAccountId = actor, Purpose = purpose, At = now });
            Audit.Record(db, now, actor, "account", "media.viewed", "media", item.Id.ToString(), "case");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new MediaReviewLink(item.Id, link, now + MediaPolicy.LinkLifetime));
        });
        admin.MapPost("/{id:guid}/decision", async (Guid id, MediaDecision request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, actor) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            if (await PhotoReviewWorkflows.AckUntilAsync(db, actor, now, cancellationToken) is null) return PhotoReviewWorkflows.AckRequired();
            var review=MediaReviewReasons.Create(request.Decision,request.ReasonCode,request.Reason);
            if(review is null)return Problem(StatusCodes.Status400BadRequest,ProblemCodes.InvalidDecision,"Choose a matching reason. Other rejection needs notes; approval notes are not supported.");
            // Uncertain photos are decided here; an approved photo can still be taken down (reject) or escalated.
            var item = await PhotoReviewWorkflows.Eligible(db, actor, current.Allows(AccountRole.Admin)).SingleOrDefaultAsync(i => i.Id == id && i.RemovedAt == null
                && (i.Status == MediaStatus.Uncertain || i.Status == MediaStatus.Passed && request.Decision != "approve"), cancellationToken);
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't waiting for a decision.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, actor, [item.AccountId], cancellationToken)) return StaffAccessEndpoints.Conflict();
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, item.AccountId, cancellationToken);
            if (!await PhotoReviewWorkflows.Eligible(db, actor, current.Allows(AccountRole.Admin)).AnyAsync(i => i.Id == id, cancellationToken))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't waiting for a decision.");
            now = clock.GetUtcNow();
            if (await PhotoReviewWorkflows.AckUntilAsync(db, actor, now, cancellationToken) is null) return PhotoReviewWorkflows.AckRequired();
            var pipeline = accounts.Services.GetRequiredService<MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            var applied = request.Decision switch
            {
                "approve" => await pipeline.PassAsync(item, actor, now, cancellationToken,review),
                "reject" => await pipeline.RejectAsync(item, actor, review.Notes, now, cancellationToken,review),
                _ => await pipeline.BlockAsync(item, actor, "escalated-by-reviewer", now, cancellationToken,review),
            };
            if (!applied) return Problem(StatusCodes.Status409Conflict, ProblemCodes.UnknownPhoto, "That photo isn't waiting for a decision.");
            return Results.Ok(new { item.Id, state = request.Decision switch { "approve" => "approved", "reject" => "rejected", _ => "escalated" } });
        });
    }

    private static async Task<(CurrentAccount? Current, IResult? Denied)> AdminOnlyAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        return current.Allows(AccountRole.Admin) ? (current, null) : (null, Results.Forbid());
    }

    private static MediaReviewReasonView ReasonView(MediaReviewReasonDefinition reason)=>new(reason.Code,reason.Decision,reason.Code switch
    {
        "APPROVED_STANDARD"=>"Meets current photo rules","APPROVED_NON_EXPLICIT"=>"Non-explicit image permitted by current rules",
        "APPROVED_AFTER_REVIEW"=>"Acceptable after manual review","APPROVED_FALSE_POSITIVE"=>"Automated flag cleared after review",
        "REJECT_EXPLICIT_BETA"=>"Explicit content is not permitted in beta","REJECT_IMPERSONATION"=>"Impersonation or unauthorised image use",
        "REJECT_PRIVACY"=>"Prohibited personal or location information","REJECT_VIOLENCE"=>"Graphic violence or disturbing content",
        "REJECT_HATE"=>"Hate or prohibited extremist imagery","REJECT_SPAM"=>"Spam or prohibited promotion","REJECT_IMAGE_QUALITY"=>"Technical or image-quality requirements",
        "REJECT_OTHER"=>"Other violation — notes required","ESCALATE_CHILD_SAFETY"=>"Child-safety concern — specialist review",
        "ESCALATE_NON_CONSENSUAL"=>"Possible non-consensual image abuse — specialist review","ESCALATE_OTHER"=>"Uncertain serious concern — specialist review",
        "LEGACY_APPROVE"=>"Uncategorised legacy-client approval","LEGACY_REJECT"=>"Uncategorised legacy-client rejection","LEGACY_ESCALATE"=>"Uncategorised legacy-client escalation",
        _=>reason.Code,
    },reason.RequiresNotes,reason.Selectable);

    private static string SignalReason(string? signals) => Read(signals, "reason") ?? "unknown";

    private static IReadOnlyList<string> Signals(string? signals)
    {
        if (signals is null) return [];
        using var document = System.Text.Json.JsonDocument.Parse(signals);
        var list = new List<string>();
        if (document.RootElement.TryGetProperty("detections", out var detections))
            foreach (var d in detections.EnumerateArray()) list.Add($"{d.GetProperty("label").GetString()} {d.GetProperty("score").GetDouble():0.00}");
        if (document.RootElement.TryGetProperty("nsfw", out var nsfw) && nsfw.ValueKind == System.Text.Json.JsonValueKind.True) list.Add("NSFW threshold met");
        return list;
    }

    private static string? Read(string? json, string name)
    {
        if (json is null) return null;
        using var document = System.Text.Json.JsonDocument.Parse(json);
        return document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() : null;
    }

    private static MyPhotoView View(MediaItem item, IReadOnlyList<string> scopes, string? link) =>
        new(item.Id, MediaPolicy.MemberState(item.Status), item.Status == MediaStatus.Passed ? link : null, item.CreatedAt, scopes, item.ReplacesId);

    public static async Task<MyPhotosView> OwnAsync(SwoovrDbContext db, MediaLinks links, Guid me, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var items = await db.MediaItems.Where(i => i.AccountId == me && i.Purpose == null && i.RemovedAt == null && i.Status != MediaStatus.Removed).OrderByDescending(i => i.CreatedAt).ToListAsync(cancellationToken);
        var places = await db.MediaPlacements.Where(p => p.AccountId == me).OrderBy(p => p.Position).ToListAsync(cancellationToken);
        var modes = places.Where(p => p.Scope != MediaPolicy.Main).GroupBy(p => p.Scope)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<Guid>)g.Select(p => p.MediaId).ToList());
        var profile = await db.MemberProfiles.SingleAsync(p => p.AccountId == me, cancellationToken);
        var tier = await Moderation.Entitlements.TierAsync(db, profile, now, cancellationToken);
        var mains = places.Where(p => p.Scope != MediaPolicy.Main && p.IsMain).GroupBy(p => p.Scope).ToDictionary(g => g.Key, g => g.First().MediaId);
        return new(items.Select(i => View(i, places.Where(p => p.MediaId == i.Id && p.Scope != MediaPolicy.Main).Select(p => p.Scope).ToList(), links.For(i.Id, "member", now))).ToList(),
            null, modes, MediaPolicy.ModeLimit(tier), MediaPolicy.MaxKept, mains, Wire(tier));
    }

    /// <summary>
    /// Approved photos of people the viewer is already allowed to see: each account's main photo and its photos for the
    /// given modes. Placements only ever point at approved photos, and the status is checked again here.
    /// </summary>
    public static async Task<(Dictionary<(Guid, string), PhotoView> Main, Dictionary<(Guid, string), List<PhotoView>> Modes)> PhotosAsync(SwoovrDbContext db, MediaLinks links,
        IReadOnlyCollection<Guid> accounts, IReadOnlyCollection<string> modes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await (from p in db.MediaPlacements join i in db.MediaItems on p.MediaId equals i.Id
                          where accounts.Contains(p.AccountId) && modes.Contains(p.Scope)
                                && i.Status == MediaStatus.Passed && i.RemovedAt == null
                          orderby p.Position select new { p.AccountId, p.Scope, p.MediaId, p.IsMain, p.Position }).ToListAsync(cancellationToken);
        var limits = await LimitsAsync(db, accounts, now, cancellationToken);
        var visible = rows.GroupBy(r => (r.AccountId, r.Scope)).SelectMany(g => g.Take(limits.GetValueOrDefault(g.Key.AccountId, MediaPolicy.ModeLimit(PlanTier.Free)))).ToList();
        PhotoView? View(Guid id) => links.For(id, "member", now) is { } link ? new PhotoView(id, link) : null;
        var main = visible.Where(r => r.IsMain).GroupBy(r => (r.AccountId, r.Scope))
            .Select(g => (g.Key, Photo: View(g.First().MediaId))).Where(r => r.Photo is not null).ToDictionary(r => r.Key, r => r.Photo!);
        var perMode = visible.GroupBy(r => (r.AccountId, r.Scope)).ToDictionary(g => g.Key, g => g.Select(r => View(r.MediaId)).OfType<PhotoView>().ToList());
        return (main, perMode);
    }

    /// <summary>Tier limits are resolved once per batch from the existing entitlement policy, including founder rights.</summary>
    public static async Task<Dictionary<Guid, int>> LimitsAsync(SwoovrDbContext db, IReadOnlyCollection<Guid> accounts, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profiles = await db.MemberProfiles.Where(p => accounts.Contains(p.AccountId)).ToListAsync(cancellationToken);
        var grants = await db.Entitlements.Where(e => accounts.Contains(e.AccountId) && e.RevokedAt == null).ToListAsync(cancellationToken);
        return profiles.ToDictionary(p => p.AccountId, p => MediaPolicy.ModeLimit(EntitlementPolicy.Effective(grants.Where(e => e.AccountId == p.AccountId), p.FoundingMember, p.FounderEntitlementFrom, now)));
    }
}
