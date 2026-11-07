using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Auth;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>#206: internal staff only. Never treats external organisation memberships as staff grants.</summary>
public static class StaffAccountEndpoints
{
    public static void Map(WebApplication app)
    {
        var staff = app.MapGroup("/api/admin/staff/accounts").RequireAuthorization();
        staff.MapGet("", async (string? search, string? role, string? status, string? mfa, DateTimeOffset? signedInAfter, DateTimeOffset? signedInBefore, int? page, ClaimsPrincipal user, AccountResolver resolver, IStaffIdentityAdmin identity, CancellationToken ct) =>
        {
            var (actor, denied) = await AuthorizeAsync(user, resolver, false, ct);
            if (denied is not null) return denied;
            var db = resolver.Db!;
            var rows = db.Accounts.AsNoTracking().Where(a => a.Realm == AccountRealm.Staff);
            if (!string.IsNullOrWhiteSpace(search))
            {
                if (search.Length > 100) return Problem(400, ProblemCodes.InvalidRequest, "Search is too long.");
                var term = search.Trim().ToLowerInvariant();
                rows = rows.Where(a => a.Email != null && a.Email.ToLower().Contains(term) || a.RegisteredFirstName != null && a.RegisteredFirstName.ToLower().Contains(term)
                    || a.RegisteredLastName != null && a.RegisteredLastName.ToLower().Contains(term));
            }
            if (!string.IsNullOrEmpty(role))
            {
                if (!Enum.TryParse<AccountRole>(role, true, out var parsed) || !StaffManagementPolicy.InternalRole(parsed)) return Problem(400, ProblemCodes.InvalidRequest, "Choose an internal staff role.");
                rows = rows.Where(a => parsed == AccountRole.Admin && a.IsRootAdmin || db.RoleGrants.Any(g => g.AccountId == a.Id && g.Role == parsed));
            }
            if (!string.IsNullOrEmpty(status))
            {
                if (!Enum.TryParse<AccountStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed)) return Problem(400, ProblemCodes.InvalidRequest, "Choose an account status.");
                rows = rows.Where(a => a.Status == parsed);
            }
            if (signedInAfter is { } after) rows = rows.Where(a => a.StaffLastSignInAt >= after);
            if (signedInBefore is { } before) rows = rows.Where(a => a.StaffLastSignInAt <= before);
            if (!string.IsNullOrEmpty(mfa) && mfa is not ("enabled" or "setup-pending" or "disabled")) return Invalid();
            var number = Math.Max(1, page ?? 1); if (number > 10000) return Problem(400, ProblemCodes.InvalidRequest, "Page is out of range.");
            List<Account> accounts;
            var snapshots = new Dictionary<Guid, IdentityAccountInfo?>();
            var total = await rows.CountAsync(ct);
            if (!string.IsNullOrEmpty(mfa))
            {
                // Provider credential state is never guessed from application roles or cached as authentication evidence.
                if (total > 200) return Problem(400, ProblemCodes.InvalidRequest, "Narrow the staff search before filtering MFA.");
                var candidates = await rows.OrderBy(a => a.Id).ToListAsync(ct);
                foreach (var candidate in candidates)
                {
                    var info = await identity.AccountAsync(candidate.Subject, ct);
                    if (info is null) return ProviderUnavailable();
                    snapshots[candidate.Id] = info;
                }
                var filtered = candidates.Where(a => Mfa(snapshots[a.Id]) == mfa).ToList(); total = filtered.Count;
                accounts = filtered.Skip((number - 1) * 25).Take(25).ToList();
            }
            else accounts = await rows.OrderBy(a => a.RegisteredFirstName).ThenBy(a => a.Id).Skip((number - 1) * 25).Take(25).ToListAsync(ct);
            var ids = accounts.Select(a => a.Id).ToList();
            var roles = await db.RoleGrants.AsNoTracking().Where(g => ids.Contains(g.AccountId)).ToListAsync(ct);
            return Results.Ok(new StaffAccountPage(accounts.Select(a => Summary(a, roles.Where(g => g.AccountId == a.Id).Select(g => g.Role))
                with { Mfa = snapshots.TryGetValue(a.Id, out var info) ? Mfa(info) : "unknown" }).ToList(), total, number, 25));
        });
        staff.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver resolver, IStaffIdentityAdmin identity, CancellationToken ct) =>
        {
            var (actor, denied) = await AuthorizeAsync(user, resolver, false, ct); if (denied is not null) return denied;
            return await DetailAsync(id, actor!, resolver, identity, ct);
        });
        staff.MapPost("", CreateAsync);
        staff.MapPut("/{id:guid}", UpdateAsync);
        staff.MapPost("/{id:guid}/security", SecurityAsync);
    }

    public static async Task<(CurrentAccount? Actor, IResult? Denied)> AuthorizeAsync(ClaimsPrincipal user, AccountResolver resolver, bool mutation, CancellationToken ct)
    {
        var current = await resolver.ResolveAsync(user, ct);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null || resolver.Db is null) return (null, Problem(503, ProblemCodes.PersistenceUnavailable, "Accounts are unavailable."));
        // Re-resolving a staff account on this context must not retain a pre-lock status snapshot.
        await resolver.Db.Entry(current.Account).ReloadAsync(ct);
        if (SessionRevocationPolicy.Refuses(current.Account.SessionsRevokedAt, current.Facts.AuthenticatedAt, user.FindFirstValue("iat"), resolver.Services.GetRequiredService<TimeProvider>().GetUtcNow()))
            return (null, Results.Unauthorized());
        if (!current.Allows(AccountRole.Admin)) return (null, Results.Forbid());
        if (mutation && (!current.Evidence.MultiFactor || !current.RecentlyAuthenticated(resolver.Services.GetRequiredService<TimeProvider>().GetUtcNow())))
            return (null, Problem(403, ProblemCodes.StaleSession, "Sign in again with MFA before managing staff access."));
        return (current, null);
    }

    public static async Task<IResult> CreateAsync(StaffAccountCreate request, ClaimsPrincipal user, AccountResolver resolver, IStaffIdentityAdmin identity, KeycloakOptions keycloak,
        TimeProvider clock, Ticketing.TicketingStaffSignal helpdesk, CancellationToken ct)
    {
        var (actor, denied) = await AuthorizeAsync(user, resolver, true, ct); if (denied is not null) return denied;
        if (!Valid(request.FirstName, request.LastName, request.Email, request.Phone, request.Reason, request.Confirm) || !Roles(request.Roles, out var roles) || roles.Count == 0)
            return Invalid();
        if (roles.Contains(AccountRole.Legal) && !actor!.IsRootAdmin) return Results.Forbid();
        if (!identity.Configured) return ProviderUnavailable();
        var db = resolver.Db!; var email = EmailAddress.Normalise(request.Email)!; var id = Guid.NewGuid();
        await using var authority = await AccountLifecycleLease.AcquireManyAsync(db, [actor!.Account!.Id, id], ct);
        // Serialize same-address creation without assuming email proves an existing user's identity.
        await using var registration = await AccountLifecycleLease.AcquireRegistrationAsync(db, keycloak.Issuer(keycloak.StaffRealm), email, ct);
        (actor, denied) = await AuthorizeAsync(user, resolver, true, ct); if (denied is not null) return denied;
        if (roles.Contains(AccountRole.Legal) && !actor!.IsRootAdmin) return Results.Forbid();
        if (await db.Accounts.AnyAsync(a => a.Realm == AccountRealm.Staff && a.Email == email, ct)) return Problem(409, ProblemCodes.RecordChanged, "A staff account already uses that verified address.");
        var update = new IdentityAccountUpdate(request.FirstName.Trim(), request.LastName.Trim(), email, request.Phone?.Trim(), false);
        var subject = await identity.CreateAccountAsync(update, ct);
        if (subject is null) return ProviderUnavailable(); // A lost response never grants authority by guessed email.
        var now = clock.GetUtcNow();
        var account = new Account { Id = id, Issuer = keycloak.Issuer(keycloak.StaffRealm), Subject = subject, Realm = AccountRealm.Staff,
            RegisteredFirstName = update.FirstName, RegisteredLastName = update.LastName, Status = AccountStatus.Locked, StatusReason = "staff-provisioning", CreatedAt = now, LastSeenAt = now, StaffRevision = 1 };
        await using (var preservation = await Retention.PreservationLease.SharedAsync(db, ct))
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            db.Accounts.Add(account);
            db.RoleGrants.AddRange(roles.Select(r => new RoleGrant { Id = Guid.NewGuid(), AccountId = id, Role = r, GrantedBy = actor!.Account!.Id, GrantedAt = now }));
            Audit.Record(db, now, actor!.Account!.Id, "account", "staff.created", "account", id.ToString(), Reason(request.Reason));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        // Keep application access locked until provider activation is confirmed. A retry uses the existing management page.
        var activated = await identity.UpdateAccountAsync(subject, update with { Enabled = true }, ct);
        account.Status = activated ? AccountStatus.Active : AccountStatus.Locked; account.StatusReason = activated ? null : "staff-provisioning";
        Audit.Record(db, clock.GetUtcNow(), actor!.Account!.Id, "account", activated ? "staff.provider-activated" : "staff.provider-activation-unconfirmed", "account", id.ToString());
        await db.SaveChangesAsync(ct); helpdesk.Nudge();
        return Results.Created($"/api/admin/staff/accounts/{id}", Summary(account, roles));
    }

    public static async Task<IResult> UpdateAsync(Guid id, StaffAccountUpdate request, ClaimsPrincipal user, AccountResolver resolver, IStaffIdentityAdmin identity,
        TimeProvider clock, Ticketing.TicketingStaffSignal helpdesk, CancellationToken ct)
    {
        var (actor, denied) = await AuthorizeAsync(user, resolver, true, ct); if (denied is not null) return denied;
        if (!Valid(request.FirstName, request.LastName, request.Email, request.Phone, request.Reason, request.Confirm) || !Roles(request.Roles, out var roles)
            || !Enum.TryParse<AccountStatus>(request.Status, true, out var status) || status is not (AccountStatus.Active or AccountStatus.Locked or AccountStatus.Suspended)) return Invalid();
        var db = resolver.Db!;
        await using var lifecycle = await AccountLifecycleLease.AcquireManyAsync(db, [actor!.Account!.Id, id], ct);
        (actor, denied) = await AuthorizeAsync(user, resolver, true, ct); if (denied is not null) return denied;
        var target = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (target is null) return Results.NotFound(); await db.Entry(target).ReloadAsync(ct);
        var oldRoles = await db.RoleGrants.Where(r => r.AccountId == id).ToListAsync(ct);
        if (!StaffManagementPolicy.CanManage(actor!.Account!.Id, actor.IsRootAdmin, target, oldRoles.Select(g => g.Role).ToList(), roles)) return Results.Forbid();
        if (request.ExpectedRevision != target.StaffRevision) return Changed();
        if (!identity.Configured) return ProviderUnavailable();
        var now = clock.GetUtcNow();
        // Close local access before a remote write. Uncertainty must leave it closed, with the reason/audit retained.
        await using (var preservation = await Retention.PreservationLease.SharedAsync(db, ct))
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            target.Status = AccountStatus.Locked; target.StatusReason = "staff-update-pending"; target.StaffRevision++;
            target.SessionsRevokedAt = MaxCutoff(target.SessionsRevokedAt, now);
            await db.Invitations.Where(i => i.Realm == AccountRealm.Staff && (i.Email == target.Email || i.Email == EmailAddress.Normalise(request.Email)) && i.AcceptedAt == null && i.RevokedAt == null)
                .ExecuteUpdateAsync(u => u.SetProperty(i => i.RevokedAt, now), ct);
            await db.AccessGrants.Where(g => g.AccountId == id && g.RevokedAt == null).ExecuteUpdateAsync(u => u.SetProperty(g => g.RevokedAt, now).SetProperty(g => g.RevokedBy, actor.Account.Id).SetProperty(g => g.RevokedReason, "staff-revoked"), ct);
            Audit.Record(db, now, actor.Account.Id, "account", "staff.update-requested", "account", id.ToString(), Reason(request.Reason));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        helpdesk.Nudge();
        var update = new IdentityAccountUpdate(request.FirstName.Trim(), request.LastName.Trim(), EmailAddress.Normalise(request.Email)!, request.Phone?.Trim(), status == AccountStatus.Active);
        var ended = await identity.SignOutEverywhereAsync(target.Subject, ct);
        var changed = ended && await identity.UpdateAccountAsync(target.Subject, update, ct);
        if (!changed)
        {
            Audit.Record(db, clock.GetUtcNow(), actor.Account.Id, "account", "staff.provider-unconfirmed", "account", id.ToString()); await db.SaveChangesAsync(ct);
            return ProviderUnavailable();
        }
        await using (var preservation = await Retention.PreservationLease.SharedAsync(db, ct))
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            db.RoleGrants.RemoveRange(oldRoles);
            db.RoleGrants.AddRange(roles.Select(r => new RoleGrant { Id = Guid.NewGuid(), AccountId = id, Role = r, GrantedBy = actor.Account.Id, GrantedAt = clock.GetUtcNow() }));
            target.RegisteredFirstName = update.FirstName; target.RegisteredLastName = update.LastName;
            // Email is updated only after a verified provider token, never asserted by an administrator.
            target.Status = status; target.StatusReason = status == AccountStatus.Active ? null : "staff-managed";
            Audit.Record(db, clock.GetUtcNow(), actor.Account.Id, "account", "staff.updated", "account", id.ToString(), string.Join(',', roles.Select(Wire)));
            await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
        }
        helpdesk.Nudge(); return Results.Ok(Summary(target, roles));
    }

    public static async Task<IResult> SecurityAsync(Guid id, StaffSecurityAction request, ClaimsPrincipal user, AccountResolver resolver, IStaffIdentityAdmin identity,
        TimeProvider clock, Ticketing.TicketingStaffSignal helpdesk, CancellationToken ct)
    {
        var (actor, denied) = await AuthorizeAsync(user, resolver, true, ct); if (denied is not null) return denied;
        if (!request.Confirm || !ValidReason(request.Reason)) return Invalid();
        if (request.Action is not ("sign-out" or "require-mfa" or "password-reset-email" or "require-password-change")) return Invalid();
        var db = resolver.Db!;
        await using var authority = await AccountLifecycleLease.AcquireManyAsync(db, [actor!.Account!.Id, id], ct);
        (actor, denied) = await AuthorizeAsync(user, resolver, true, ct); if (denied is not null) return denied;
        var target = await db.Accounts.SingleOrDefaultAsync(a => a.Id == id, ct);
        if (target is null) return Results.NotFound(); await db.Entry(target).ReloadAsync(ct);
        var roles = await db.RoleGrants.Where(g => g.AccountId == id).Select(g => g.Role).ToListAsync(ct);
        if (!StaffManagementPolicy.CanManage(actor!.Account!.Id, actor.IsRootAdmin, target, roles, roles)) return Results.Forbid();
        if (target.StaffRevision != request.ExpectedRevision) return Changed();
        // Employees use Entra. No local password is created or exposed to make an unavailable flow appear functional.
        if (request.Action.StartsWith("password", StringComparison.Ordinal) || request.Action == "require-password-change")
        {
            if (!actor.LocalPasswordStaff) return Problem(409, "password-managed-externally", "Employee passwords are managed through Microsoft.");
        }
        if (!identity.Configured) return ProviderUnavailable();
        var now = clock.GetUtcNow(); target.StaffRevision++; target.SessionsRevokedAt = MaxCutoff(target.SessionsRevokedAt, now);
        Audit.Record(db, now, actor.Account.Id, "account", "staff.security-requested", "account", id.ToString(), request.Action + ";" + Reason(request.Reason));
        await db.SaveChangesAsync(ct); helpdesk.Nudge();
        var ok = await identity.SignOutEverywhereAsync(target.Subject, ct);
        if (ok) ok = request.Action switch
        {
            "require-mfa" => await identity.RequireActionsAsync(target.Subject, ["CONFIGURE_TOTP"], ct),
            "password-reset-email" => await identity.PasswordEmailAsync(target.Subject, ct),
            "require-password-change" => await identity.RequireActionsAsync(target.Subject, ["UPDATE_PASSWORD"], ct),
            _ => true,
        };
        Audit.Record(db, clock.GetUtcNow(), actor.Account.Id, "account", ok ? "staff.security-completed" : "staff.security-unconfirmed", "account", id.ToString(), request.Action);
        await db.SaveChangesAsync(ct); return ok ? Results.Ok(Summary(target, roles)) : ProviderUnavailable();
    }

    public static StaffAccountSummary Summary(Account a, IEnumerable<AccountRole> roles) => new(a.Id, a.RegisteredFirstName, a.RegisteredLastName, a.Email, Wire(a.Status),
        roles.Where(StaffManagementPolicy.InternalRole).Select(Wire).Order().ToArray(), a.LastSeenAt, a.IsRootAdmin, a.StaffRevision, a.StaffLastSignInAt);
    private static async Task<IResult> DetailAsync(Guid id, CurrentAccount actor, AccountResolver resolver, IStaffIdentityAdmin identity, CancellationToken ct)
    {
        var db = resolver.Db!;
        var target = await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a => a.Id == id && a.Realm == AccountRealm.Staff, ct);
        if (target is null) return Results.NotFound();
        var roles = await db.RoleGrants.AsNoTracking().Where(g => g.AccountId == id).Select(g => g.Role).ToListAsync(ct);
        var info = await identity.AccountAsync(target.Subject, ct);
        var sessions = info is null ? null : await identity.SignInsAsync(target.Subject, ct);
        var history = await db.AuditEntries.AsNoTracking().Where(a => a.TargetType == "account" && a.TargetId == id.ToString() && a.Action.StartsWith("staff."))
            .OrderByDescending(a => a.Id).Take(100).Select(a => new StaffActionEntry(a.Id, a.At, a.Action)).ToListAsync(ct);
        var summary = Summary(target, roles);
        if (info is not null) summary = summary with { Email = info.Email, FirstName = info.FirstName, LastName = info.LastName, Mfa = Mfa(info) };
        return Results.Ok(new StaffAccountDetail(summary, info?.Phone, info is not null, info?.Enabled == true, info?.EmailVerified == true,
            info?.CredentialTypes ?? [], info?.RequiredActions ?? [], sessions?.Sessions.Select(s => new SignInSessionView(SignInPolicy.Handle(s.Id), SignInPolicy.Device(s.Clients), s.Started, s.LastActive, false)).ToArray() ?? [], history, StaffManagementPolicy.CanManage(actor.Account!.Id, actor.IsRootAdmin, target, roles, roles)));
    }
    private static string Mfa(IdentityAccountInfo? info) => info is null ? "unknown" : info.CredentialTypes.Contains("otp") ? "enabled" : info.RequiredActions.Contains("CONFIGURE_TOTP") ? "setup-pending" : "disabled";
    public static bool Roles(IReadOnlyList<string>? values, out List<AccountRole> roles)
    {
        roles = [];
        if (values is null || values.Count > 5) return false;
        foreach (var value in values) { if (!Enum.TryParse<AccountRole>(value, true, out var role) || !StaffManagementPolicy.InternalRole(role)) return false; roles.Add(role); }
        return StaffManagementPolicy.ValidRoles(roles);
    }
    private static bool Valid(string? first, string? last, string? email, string? phone, string? reason, bool confirm) => confirm
        && first is { Length: > 0 and <= 100 } && !string.IsNullOrWhiteSpace(first) && last is { Length: > 0 and <= 100 } && !string.IsNullOrWhiteSpace(last)
        && EmailAddress.Normalise(email) is not null && (phone is null || phone.Length <= 32 && phone.All(c => char.IsAsciiDigit(c) || c is '+' or '-' or ' ' or '(' or ')'))
        && ValidReason(reason);
    // Machine reason or case reference only; no mailbox/name/secret copied into the permanent audit stream.
    public static bool ValidReason(string? value) => value is { Length: >= 3 and <= 80 }
        && System.Text.RegularExpressions.Regex.IsMatch(value, @"^[a-z][a-z0-9-]{2,63}(:[0-9]{1,18})?$");
    private static string Reason(string value) => value;
    private static DateTimeOffset MaxCutoff(DateTimeOffset? old, DateTimeOffset now) => old > SessionRevocationPolicy.Cutoff(now) ? old.Value : SessionRevocationPolicy.Cutoff(now);
    private static IResult Invalid() => Problem(400, ProblemCodes.InvalidRequest, "Check the staff details, roles, reason and confirmation.");
    private static IResult Changed() => Problem(409, ProblemCodes.RecordChanged, "This staff account changed. Reload before confirming again.");
    private static IResult ProviderUnavailable() => Problem(503, ProblemCodes.IdentityUnavailable, "The identity change could not be confirmed. Review the account before retrying.");
}
