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
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapPost("/invitations", async (InvitationRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            if (!Enum.TryParse<AccountRole>(request.Role, ignoreCase: true, out var role) || !Enum.IsDefined(role)
                || !InvitationPolicy.IsValidRequest(role, request.Email, request.VenueId))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Invitation needs an email, an invitable role and a venue only for venue partners.");
            // D189: only the root admin appoints a security / legal officer, so no admin can give themselves evidence access.
            if (role == AccountRole.Legal && !current!.IsRootAdmin) return Results.Forbid();
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
            return Results.Created($"/api/admin/invitations/{invitation.Id}", View(invitation, now));
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
                _ => Results.Created($"/api/admin/beta/{enrolment!.Id}", BetaEnrolments.View(enrolment)),
            };
        });

        admin.MapGet("/beta", async (ClaimsPrincipal user, AccountResolver accounts, HttpContext context, CancellationToken cancellationToken) =>
        {
            context.Response.Headers.CacheControl="no-store";
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

        admin.MapPut("/beta/{id:guid}/ios-distribution",async(Guid id,IosDistributionUpdate update,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var(current,denied)=await StaffAsync(user,accounts,AccountRole.Admin,token);if(denied is not null)return denied;
            if(!current!.RecentlyAuthenticated(clock.GetUtcNow()))return Problem(403,ProblemCodes.StaleSession,"Sign in again before recording distribution work.");
            var(result,row)=await BetaEnrolments.RecordIosAsync(accounts.Db!,id,update,current.Account!.Id,clock.GetUtcNow(),token);
            return result switch
            {
                BetaEnrolments.IosUpdateResult.Invalid=>Problem(400,ProblemCodes.InvalidRequest,"Confirm a supported staff-recorded state and current revision."),
                BetaEnrolments.IosUpdateResult.NotFound=>Results.NotFound(),
                BetaEnrolments.IosUpdateResult.Conflict=>Problem(409,ProblemCodes.RecordChanged,"This enrolment changed. Reload before recording another status."),
                _=>Results.Ok(BetaEnrolments.View(row!)),
            };
        });

        // Founding membership (D63/D65). Until verification exists (Q04), an admin grants it.
        admin.MapPost("/accounts/{id:guid}/founder", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, Founders founders, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var account = await db.Accounts.FindAsync([id], cancellationToken);
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (account is null || profile is null) return Results.NotFound();
            // D130: an admin exception still needs ID or Photo verification and the 1,000-place limit.
            if (profile.IdStatus != VerificationStatus.Verified && profile.PhotoStatus != VerificationStatus.Verified)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.FounderNotEligible, "Founding places need ID or Photo verification first.");
            var grant = await founders.GrantAsync(account, profile, current!.Account!.Id, clock.GetUtcNow(), cancellationToken);
            return grant.Outcome switch
            {
                FounderGrantOutcome.Granted => Results.Created($"/api/admin/founder-tokens?place={grant.Place}", new FounderGrantView(grant.Place!.Value, grant.EmailSent)),
                FounderGrantOutcome.AlreadyFounder => Problem(StatusCodes.Status409Conflict, ProblemCodes.AlreadyFounder, "This account is already a founding member."),
                FounderGrantOutcome.PlacesFull => Problem(StatusCodes.Status409Conflict, ProblemCodes.FounderPlacesFull, "All founding places are taken."),
                FounderGrantOutcome.NoVerifiedEmail => Problem(StatusCodes.Status422UnprocessableEntity, ProblemCodes.NoVerifiedEmail, "The account has no verified email."),
                _ => Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.NotConfigured, "Founder tokens are not configured."),
            };
        });

        admin.MapPost("/accounts/{id:guid}/founder/resend", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, Founders founders, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var account = await db.Accounts.FindAsync([id], cancellationToken);
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (account is null || profile is null) return Results.NotFound();
            var sent = await founders.ResendAsync(account, profile, clock.GetUtcNow(), cancellationToken);
            return sent is null ? Results.NotFound() : Results.Ok(new FounderGrantView(profile.FounderPlace!.Value, sent.Value));
        });

        admin.MapGet("/founder-tokens", async (int place, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var tokens = await accounts.Db!.FounderTokens.Where(t => t.Place == place).OrderBy(t => t.IssuedAt).ToListAsync(cancellationToken);
            return Results.Ok(tokens.Select(View).ToList());
        });

        // Admins can invalidate or delete a founder token (D65); neither changes a live account's status.
        admin.MapPost("/founder-tokens/{id:guid}/invalidate", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var token = await db.FounderTokens.FindAsync([id], cancellationToken);
            if (token is null) return Results.NotFound();
            if (token.Status == FounderTokenStatus.Valid)
            {
                var now = clock.GetUtcNow();
                (token.Status, token.InvalidatedAt, token.InvalidatedReason) = (FounderTokenStatus.Invalidated, now, "admin");
                Audit.Record(db, now, current!.Account!.Id, "account", "founder-token.invalidated", "founder-token", id.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.NoContent();
        });

        admin.MapDelete("/founder-tokens/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var token = await db.FounderTokens.FindAsync([id], cancellationToken);
            if (token is null) return Results.NotFound();
            // D195 (procedure §7): a legal hold blocks admin deletion too, and an unreadable hold status refuses (fail closed).
            var owner = await db.MemberProfiles.Where(p => p.FounderPlace == token.Place).Select(p => (Guid?)p.AccountId).FirstOrDefaultAsync(cancellationToken);
            if (owner is { } ownerId && await accounts.Services.GetRequiredService<Swoovr.Api.Evidence.EvidencePreservation>().HeldAsync(ownerId, cancellationToken) is not false)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.HeldByLegalHold, "A legal hold covers this account, so its records can't be deleted now.");
            db.FounderTokens.Remove(token);
            Audit.Record(db, clock.GetUtcNow(), current!.Account!.Id, "account", "founder-token.deleted", "founder-token", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
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

        // Q11 (D108): official bots are classified by an admin, never self-declared, and appear only in the modes an
        // admin authorised for each bot. Staff affiliation is a revocable credential on an ordinary member account; it
        // grants no role. All three are audited with ids and machine values only.
        admin.MapPut("/accounts/{id:guid}/bot", async (Guid id, BotClassificationRequest request, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (profile is null) return Results.NotFound();
            if (profile.Bot != request.Bot)
            {
                var now = clock.GetUtcNow();
                profile.Bot = request.Bot;
                // A bot is never staff; losing bot status removes every mode authorisation and market assignment (D125).
                if (request.Bot && profile.Employee)
                {
                    profile.Employee = false;
                    Audit.Record(db, now, current!.Account!.Id, "account", "staff.revoked", "account", id.ToString(), "bot");
                }
                if (!request.Bot)
                {
                    db.BotModeAuthorisations.RemoveRange(await db.BotModeAuthorisations.Where(b => b.AccountId == id).ToListAsync(cancellationToken));
                    db.BotMarkets.RemoveRange(await db.BotMarkets.Where(b => b.AccountId == id).ToListAsync(cancellationToken));
                }
                Audit.Record(db, now, current!.Account!.Id, "account", request.Bot ? "bot.classified" : "bot.declassified", "account", id.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.NoContent();
        });

        admin.MapGet("/accounts/{id:guid}/bot-modes", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is not { Bot: true })
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotABot, "That account isn't an official bot.");
            return Results.Ok(new BotModesView(await db.BotModeAuthorisations.Where(b => b.AccountId == id).OrderBy(b => b.ModeId).Select(b => b.ModeId).ToListAsync(cancellationToken)));
        });

        admin.MapPut("/accounts/{id:guid}/bot-modes", async (Guid id, BotModesRequest request, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is not { Bot: true })
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotABot, "That account isn't an official bot.");
            var known = await db.ConnectionModes.Select(m => m.Id).ToListAsync(cancellationToken);
            var wanted = (request.Modes ?? []).Distinct().ToList();
            if (wanted.Count > known.Count || wanted.Any(m => !known.Contains(m)))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownMode, "Authorise only the four connection modes.");
            var now = clock.GetUtcNow();
            var existing = await db.BotModeAuthorisations.Where(b => b.AccountId == id).ToListAsync(cancellationToken);
            db.BotModeAuthorisations.RemoveRange(existing.Where(b => !wanted.Contains(b.ModeId)));
            db.BotModeAuthorisations.AddRange(wanted.Where(m => existing.All(b => b.ModeId != m))
                .Select(m => new BotModeAuthorisation { AccountId = id, ModeId = m, AuthorisedAt = now, AuthorisedBy = current!.Account!.Id }));
            Audit.Record(db, now, current!.Account!.Id, "account", "bot.modes-authorised", "account", id.ToString(), string.Join(",", wanted.Order()));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new BotModesView(wanted.Order().ToList()));
        });

        admin.MapPut("/accounts/{id:guid}/staff-affiliation", async (Guid id, StaffAffiliationRequest request, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (profile is null) return Results.NotFound();
            if (request.Active && profile.Bot)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "An official bot can't hold a staff affiliation.");
            if (profile.Employee != request.Active)
            {
                profile.Employee = request.Active;
                // Revoking the badge leaves the member account and every other credential as they were (Q11 rule 44).
                Audit.Record(db, clock.GetUtcNow(), current!.Account!.Id, "account", request.Active ? "staff.affiliated" : "staff.revoked", "account", id.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
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

    private static FounderTokenView View(FounderToken t) => new(t.Id, t.Place, t.SecretVersion, Wire(t.Status), t.IssuedAt, t.InvalidatedAt, t.InvalidatedReason);

    private static InvitationView View(Invitation i, DateTimeOffset now) => new(i.Id, i.Email, Wire(i.Realm), Wire(i.Role), i.VenueId,
        i.CreatedAt, i.ExpiresAt,
        i.RevokedAt is not null ? "revoked" : i.AcceptedAt is not null ? "accepted" : now >= i.ExpiresAt ? "expired" : "pending");
}
