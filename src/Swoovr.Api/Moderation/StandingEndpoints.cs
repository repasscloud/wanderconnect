using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Moderation;

/// <summary>
/// Account standing (#50): one view of the member's verification, what they must do, sanctions in force, decisions about
/// them and their own reports with support references. The member reads it at any time (even restricted or suspended);
/// support and admins read exactly the same view to help them, and look a reference up. Built only from the member's own
/// records: reports about them, reporters, case notes, internal flags, ban matching, holds and fraud signals never appear.
/// </summary>
public static class StandingEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/me/standing", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true, phoneExempt: true);
            if (denied is not null) return denied;
            return Results.Ok((await StandingAsync(accounts.Db!, current!.Profile!, current.Facts.EmailVerified, accounts.PhoneRequired, clock.GetUtcNow(), cancellationToken))
                with { EmailChangeProblem = current.EmailChangeProblem });
        }).RequireAuthorization();

        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.MapGet("/accounts/{id:guid}/standing", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.SingleOrDefaultAsync(p => p.AccountId == id, cancellationToken);
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id && a.Realm == AccountRealm.Members, cancellationToken);
            if (profile is null || account is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That account isn't available.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, current!.Account!.Id, [id], cancellationToken)) return StaffAccessEndpoints.Conflict();
            Audit.Record(db, clock.GetUtcNow(), current.Account.Id, "account", "standing.viewed", "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            // Members sign in only with a verified email (D33), so an address on file is a verified one.
            return Results.Ok(await StandingAsync(db, profile, account.Email is not null, accounts.PhoneRequired, clock.GetUtcNow(), cancellationToken));
        });
        admin.MapGet("/support/{reference}", async (string reference, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var normal = SupportReference.Normalise(reference);
            var found = normal is null ? null : await (from r in db.Reports join c in db.ModerationCases on r.CaseId equals c.Id
                                                       join k in db.ReportCategories on r.CategoryId equals k.Id
                                                       where r.SupportReference == normal select new { r, c, k.Serious }).SingleOrDefaultAsync(cancellationToken);
            // Admin-queue cases (staff-affiliated accounts, serious incidents) stay out of support's sight, as in the queue.
            if (found is null || found.c.Queue == CaseQueue.Admin && current!.StaffRole != AccountRole.Admin)
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownReference, "No report has that reference.");
            if (await StaffAccessEndpoints.ExcludedAsync(db, current!.Account!.Id, [found.r.ReporterAccountId, found.r.SubjectAccountId], cancellationToken))
                return StaffAccessEndpoints.Conflict();
            Audit.Record(db, clock.GetUtcNow(), current.Account.Id, "account", "support.lookup", "report", found.r.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new SupportLookupView(normal!, found.r.Id, found.c.Id, Wire(found.c.Status), Wire(found.c.Queue), Wire(found.r.Priority), found.r.CategoryId,
                found.Serious, found.r.CreatedAt, found.r.ReporterAccountId, found.r.SubjectAccountId));
        });
    }

    public static async Task<AccountStandingView> StandingAsync(SwoovrDbContext db, MemberProfile profile, bool emailVerified, bool phoneRequired, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var me = profile.AccountId;
        var phone = await db.MemberPhones.AnyAsync(p => p.AccountId == me, cancellationToken);
        var verification = new StandingVerification(emailVerified, phone, phoneRequired, Wire(profile.AgeStatus), profile.DateOfBirth is not null,
            profile.DateOfBirthVerified, Wire(profile.DobCheck), Wire(profile.IdStatus));
        var actions = StandingPolicy.ActionsRequired(new StandingFacts(phoneRequired, phone, profile.AgeStatus, profile.DateOfBirth is not null, profile.DobCheck))
            .Select(code => new StandingAction(code, code switch
            {
                StandingPolicy.Phone => "/account?phone=required",
                StandingPolicy.AgeCheck => "/me/verification",
                StandingPolicy.DobCorrection => "/me/verification",
                _ => "/account",
            })).ToList();

        var sanctions = await db.Sanctions.Where(s => s.AccountId == me).OrderByDescending(s => s.StartsAt).Take(100).ToListAsync(cancellationToken);
        var ids = sanctions.Select(s => s.Id).ToList();
        var appeals = await db.Appeals.Where(a => ids.Contains(a.SanctionId)).ToDictionaryAsync(a => a.SanctionId, cancellationToken);
        var enforcement = Enforcement.For(sanctions, now);
        var inForce = sanctions.Where(s => Enforcement.Live(s, now) && s.Kind != SanctionKind.Warning)
            .Select(s => new StandingSanction(s.Id, Wire(s.Kind), s.Reason, s.StartsAt, s.EndsAt, StandingPolicy.AppealBy(s, appeals.ContainsKey(s.Id), now),
                appeals.TryGetValue(s.Id, out var a) ? Wire(a.Status) : null)).ToList();

        var decisions = sanctions.Select(s => new StandingDecision(Wire(s.Kind), s.StartsAt, StandingPolicy.SanctionState(s, now), s.Id,
            appeals.TryGetValue(s.Id, out var a) ? Wire(a.Status) : null, StandingPolicy.AppealBy(s, appeals.ContainsKey(s.Id), now))).ToList();
        // D166: the outcome of a manual date-of-birth review, never the reviewer's reason.
        var reviews = await db.DobReviews.Where(r => r.AccountId == me && r.Outcome != null && r.ResolvedAt != null).ToListAsync(cancellationToken);
        decisions.AddRange(reviews.Select(r => new StandingDecision(r.Outcome == DobReviewOutcome.Verified ? "dob-verified" : "dob-not-verified", r.ResolvedAt!.Value,
            null, null, null, null)));
        // D203: a photo that wasn't approved, dated by its upload; a blocked one reads exactly like a rejection, with no
        // decision time that could tell an automatic match from a person's review (procedure §4).
        var refused = await db.MediaItems.Where(i => i.AccountId == me && (i.Status == MediaStatus.Rejected || i.Status == MediaStatus.Blocked))
            .OrderByDescending(i => i.CreatedAt).Take(50).Select(i => i.CreatedAt).ToListAsync(cancellationToken);
        decisions.AddRange(refused.Select(at => new StandingDecision("photo-not-approved", at, null, null, null, null)));

        var mine = await (from r in db.Reports join c in db.ModerationCases on r.CaseId equals c.Id join k in db.ReportCategories on r.CategoryId equals k.Id
                          where r.ReporterAccountId == me orderby r.CreatedAt descending
                          select new { Report = r, c.Status, k.Serious, Action = db.Sanctions.Any(s => s.CaseId == c.Id && s.StartsAt >= r.CreatedAt) })
            .Take(100).ToListAsync(cancellationToken);
        // Older reports get their reference the first time the member sees them.
        foreach (var row in mine.Where(x => x.Report.SupportReference is null)) row.Report.SupportReference = SupportReference.New();
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
        var reports = mine.Select(x => new StandingReport(x.Report.Id, x.Report.SupportReference!, Wire(x.Report.SubjectType), x.Report.CategoryId,
            ReportPolicy.ReporterStatus(x.Status), ReportPolicy.Outcome(x.Status, x.Action), x.Report.CreatedAt, x.Serious)).ToList();

        return new(enforcement.State, verification, actions, inForce, decisions.OrderByDescending(d => d.At).ToList(), reports);
    }
}
