using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Account detail controls (#190, D322, D323): what the caller may do on one account, sign-in locks, sign out everywhere and
/// staff overrides of Photo Verified / ID Verified. Every endpoint authorises on its own; the permissions view only drives
/// which controls the admin app enables.
/// </summary>
public static class AccountControlEndpoints
{
    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        admin.MapGet("/accounts/{id:guid}/permissions", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var current = await accounts.ResolveAsync(user, token);
            if (current?.Account is null) return Results.Unauthorized();
            var role = ActingRole(current);
            if (role == AccountRole.Unknown) return Results.Forbid();
            var db = accounts.Db!;
            if (await db.Accounts.FindAsync([id], token) is null) return Results.NotFound();
            var r = ModerationPolicy.Rank(role);
            return Results.Ok(new AccountPermissionsView(Wire(role),
                Warn: r >= 1, Restrict: r >= 1, MaxRestrictionDays: r >= 1 ? (int)ModerationPolicy.MaxRestrictionFor(role).TotalDays : 0,
                Suspend: ModerationPolicy.MayApply(role, SanctionKind.Suspension), Terminate: ModerationPolicy.MayApply(role, SanctionKind.Termination),
                Verify: role == AccountRole.Admin, Membership: role == AccountRole.Admin, StaffBadge: role == AccountRole.Admin,
                Lock: AccountLockPolicy.MayLock(role, "administrative"), LegalLock: AccountLockPolicy.MayLock(role, "legal-restriction"),
                InspectMedia: current.Allows(AccountRole.Admin), Conversations: r >= 1 || role == AccountRole.Legal, EditLocation: role == AccountRole.Admin));
        });

        admin.MapGet("/accounts/{id:guid}/locks", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await LockStaffAsync(user, accounts, token);
            if (denied is not null) return denied;
            if (await StaffAccessEndpoints.ExcludedAsync(accounts.Db!, current!.Account!.Id, [id], token)) return StaffAccessEndpoints.Conflict();
            var now = clock.GetUtcNow();
            var rows = await accounts.Db!.AccountLocks.Where(l => l.AccountId == id).OrderByDescending(l => l.CreatedAt).Take(100).ToListAsync(token);
            return Results.Ok(rows.Select(l => View(l, now)).ToList());
        });

        admin.MapPost("/accounts/{id:guid}/locks", async (Guid id, AccountLockRequest request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            IIdentityAdmin identity, Swoovr.Api.Connections.MessagesConnections connections, CancellationToken token) =>
        {
            var (current, denied) = await LockStaffAsync(user, accounts, token);
            if (denied is not null) return denied;
            var role = ActingRole(current!);
            if (!AccountReasons.Valid(AccountReasons.Lock, request.ReasonCode) || request.Days is < 1 or > 365 || request.Note is { Length: > AccountLockPolicy.NoteMax })
                return Problem(400, ProblemCodes.InvalidRequest, "Choose a listed reason and an expiry of 1 to 365 days, or none.");
            if (!AccountLockPolicy.MayLock(role, request.ReasonCode)) return Problem(403, ProblemCodes.ModerationNotPermitted, "Your role can't place this lock.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var account = await db.Accounts.FindAsync([id], token);
            if (account is not { Realm: AccountRealm.Members }) return Results.NotFound();
            if (await StaffAccessEndpoints.ExcludedAsync(db, current!.Account!.Id, [id], token)) return StaffAccessEndpoints.Conflict();
            if (await db.AccountLocks.AnyAsync(l => l.AccountId == id && l.RemovedAt == null && (l.ExpiresAt == null || l.ExpiresAt > now), token))
                return Problem(409, ProblemCodes.InvalidRequest, "This account is already locked.");
            var reference = SupportReference.Normalise(request.CaseReference) ?? SupportReference.New();
            var item = new AccountLock
            {
                Id = Guid.NewGuid(), AccountId = id, ReasonCode = request.ReasonCode, CaseReference = reference,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), CreatedAt = now, CreatedBy = current.Account.Id,
                CreatedByRole = role, ExpiresAt = request.Days is { } days ? now.AddDays(days) : null,
            };
            db.AccountLocks.Add(item);
            // The lock gate holds while the lock is active; the cutoff also retires every existing (possibly stolen) token so it
            // can't resume when the lock is removed or expires.
            RetireSessions(account, now);
            Audit.Record(db, now, current.Account.Id, "account", "account.locked", "account", id.ToString(), $"{role}:{request.ReasonCode}:{reference}");
            await db.SaveChangesAsync(token);
            // #198: open messaging connections stop receiving at once, not when they next reconnect.
            connections.End(id);
            // The API gate refuses every request at once; ending Keycloak sessions also stops silent refreshes on every device.
            var signedOut = identity.Configured && await identity.SignOutEverywhereAsync(account.Subject, token);
            if (signedOut) { Audit.Record(db, now, current.Account.Id, "account", "sessions.revoked", "account", id.ToString(), "lock"); await db.SaveChangesAsync(token); }
            return Results.Ok(View(item, now));
        });

        admin.MapPost("/account-locks/{lockId:guid}/remove", async (Guid lockId, AccountUnlockRequest request, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await LockStaffAsync(user, accounts, token);
            if (denied is not null) return denied;
            var role = ActingRole(current!);
            if (!AccountReasons.Valid(AccountReasons.Unlock, request.ReasonCode)) return Problem(400, ProblemCodes.InvalidRequest, "Choose a listed reason.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var item = await db.AccountLocks.FindAsync([lockId], token);
            if (item is null) return Results.NotFound();
            // The same retained conflicts as placing a lock: no one removes a lock on their own or a linked/excluded account.
            if (await StaffAccessEndpoints.ExcludedAsync(db, current!.Account!.Id, [item.AccountId], token)) return StaffAccessEndpoints.Conflict();
            if (!AccountLockPolicy.Active(item, now)) return Problem(409, ProblemCodes.InvalidRequest, "This lock has already ended.");
            if (!AccountLockPolicy.MayUnlock(role, item)) return Problem(403, ProblemCodes.ModerationNotPermitted, "A higher role placed this lock.");
            (item.RemovedAt, item.RemovedBy, item.RemovedReason) = (now, current.Account.Id, request.ReasonCode);
            Audit.Record(db, now, current.Account.Id, "account", "account.unlocked", "account", item.AccountId.ToString(), $"{role}:{request.ReasonCode}");
            await db.SaveChangesAsync(token);
            return Results.Ok(View(item, now));
        });

        admin.MapPost("/accounts/{id:guid}/sign-out", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, IIdentityAdmin identity,
            Swoovr.Api.Connections.MessagesConnections connections, CancellationToken token) =>
        {
            var (current, denied) = await LockStaffAsync(user, accounts, token);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var account = await db.Accounts.FindAsync([id], token);
            if (account is not { Realm: AccountRealm.Members }) return Results.NotFound();
            if (await StaffAccessEndpoints.ExcludedAsync(db, current!.Account!.Id, [id], token)) return StaffAccessEndpoints.Conflict();
            if (!identity.Configured) return Problem(503, ProblemCodes.InvalidRequest, "Session sign-out isn't available.");
            // The API validates tokens locally, so the application cutoff is what retires existing tokens at once; it is saved
            // before the Keycloak logout (which stops refreshes) so a provider failure never leaves old tokens usable.
            var now = clock.GetUtcNow();
            RetireSessions(account, now);
            Audit.Record(db, now, current.Account.Id, "account", "sessions.revoked", "account", id.ToString(), ActingRole(current).ToString());
            await db.SaveChangesAsync(token);
            // #198: an already-open messaging connection from before the cutoff closes now (on this instance; others sweep).
            connections.Retire(id, account.SessionsRevokedAt, now);
            if (!await identity.SignOutEverywhereAsync(account.Subject, token)) return Problem(502, ProblemCodes.InvalidRequest, "Sign-out didn't complete. Try again.");
            return Results.NoContent();
        });

        admin.MapGet("/accounts/{id:guid}/contact", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var (_, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var account = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id, token);
            if (account is null) return Results.NotFound();
            var phone = await db.MemberPhones.AsNoTracking().SingleOrDefaultAsync(p => p.AccountId == id, token);
            return Results.Ok(new AccountContactView(MaskEmail(account.Email), MaskPhone(phone?.Number), phone?.VerifiedAt, account.CreatedAt, account.LastSeenAt));
        });

        admin.MapGet("/accounts/{id:guid}/verification-state", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken token) =>
        {
            var (_, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, token);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var profile = await db.MemberProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.AccountId == id, token);
            if (profile is null) return Results.NotFound();
            var history = await db.VerificationOverrides.Where(o => o.AccountId == id).OrderByDescending(o => o.At).Take(100).ToListAsync(token);
            return Results.Ok(new VerificationStateView(Wire(profile.PhotoStatus), profile.PhotoManual, profile.PhotoCheckedAt, Wire(profile.IdStatus),
                profile.IdManual, profile.IdCheckedAt, history.Select(o => new VerificationOverrideView(o.Id, Wire(o.Badge), o.Applied, o.ReasonCode, o.At, Wire(o.ByRole))).ToList()));
        });

        // #190 §3: manual Photo Verified / ID Verified. Admin only, fresh sign-in, a set reason; never a provider result, and a
        // manual ID Verified never verifies the date of birth. Revoking keeps every attempt and identity record.
        admin.MapPost("/accounts/{id:guid}/verification-override", async (Guid id, VerificationOverrideRequest request, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken token) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, token);
            if (denied is not null) return denied;
            var badge = request.Badge switch { "photoVerified" => VerificationBadge.PhotoVerified, "idVerified" => VerificationBadge.IdVerified, _ => (VerificationBadge?)null };
            if (badge is null || !AccountReasons.Valid(request.Apply ? AccountReasons.VerifyApply : AccountReasons.VerifyRevoke, request.ReasonCode)
                || request.Note is { Length: > 1000 })
                return Problem(400, ProblemCodes.InvalidRequest, "Choose a badge and a listed reason.");
            var now = clock.GetUtcNow();
            if (!current!.RecentlyAuthenticated(now)) return Problem(403, ProblemCodes.StaleSession, "Sign in again to do this.");
            var db = accounts.Db!;
            await using var lifecycle = await AccountLifecycleLease.AcquireAsync(db, id, token);
            var profile = await db.MemberProfiles.FindAsync([id], token);
            if (profile is null) return Results.NotFound();
            if (await StaffAccessEndpoints.ExcludedAsync(db, current.Account!.Id, [id], token)) return StaffAccessEndpoints.Conflict();
            var target = request.Apply ? VerificationStatus.Verified : VerificationStatus.NotStarted;
            if (badge == VerificationBadge.PhotoVerified)
            {
                if ((profile.PhotoStatus == VerificationStatus.Verified) == request.Apply) return Problem(409, ProblemCodes.InvalidRequest, "Nothing to change.");
                (profile.PhotoStatus, profile.PhotoCheckedAt, profile.PhotoManual) = (target, now, request.Apply);
            }
            else
            {
                if ((profile.IdStatus == VerificationStatus.Verified) == request.Apply) return Problem(409, ProblemCodes.InvalidRequest, "Nothing to change.");
                // Matches the existing D129 revoke: a revoked ID Verified reads as rejected until the member verifies again.
                (profile.IdStatus, profile.IdCheckedAt, profile.IdManual) = (request.Apply ? VerificationStatus.Verified : VerificationStatus.Rejected, now, request.Apply);
            }
            db.VerificationOverrides.Add(new VerificationOverride
            {
                Id = Guid.NewGuid(), AccountId = id, Badge = badge.Value, Applied = request.Apply, ReasonCode = request.ReasonCode,
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(), At = now, By = current.Account.Id, ByRole = ActingRole(current),
            });
            Audit.Record(db, now, current.Account.Id, "account", $"verification.{Wire(badge.Value)}.{(request.Apply ? "applied" : "revoked")}", "account", id.ToString(), request.ReasonCode);
            await db.SaveChangesAsync(token);
            return Results.NoContent();
        });
    }

    /// <summary>The role used for account controls: Admin, then Trust & Safety, then Legal, then Support (D322).</summary>
    public static AccountRole ActingRole(CurrentAccount current) =>
        current.Allows(AccountRole.Admin) ? AccountRole.Admin : current.Allows(AccountRole.TrustSafety) ? AccountRole.TrustSafety
        : current.Allows(AccountRole.Legal) ? AccountRole.Legal : current.Allows(AccountRole.Support) ? AccountRole.Support : AccountRole.Unknown;

    private static async Task<(CurrentAccount? Current, IResult? Denied)> LockStaffAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken token)
    {
        var current = await accounts.ResolveAsync(user, token);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(503, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        return ActingRole(current) is AccountRole.Admin or AccountRole.TrustSafety or AccountRole.Legal ? (current, null) : (null, Results.Forbid());
    }

    /// <summary>Refuses every token authenticated through the issuer-skew cutoff (the same rule as staff revocation).</summary>
    private static void RetireSessions(Account account, DateTimeOffset now)
    {
        var cutoff = SessionRevocationPolicy.Cutoff(now);
        if (account.SessionsRevokedAt is null || account.SessionsRevokedAt < cutoff) account.SessionsRevokedAt = cutoff;
    }

    public static string? MaskEmail(string? email)
    {
        if (email is null || email.IndexOf('@') is var at && at < 1) return email is null ? null : "•••";
        return email[0] + new string('•', Math.Max(3, at - 1)) + email[at..];
    }
    public static string? MaskPhone(string? number) =>
        number is null ? null : number.Length <= 6 ? "•••" : number[..3] + new string('•', number.Length - 6) + number[^3..];

    private static AccountLockView View(AccountLock l, DateTimeOffset now) => new(l.Id, l.ReasonCode, l.CaseReference, l.CreatedAt, Wire(l.CreatedByRole),
        l.ExpiresAt, l.RemovedAt, l.RemovedReason, AccountLockPolicy.Active(l, now));
}
