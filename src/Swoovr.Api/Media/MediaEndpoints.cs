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
            var max = update.Scope == MediaPolicy.Main ? 1 : MediaPolicy.MaxPerMode;
            if (update.Scope is null || !MediaPolicy.ValidScope(update.Scope, modes) || ids.Count > max || ids.Distinct().Count() != ids.Count)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidPlacement, update.Scope == MediaPolicy.Main ? "Pick one main photo." : $"Pick up to {MediaPolicy.MaxPerMode} photos for a mode.");
            await using var mediaLease = await MediaAccountLease.AcquireAsync(db, me, cancellationToken);
            var approved = await db.MediaItems.Where(i => ids.Contains(i.Id) && i.AccountId == me && i.Purpose == null && i.Status == MediaStatus.Passed && i.RemovedAt == null).Select(i => i.Id).ToListAsync(cancellationToken);
            if (approved.Count != ids.Count)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.PhotoNotApproved, "Only your approved photos can be shown.");
            await db.MediaPlacements.Where(p => p.AccountId == me && p.Scope == update.Scope).ExecuteDeleteAsync(cancellationToken);
            db.MediaPlacements.AddRange(ids.Select((id, i) => new MediaPlacement { AccountId = me, Scope = update.Scope, Position = i, MediaId = id }));
            Audit.Record(db, now, me, "account", "media.placed", "connection-mode", update.Scope, $"photos:{ids.Count}");
            await db.SaveChangesAsync(cancellationToken);
            await Profiles.ProfilePrivacy.CaptureAsync(db, me, now, cancellationToken);
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
        admin.MapGet("/review", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminOnlyAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var items = await db.MediaItems.Where(i => i.Status == MediaStatus.Uncertain && i.RemovedAt == null).OrderBy(i => i.CreatedAt).Take(100).ToListAsync(cancellationToken);
            var pending = await db.MediaItems.Where(i => i.Status == MediaStatus.Pending && i.RemovedAt == null).Select(i => i.CreatedAt).ToListAsync(cancellationToken);
            return Results.Ok(new MediaReviewQueue(items.Select(i => new MediaReviewItem(i.Id, i.AccountId, i.CreatedAt, SignalReason(i.Signals), Signals(i.Signals),
                i.Screener ?? "", i.KnownContentChecked, i.Purpose)).ToList(), pending.Count, pending.Count(c => c < now - TimeSpan.FromHours(1))));
        });
        admin.MapPost("/{id:guid}/view", async (Guid id, MediaViewRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminOnlyAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, actor) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            if (request.Purpose?.Trim() is not { Length: > 0 and <= MediaPolicy.PurposeMax } purpose)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.PurposeRequired, "Say why you need to look at this photo.");
            // Only uncertain photos can be looked at; a blocked one reads as not found (procedure §4).
            var item = await db.MediaItems.SingleOrDefaultAsync(i => i.Id == id && i.Status == MediaStatus.Uncertain && i.RemovedAt == null, cancellationToken);
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't waiting for review.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, actor, [item.AccountId], cancellationToken)) return StaffAccessEndpoints.Conflict();
            if (accounts.Services.GetRequiredService<MediaLinks>().For(item.Id, "review", now) is not { } link)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photo links aren't configured.");
            db.MediaReviewViews.Add(new MediaReviewView { MediaId = item.Id, ActorAccountId = actor, Purpose = purpose, At = now });
            Audit.Record(db, now, actor, "account", "media.viewed", "media", item.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new MediaReviewLink(item.Id, link, now + MediaPolicy.LinkLifetime));
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
            var named = (await db.CaseEvidence.Where(e => e.CaseId == caseId).Select(e => e.Snapshot).ToListAsync(cancellationToken)).Any(s => s.Contains(id.ToString()));
            var item = named ? await db.MediaItems.SingleOrDefaultAsync(i => i.Id == id && i.Purpose == AttachmentPolicy.Purpose
                && (i.Status == MediaStatus.Passed || i.Status == MediaStatus.Uncertain) && i.ObjectKey != null, cancellationToken) : null;
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't part of this case's evidence.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, actor, [item.AccountId], cancellationToken)) return StaffAccessEndpoints.Conflict();
            if (accounts.Services.GetRequiredService<MediaLinks>().For(item.Id, "case", now) is not { } link)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photo links aren't configured.");
            db.MediaReviewViews.Add(new MediaReviewView { MediaId = item.Id, ActorAccountId = actor, Purpose = purpose, At = now });
            Audit.Record(db, now, actor, "account", "media.viewed", "media", item.Id.ToString(), "case");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new MediaReviewLink(item.Id, link, now + MediaPolicy.LinkLifetime));
        });
        admin.MapPost("/{id:guid}/decision", async (Guid id, MediaDecision request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminOnlyAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now, actor) = (accounts.Db!, clock.GetUtcNow(), current!.Account!.Id);
            if (request.Decision is not ("approve" or "reject" or "escalate") || request.Reason is { Length: > MediaPolicy.ReasonMax })
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidDecision, "Choose approve, reject or escalate.");
            // Uncertain photos are decided here; an approved photo can still be taken down (reject) or escalated.
            var item = await db.MediaItems.SingleOrDefaultAsync(i => i.Id == id && i.RemovedAt == null
                && (i.Status == MediaStatus.Uncertain || i.Status == MediaStatus.Passed && request.Decision != "approve"), cancellationToken);
            if (item is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownPhoto, "That photo isn't waiting for a decision.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, actor, [item.AccountId], cancellationToken)) return StaffAccessEndpoints.Conflict();
            var pipeline = accounts.Services.GetRequiredService<MediaPipeline>();
            if (!pipeline.Available) return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.MediaUnavailable, "Photos aren't available right now.");
            var applied = request.Decision switch
            {
                "approve" => await pipeline.PassAsync(item, actor, now, cancellationToken),
                "reject" => await pipeline.RejectAsync(item, actor, request.Reason?.Trim(), now, cancellationToken),
                _ => await pipeline.BlockAsync(item, actor, "escalated-by-reviewer", now, cancellationToken),
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
        return new(items.Select(i => View(i, places.Where(p => p.MediaId == i.Id).Select(p => p.Scope).ToList(), links.For(i.Id, "member", now))).ToList(),
            places.FirstOrDefault(p => p.Scope == MediaPolicy.Main)?.MediaId, modes, MediaPolicy.MaxPerMode, MediaPolicy.MaxKept);
    }

    /// <summary>
    /// Approved photos of people the viewer is already allowed to see: each account's main photo and its photos for the
    /// given modes. Placements only ever point at approved photos, and the status is checked again here.
    /// </summary>
    public static async Task<(Dictionary<Guid, PhotoView> Main, Dictionary<(Guid, string), List<PhotoView>> Modes)> PhotosAsync(SwoovrDbContext db, MediaLinks links,
        IReadOnlyCollection<Guid> accounts, IReadOnlyCollection<string> modes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var rows = await (from p in db.MediaPlacements join i in db.MediaItems on p.MediaId equals i.Id
                          where accounts.Contains(p.AccountId) && (p.Scope == MediaPolicy.Main || modes.Contains(p.Scope))
                                && i.Status == MediaStatus.Passed && i.RemovedAt == null
                          orderby p.Position select new { p.AccountId, p.Scope, p.MediaId }).ToListAsync(cancellationToken);
        PhotoView? View(Guid id) => links.For(id, "member", now) is { } link ? new PhotoView(id, link) : null;
        var main = rows.Where(r => r.Scope == MediaPolicy.Main).Select(r => (r.AccountId, View(r.MediaId))).Where(r => r.Item2 is not null)
            .GroupBy(r => r.AccountId).ToDictionary(g => g.Key, g => g.First().Item2!);
        var perMode = rows.Where(r => r.Scope != MediaPolicy.Main).GroupBy(r => (r.AccountId, r.Scope))
            .ToDictionary(g => g.Key, g => g.Select(r => View(r.MediaId)).OfType<PhotoView>().ToList());
        return (main, perMode);
    }
}
