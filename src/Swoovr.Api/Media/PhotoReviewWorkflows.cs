using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Media;

public static class PhotoReviewWorkflows
{
    public static IQueryable<MediaItem> Eligible(SwoovrDbContext db, Guid actor, bool mayReviewStaffAffiliated = true)
    {
        var staff = actor.ToString();
        return db.MediaItems.Where(i => i.AccountId != actor
            && (mayReviewStaffAffiliated || !db.MemberProfiles.Any(p => p.AccountId == i.AccountId && p.Employee))
            && !db.StaffExclusions.Any(e => e.StaffAccountId == actor && e.SubjectAccountId == i.AccountId)
            && !db.StaffMemberLinks.Any(e => e.StaffAccountId == actor && e.MemberAccountId == i.AccountId)
            && !db.AuditEntries.Any(a => a.Action == "staff-link.set" && a.ActorAccountId != null && a.TargetId == staff && a.Detail == i.AccountId.ToString()));
    }
    public static IQueryable<MediaAppeal> PendingAppeals(SwoovrDbContext db, Guid actor)
    {
        var eligible = Eligible(db, actor).Where(i => i.Status == MediaStatus.Rejected).Select(i => i.Id);
        return from a in db.Set<MediaAppeal>() join d in db.Set<MediaDecisionRecord>() on a.DecisionId equals d.Id
               where a.DecidedAt == null && d.ActorId != actor && eligible.Contains(a.MediaId) select a;
    }
    public static Task<Guid> LatestDecisionAsync(SwoovrDbContext db, Guid media, CancellationToken token) => db.Set<MediaDecisionRecord>()
        .Where(d => d.MediaId == media && d.Decision != "reconsider-request").OrderByDescending(d => d.At).ThenByDescending(d => d.Id).Select(d => d.Id).FirstOrDefaultAsync(token);
    public static void FinalizeUnavailableAppeal(SwoovrDbContext db, MediaAppeal appeal, MediaItem item, Guid actor, DateTimeOffset now)
    {
        if (appeal.DecidedAt is not null) return;
        appeal.DecidedAt = now; appeal.DecidedBy = actor; appeal.Outcome = "unavailable";
        Stage(db, item, actor, "appeal-unavailable", "PHOTO_UNAVAILABLE", null, now);
        Audit.Record(db, now, actor, "account", "media.appeal-decided", "media-appeal", appeal.Id.ToString(), "unavailable");
    }
    public static async Task<DateTimeOffset?> AckUntilAsync(SwoovrDbContext db, Guid actor, DateTimeOffset now, CancellationToken token)
    {
        var at = await db.Set<MediaSafetyAcknowledgement>().Where(a => a.StaffAccountId == actor && a.Version == PhotoReviewPolicy.NoticeVersion).Select(a => (DateTimeOffset?)a.AcknowledgedAt).SingleOrDefaultAsync(token);
        return PhotoReviewPolicy.Acknowledged(at, now) ? at + PhotoReviewPolicy.AcknowledgementLifetime : null;
    }
    public static async Task<MediaReviewLink?> ReviewLinkAsync(SwoovrDbContext db, MediaLinks links, Guid actor, Guid media, string variant, DateTimeOffset now, CancellationToken token)
    {
        var until = await AckUntilAsync(db, actor, now, token); if (until is null || links.Key is null) return null;
        var expires = now + MediaPolicy.LinkLifetime < until.Value ? now + MediaPolicy.LinkLifetime : until.Value;
        return new MediaReviewLink(media, "api/media/" + MediaPolicy.Link(links.Key, media, variant, expires), expires);
    }
    public static IResult AckRequired() => Problem(403, "media-safety-ack-required", "Read and acknowledge the current photo-review safety notice.");
    public static MediaDecisionRecord Stage(SwoovrDbContext db, MediaItem item, Guid? actor, string decision, string code, string? notes, DateTimeOffset now)
    {
        var record = new MediaDecisionRecord { Id = Guid.NewGuid(), MediaId = item.Id, AccountId = item.AccountId, ActorId = actor, Decision = decision, Code = code, Notes = notes, At = now };
        db.Set<MediaDecisionRecord>().Add(record);
        if (decision is "reject" or "escalate" or "appeal-upheld" or "appeal-approved" or "reconsider-approved" or "appeal-unavailable")
            db.Notifications.Add(new Notification { Id = Guid.NewGuid(), AccountId = item.AccountId, Kind = NotificationKind.PhotoDecision, SubjectId = record.Id, DedupeKey = "photo-decision:" + record.Id.ToString("N"), CreatedAt = now, UpdatedAt = now });
        return record;
    }
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/admin/media").RequireAuthorization();
        admin.MapPost("/safety-ack", async (MediaSafetyAck request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token); if (denied is not null) return denied;
            if (!request.Confirmed || request.Version != PhotoReviewPolicy.NoticeVersion) return Problem(400, "invalid-safety-ack", "Confirm the current safety notice.");
            var db = accounts.Db!; var actor = current!.Account!.Id; var now = clock.GetUtcNow();
            await using var lease = await AccountLifecycleLease.AcquireAsync(db, actor, token);
            var ack = await db.Set<MediaSafetyAcknowledgement>().FindAsync([actor, request.Version], token);
            if (ack is null) db.Add(ack = new MediaSafetyAcknowledgement { StaffAccountId = actor, Version = request.Version });
            ack.AcknowledgedAt = now; Audit.Record(db, now, actor, "account", "media.safety-ack", "notice", request.Version); await db.SaveChangesAsync(token);
            return Results.Ok(new { expiresAt = now + PhotoReviewPolicy.AcknowledgementLifetime });
        });
        admin.MapGet("/workload", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token); if (denied is not null) return denied;
            var db = accounts.Db!; var eligible = Eligible(db, current!.Account!.Id, current.Allows(AccountRole.Admin));
            var ids = eligible.Where(i => i.Status == MediaStatus.Rejected).Select(i => i.Id);
            return Results.Ok(new PhotoWorkload(await eligible.CountAsync(i => i.Status == MediaStatus.Uncertain && i.RemovedAt == null, token),
                await eligible.CountAsync(i => i.Status == MediaStatus.Pending && i.RemovedAt == null, token),
                current.Allows(AccountRole.Admin) ? await eligible.CountAsync(i => i.Status == MediaStatus.Blocked, token) : 0,
                current.Allows(AccountRole.Admin) ? await PendingAppeals(db, current.Account!.Id).CountAsync(token) : 0,
                current.Allows(AccountRole.Admin) ? await eligible.CountAsync(i => i.Status == MediaStatus.Rejected && i.RemovedAt == null, token) : 0));
        });
        admin.MapGet("/history", async (int? page, string? decision, ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token); if (denied is not null) return denied;
            var db = accounts.Db!; var eligible = Eligible(db, current!.Account!.Id, current.Allows(AccountRole.Admin)).Where(i => i.Status != MediaStatus.Blocked).Select(i => i.Id);
            var query = db.Set<MediaDecisionRecord>().Where(r => eligible.Contains(r.MediaId) && r.Decision != "escalate");
            if (decision is not null) query = query.Where(r => r.Decision == decision);
            var p = Math.Clamp(page ?? 1, 1, 100000); var total = await query.CountAsync(token);
            var rows = await query.OrderByDescending(r => r.At).ThenBy(r => r.Id).Skip((p - 1) * 25).Take(25).Select(r => new PhotoDecisionHistory(r.Id, r.MediaId, r.AccountId, r.ActorId, r.Decision, r.Code, r.At)).ToListAsync(token);
            return Results.Ok(new PhotoDecisionPage(rows, p, total));
        });
        admin.MapGet("/appeals", async (int? page, ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, token); if (denied is not null) return denied;
            var db = accounts.Db!; var eligible = Eligible(db, current!.Account!.Id, current.Allows(AccountRole.Admin)).Where(i => i.Status != MediaStatus.Blocked).Select(i => i.Id); var actor = current.Account!.Id;
            var query = PendingAppeals(db, actor);
            var p = Math.Clamp(page ?? 1, 1, 100000); var total = await query.CountAsync(token);
            var rows = await query.OrderBy(a => a.SubmittedAt).ThenBy(a => a.Id).Skip((p - 1) * 25).Take(25).Select(a => new PhotoAppealView(a.Id, a.DecisionId, a.MediaId, a.AccountId, a.SubmittedAt, a.Text, a.Outcome, a.DecidedAt)).ToListAsync(token);
            return Results.Ok(new PhotoAppealPage(rows, p, total));
        });
        admin.MapPost("/appeals/{id:guid}/resolve", ResolveAsync);
        admin.MapPost("/{id:guid}/reconsider", ReconsiderAsync);
        admin.MapPost("/{id:guid}/appeal-view", async (Guid id, MediaViewRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, token); if (denied is not null) return denied;
            var db = accounts.Db!; var now = clock.GetUtcNow(); var actor = current!.Account!.Id;
            if (await AckUntilAsync(db, actor, now, token) is null) return AckRequired();
            if (request.Purpose?.Trim() is not { Length: > 0 and <= 500 } purpose) return Problem(400, ProblemCodes.PurposeRequired, "Record a review purpose.");
            var item = await Eligible(db, actor).SingleOrDefaultAsync(i => i.Id == id && i.Status == MediaStatus.Rejected && i.RemovedAt == null && i.RejectedRetainUntil > now && i.ObjectKey != null, token);
            if (item is null) return Results.NotFound();
            var links = accounts.Services.GetRequiredService<MediaLinks>(); var link = await ReviewLinkAsync(db, links, actor, id, "appeal", now, token); if (link is null) return Results.NotFound();
            db.MediaReviewViews.Add(new MediaReviewView { MediaId = id, ActorAccountId = actor, Purpose = purpose, At = now }); Audit.Record(db, now, actor, "account", "media.appeal-viewed", "media", id.ToString()); await db.SaveChangesAsync(token);
            return Results.Ok(link);
        });
        app.MapGet("/api/me/photos/decisions", MemberDecisionsAsync).RequireAuthorization();
        app.MapPost("/api/me/photos/decisions/{id:guid}/appeal", AppealAsync).RequireAuthorization();
    }

    private static async Task<IResult> MemberDecisionsAsync(ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token)
    {
        var (current, denied) = await MemberAsync(user, accounts, token, barredExempt: true); if (denied is not null) return denied;
        var db = accounts.Db!; var me = current!.Account!.Id; var now = clock.GetUtcNow();
        var rows = await db.Set<MediaDecisionRecord>().Where(d => d.AccountId == me && (d.Decision == "reject" || d.Decision == "escalate" || d.Decision == "appeal-upheld" || d.Decision == "appeal-approved" || d.Decision == "reconsider-approved" || d.Decision == "appeal-unavailable")).OrderByDescending(d => d.At).Take(100).ToListAsync(token);
        var appeals = await db.Set<MediaAppeal>().Where(a => a.AccountId == me).ToListAsync(token);
        var media = await db.MediaItems.Where(i => i.AccountId == me).ToDictionaryAsync(i => i.Id, token);
        var latest = new Dictionary<Guid, Guid>();
        foreach (var mediaId in rows.Select(d => d.MediaId).Distinct()) latest[mediaId] = await LatestDecisionAsync(db, mediaId, token);
        return Results.Ok(rows.Select(d => { var appeal = appeals.FirstOrDefault(a => a.DecisionId == d.Id); var available = media.TryGetValue(d.MediaId, out var item) && item.Status == MediaStatus.Rejected && item.RemovedAt == null && item.ObjectKey != null && item.RejectedRetainUntil > now;
            return new PhotoDecisionView(d.Id, d.MediaId, d.Decision is "reject" or "escalate" ? "not-approved" : d.Decision, d.Decision == "reconsider-approved" ? "Your rejected photo was reconsidered and approved. Choose where to show it." : d.Decision == "appeal-approved" ? "Your photo appeal was approved. Choose where to show the photo." : d.Decision == "appeal-upheld" ? "Your photo appeal was reviewed. The rejection is final." : PhotoReviewPolicy.MemberReason(d.Code), d.At,
                available && latest.GetValueOrDefault(d.MediaId) == d.Id && appeal is null && PhotoReviewPolicy.Appealable(d.Decision, d.Code, d.At, now), appeal?.Id, appeal?.Outcome); }).ToList());
    }
    private static async Task<IResult> AppealAsync(Guid id, PhotoAppealRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token)
    {
        var (current, denied) = await MemberAsync(user, accounts, token, barredExempt: true); if (denied is not null) return denied;
        var db = accounts.Db!; var me = current!.Account!.Id; var now = clock.GetUtcNow();
        if (request.Text?.Trim() is not { Length: > 0 and <= 1000 } text) return Problem(400, "invalid-photo-appeal", "Explain your appeal in up to 1000 characters.");
        var decision = await db.Set<MediaDecisionRecord>().SingleOrDefaultAsync(d => d.Id == id && d.AccountId == me, token);
        if (decision is null) return Results.NotFound();
        await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, me, token);
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, me, token);
        now = clock.GetUtcNow();
        var item = await db.MediaItems.SingleOrDefaultAsync(i => i.Id == decision.MediaId && i.AccountId == me, token);
        if (item is not { Status: MediaStatus.Rejected, RemovedAt: null, ObjectKey: not null } || item.RejectedRetainUntil is null || item.RejectedRetainUntil <= now || !PhotoReviewPolicy.Appealable(decision.Decision, decision.Code, decision.At, now)) return Problem(409, "photo-not-appealable", "That decision is not eligible for an appeal.");
        if (await LatestDecisionAsync(db, decision.MediaId, token) != id) return Problem(409, "photo-decision-superseded", "Only the current photo rejection can be appealed.");
        if (await db.Set<MediaAppeal>().AnyAsync(a => a.DecisionId == id, token)) return Problem(409, "photo-appeal-exists", "Only one appeal can be submitted for a decision.");
        var appeal = new MediaAppeal { Id = Guid.NewGuid(), DecisionId = id, MediaId = item.Id, AccountId = me, Text = text, SubmittedAt = now }; db.Add(appeal);
        Audit.Record(db, now, me, "account", "media.appealed", "media", item.Id.ToString()); await db.SaveChangesAsync(token); return Results.Created("/api/me/photos/decisions", new { appeal.Id });
    }
    private static async Task<IResult> ResolveAsync(Guid id, PhotoAppealResolution request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token)
    {
        var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, token); if (denied is not null) return denied;
        var db = accounts.Db!; var actor = current!.Account!.Id; var now = clock.GetUtcNow();
        if (await AckUntilAsync(db, actor, now, token) is null) return AckRequired();
        var appeal = await db.Set<MediaAppeal>().SingleOrDefaultAsync(a => a.Id == id, token); if (appeal is null) return Results.NotFound();
        await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, appeal.AccountId, token);
        await using var lease = await AccountLifecycleLease.AcquireMediaAsync(db, appeal.AccountId, token); await db.Entry(appeal).ReloadAsync(token);
        now = clock.GetUtcNow();
        if (await AckUntilAsync(db, actor, now, token) is null) return AckRequired();
        if (!await db.Accounts.AnyAsync(a => a.Id == appeal.AccountId && a.Status == AccountStatus.Active, token)) return Results.NotFound();
        var decision = await db.Set<MediaDecisionRecord>().SingleAsync(d => d.Id == appeal.DecisionId, token);
        if (appeal.DecidedAt is not null) return Problem(409, "photo-appeal-final", "The appeal already has its final outcome.");
        if (decision.ActorId == actor || await StaffAccessEndpoints.ExcludedAsync(db, actor, [appeal.AccountId], token)) return Results.Forbid();
        if (request.Outcome is not ("approve" or "uphold") || request.Reason?.Trim() is not { Length: > 0 and <= 500 } reason) return Problem(400, "invalid-photo-outcome", "Choose an outcome and record a reconsideration reason.");
        var item = await db.MediaItems.SingleOrDefaultAsync(i => i.Id == appeal.MediaId, token); if (item is null || item.Status != MediaStatus.Rejected) return Problem(409, "photo-state-changed", "The photo decision has changed.");
        if (await LatestDecisionAsync(db, item.Id, token) != decision.Id) return Problem(409, "photo-state-changed", "A later photo decision must be reviewed.");
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        if (request.Outcome == "approve" && !await accounts.Services.GetRequiredService<MediaPipeline>().ReconsiderAsync(item, actor, reason, now, token, notifyOutcome: false))
        {
            await db.Entry(item).ReloadAsync(token);
            if (item.Status == MediaStatus.Blocked)
            { FinalizeUnavailableAppeal(db, appeal, item, actor, clock.GetUtcNow()); await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return Results.Ok(new { appeal.Id, appeal.Outcome }); }
            await transaction.CommitAsync(token); return Problem(409, "photo-rescreen-not-clear", "Fresh screening did not clear the photo. Review the current decision.");
        }
        now = clock.GetUtcNow();
        (appeal.DecidedAt, appeal.DecidedBy, appeal.Outcome) = (now, actor, request.Outcome == "approve" ? "approved" : "upheld");
        Stage(db, item, actor, request.Outcome == "approve" ? "appeal-approved" : "appeal-upheld", "APPEAL_FINAL", reason, now);
        Audit.Record(db, now, actor, "account", "media.appeal-decided", "media-appeal", id.ToString(), appeal.Outcome); await db.SaveChangesAsync(token); await transaction.CommitAsync(token); return Results.Ok(new { appeal.Id, appeal.Outcome });
    }
    private static async Task<IResult> ReconsiderAsync(Guid id, PhotoAppealResolution request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token)
    {
        var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, token); if (denied is not null) return denied;
        var db = accounts.Db!; var actor = current!.Account!.Id; var now = clock.GetUtcNow();
        if (await AckUntilAsync(db, actor, now, token) is null) return AckRequired();
        var item = await Eligible(db, actor).SingleOrDefaultAsync(i => i.Id == id && i.Status == MediaStatus.Rejected, token); if (item is null || item.ReviewedBy == actor) return Results.Forbid();
        await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, item.AccountId, token);
        now = clock.GetUtcNow(); if (await AckUntilAsync(db, actor, now, token) is null) return AckRequired();
        if (await db.Set<MediaAppeal>().AnyAsync(a => a.MediaId == id, token)) return Problem(409, "photo-appeal-final", "Use the photo appeal workflow; a decided appeal is final.");
        if (request.Reason?.Trim() is not { Length: > 0 and <= 500 } reason) return Problem(400, "reason-required", "Record a reconsideration reason.");
        return await accounts.Services.GetRequiredService<MediaPipeline>().ReconsiderAsync(item, actor, reason, now, token) ? Results.Ok(new { item.Id }) : Problem(409, "photo-rescreen-not-clear", "Fresh screening did not clear the photo.");
    }
}
