using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Staff-only account administration. Every route needs the staff area (Entra SSO, or the
/// root admin with password + OTP) from application records, never from a token claim.
/// </summary>
public static class AdminEndpoints
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();

        admin.MapPost("/invitations", async (InvitationRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            if (!Enum.TryParse<AccountRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role)
                || !InvitationPolicy.IsValidRequest(role, request.Email, request.VenueId))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Invitation needs an email, an invitable role and a venue only for venue partners.");
            var now = clock.GetUtcNow();
            var invitation = new Invitation
            {
                Id = Guid.NewGuid(), Realm = InvitationPolicy.RealmFor(role), Email = EmailAddress.Normalise(request.Email)!,
                Role = role, VenueId = request.VenueId, CreatedAt = now, CreatedBy = current!.Account!.Id,
                ExpiresAt = now + InvitationPolicy.Lifetime,
            };
            var db = accounts.Db!;
            db.Invitations.Add(invitation);
            Audit.Record(db, now, current.Account.Id, "account", "invitation.created", "invitation", invitation.Id.ToString(), role.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/admin/invitations/{invitation.Id}", View(invitation, now));
        });

        admin.MapGet("/invitations", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            var list = await accounts.Db!.Invitations.OrderByDescending(i => i.CreatedAt).Take(200).ToListAsync(cancellationToken);
            return Results.Ok(list.Select(i => View(i, now)).ToList());
        });

        admin.MapDelete("/invitations/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var invitation = await db.Invitations.FindAsync([id], cancellationToken);
            if (invitation is null) return Results.NotFound();
            if (invitation.RevokedAt is null && invitation.AcceptedAt is null)
            {
                var now = clock.GetUtcNow();
                invitation.RevokedAt = now;
                Audit.Record(db, now, current!.Account!.Id, "account", "invitation.revoked", "invitation", id.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.NoContent();
        });

        // Private beta enrolment (D62): admins add, list and revoke pre-authorised addresses.
        admin.MapPost("/beta", async (BetaEnrolmentRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var (result, enrolment) = await BetaEnrolments.AddAsync(accounts.Db!, request.Email, current!.Account!.Id, clock.GetUtcNow(), cancellationToken);
            return result switch
            {
                BetaEnrolments.AddResult.InvalidEmail => Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Enrolment needs a full email address."),
                BetaEnrolments.AddResult.AlreadyEnrolled => Results.Ok(BetaEnrolments.View(enrolment!)),
                _ => Results.Created($"/api/v1/admin/beta/{enrolment!.Id}", BetaEnrolments.View(enrolment)),
            };
        });

        admin.MapGet("/beta", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var list = await accounts.Db!.BetaEnrolments.OrderByDescending(e => e.CreatedAt).Take(1000).ToListAsync(cancellationToken);
            return Results.Ok(list.Select(BetaEnrolments.View).ToList());
        });

        admin.MapDelete("/beta/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var enrolment = await accounts.Db!.BetaEnrolments.FindAsync([id], cancellationToken);
            if (enrolment is null) return Results.NotFound();
            await BetaEnrolments.RevokeAsync(accounts.Db, enrolment, current!.Account!.Id, clock.GetUtcNow(), cancellationToken);
            return Results.NoContent();
        });

        // Support needs to find an account from the address a member gives them. Exact match on
        // the normalised verified email only; the search is audited without the address.
        admin.MapGet("/accounts", async (string? email, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            if (EmailAddress.Normalise(email) is not { } normalised)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Search needs a full email address.");
            var db = accounts.Db!;
            var found = await db.Accounts.Where(a => a.Email == normalised)
                .GroupJoin(db.MemberProfiles, a => a.Id, p => p.AccountId, (a, p) => new { a, p })
                .SelectMany(x => x.p.DefaultIfEmpty(), (x, p) => new { x.a, p }).Take(10).ToListAsync(cancellationToken);
            Audit.Record(db, clock.GetUtcNow(), current!.Account!.Id, "account", "account.searched", null, null, $"results:{found.Count}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(found.Select(x => new AccountView(x.a.Id, Wire(x.a.Realm), Wire(x.a.Status), x.a.StatusReason,
                x.p is null ? null : Wire(x.p.AgeStatus), x.a.CreatedAt)).ToList());
        });

        // D40: support or an admin can confirm a member is an adult. This also lifts an
        // under-18 lock, so it needs a sign-in within the last 15 minutes.
        admin.MapPost("/accounts/{id:guid}/adult-override", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var now = clock.GetUtcNow();
            if (!current!.RecentlyAuthenticated(now))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.StaleSession, "Sign in again to do this.");
            var db = accounts.Db!;
            var account = await db.Accounts.FindAsync([id], cancellationToken);
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (account is null || profile is null) return Results.NotFound();
            profile.AgeStatus = AgeStatus.AdultOverride;
            profile.AgeStatusAt = now;
            if (account.Status == AccountStatus.Locked && account.StatusReason == "underage")
                (account.Status, account.StatusReason) = (AccountStatus.Active, null);
            Audit.Record(db, now, current.Account!.Id, "account", "age.adult-override", "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });
    }

    /// <summary>
    /// The caller in a staff area. Admin routes need the admin area; Support routes accept
    /// either support or admin, because an admin may do anything support can.
    /// </summary>
    public static async Task<(CurrentAccount? Current, IResult? Denied)> StaffAsync(ClaimsPrincipal user, AccountResolver accounts,
        AccountRole area, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null)
            return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        var allowed = current.Allows(AccountRole.Admin) || area == AccountRole.Support && current.Allows(AccountRole.Support);
        return allowed ? (current, null) : (null, Results.Forbid());
    }

    private static InvitationView View(Invitation i, DateTimeOffset now) => new(i.Id, i.Email, Wire(i.Realm), Wire(i.Role), i.VenueId,
        i.CreatedAt, i.ExpiresAt,
        i.RevokedAt is not null ? "revoked" : i.AcceptedAt is not null ? "accepted" : now >= i.ExpiresAt ? "expired" : "pending");
}
