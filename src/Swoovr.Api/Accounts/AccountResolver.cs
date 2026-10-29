using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Auth;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>
/// The caller as the API sees it: validated token facts plus the application account they
/// map to. Account is null only when no database is configured. VerifiedEmail is the
/// token's email only when the token also says it is verified. Beta is the private beta
/// gate (D62); the member area needs it to allow access.
/// </summary>
public sealed record CurrentAccount(
    ValidatedSessionFacts Facts,
    SessionEvidence Evidence,
    string? VerifiedEmail,
    Account? Account,
    IReadOnlyList<RoleGrant> Roles,
    MemberProfile? Profile,
    BetaAccess Beta)
{
    /// <summary>
    /// The trusted snapshot for one area. Roles come only from application records: the
    /// member role is implicit for a members-realm account, staff and venue roles need a
    /// grant, and the root-admin flag counts only in the staff realm.
    /// </summary>
    public AccessSession SessionFor(AccountRole area)
    {
        var holds = area == AccountRole.Member
            ? Facts.Realm == AccountRealm.Members
            : Roles.Any(r => r.Role == area) || area == AccountRole.Admin && IsRootAdmin;
        var venues = Roles.Where(r => r.Role == AccountRole.VenuePartner && r.VenueId is not null).Select(r => r.VenueId!.Value).ToHashSet();
        // Without a database there are no grants, so only the implicit member role can exist.
        var active = Account is null ? true : Account.Status == AccountStatus.Active;
        return new AccessSession(holds ? area : AccountRole.Unknown, Facts.Realm, Evidence.Method, Evidence.MultiFactor,
            Facts.EmailVerified, Evidence.IsFresh, active, IsRootAdmin, venues);
    }

    public bool IsRootAdmin => Account is { IsRootAdmin: true } && Facts.Realm == AccountRealm.Staff;

    /// <summary>What the member's live sanctions mean right now (D133); filled in by the member gate.</summary>
    public Enforcement Enforcement { get; init; } = Enforcement.None;

    /// <summary>The staff role this caller acts with in moderation: Admin when they hold it, else Support, else Unknown.</summary>
    public AccountRole StaffRole => Allows(AccountRole.Admin) ? AccountRole.Admin : Allows(AccountRole.Support) ? AccountRole.Support : AccountRole.Unknown;

    /// <summary>D136: local fictional password staff are allowed (Development with Staff:LocalPasswords only).</summary>
    public bool LocalPasswordStaff { get; init; }

    public bool Allows(AccountRole area) => new AccountAccessPolicy(LocalPasswordStaff).Allows(SessionFor(area), area)
        && (area != AccountRole.Member || BetaAccessPolicy.Allows(Beta));

    /// <summary>DEFAULT (Keycloak design): sensitive actions need a sign-in within the last 15 minutes.</summary>
    public bool RecentlyAuthenticated(DateTimeOffset now) =>
        now - Facts.AuthenticatedAt is var age && age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromMinutes(15);
}

public sealed class AccountResolver(IServiceProvider services, KeycloakOptions keycloak, SessionEvidencePolicy evidence,
    TimeProvider clock, IConfiguration configuration, IHostEnvironment environment, ILogger<AccountResolver> log)
{
    /// <summary>
    /// D148, D184 (owner, 27 October 2026): a verified mobile number is required to finish registration, so the gate is on
    /// unless `Phone:Required` is false (local test hosts only). Codes stay simulated until deployment (D183).
    /// </summary>
    public bool PhoneRequired => configuration.GetValue("Phone:Required", true);
    private bool LocalPasswordStaff => environment.IsDevelopment() && configuration.GetValue("Staff:LocalPasswords", false);
    /// <summary>The private beta gate is on unless Beta:Enabled is false (D62; turned off at public launch).</summary>
    private bool BetaEnabled => configuration.GetValue("Beta:Enabled", true);
    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(5);
    public SwoovrDbContext? Db { get; } = services.GetService<SwoovrDbContext>();
    /// <summary>The request's services, for endpoints that need a scoped service only on some paths.</summary>
    public IServiceProvider Services => services;

    public async Task<CurrentAccount?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var facts = KeycloakAuthentication.Facts(user, keycloak);
        if (facts is null) return null;
        var now = clock.GetUtcNow();
        var result = evidence.Evaluate(facts, now);
        var email = facts.EmailVerified ? EmailAddress.Normalise(user.FindFirstValue("email")) : null;
        if (Db is null)
            return new(facts, result, email, null, [], null,
                BetaAccessPolicy.Evaluate(BetaEnabled, persistence: false, facts.Realm, Guid.Empty, facts.EmailVerified, email, []));

        var issuer = user.FindFirstValue("iss")!;
        var account = await Db.Accounts.SingleOrDefaultAsync(a => a.Issuer == issuer && a.Subject == facts.Subject, cancellationToken);
        if (account is null)
            account = facts.Realm == AccountRealm.Members
                ? await CreateMemberAsync(issuer,facts,email,now,cancellationToken)
                : await CreateAsync(issuer,facts,email,now,cancellationToken);
        await using var state = facts.Realm == AccountRealm.Members ? await AccountLifecycleLease.AcquireAsync(Db, account.Id, cancellationToken) : null;
        if (state is not null)
        {
            await Db.Entry(account).ReloadAsync(cancellationToken);
            if (Db.Entry(account).State == EntityState.Detached) return null;
        }
        // D170: a deleted account stays deleted. A token issued just before final deletion is still valid for a few
        // minutes; it gets the tombstone (refused everywhere), never a new profile, and the email isn't written back.
        if (account.Status == AccountStatus.Deleted)
            return new(facts, result, null, account, [], null, BetaAccess.NotRequired) { LocalPasswordStaff = LocalPasswordStaff };
        // #88 (policy §11): a revoked staff member's sessions end; a token from a sign-in before the revocation is refused.
        if (StaffAccessPolicy.SessionRevoked(account.SessionsRevokedAt, facts.AuthenticatedAt)) return null;
        await using var identifierAuthority = facts.Realm == AccountRealm.Members ? await PreservationLease.SharedAsync(Db,cancellationToken) : null;
        var existingProfile = facts.Realm == AccountRealm.Members ? await Db.MemberProfiles.FindAsync([account.Id],cancellationToken) : null;
        if (facts.Realm == AccountRealm.Members && email is not null && (account.Email != email || existingProfile is null))
            await CheckEmailAsync(email,issuer,facts.Subject,account.Id,now,cancellationToken);
        var emailChanged = email is not null && account.Email is not null && account.Email != email;
        // D187 (policy §4): a verified email that changed (in Keycloak) goes to the encrypted contact history.
        if (emailChanged && services.GetRequiredService<ContactHistoryKeys>().Record(account.Id, ContactType.Email, account.Email!, null, now, account.Id, "changed") is { } previous)
            Db.ContactHistories.Add(previous);
        var previousEmail = emailChanged ? account.Email : null;
        if (email is not null && account.Email != email || now - account.LastSeenAt > SeenInterval)
        {
            account.Email = email ?? account.Email;
            account.LastSeenAt = now;
            await Db.SaveChangesAsync(cancellationToken);
        }
        MemberProfile? profile = null;
        if (facts.Realm == AccountRealm.Members)
        {
            profile = existingProfile ?? await CreateProfileAsync(account.Id, facts, email, now, cancellationToken);
        }
        else
            await AcceptInvitationsAsync(account, facts, result, email, now, cancellationToken);
        if (identifierAuthority is not null) await identifierAuthority.DisposeAsync();
        // #87 (policy §4, §14): the previous address hears that it changed, never what to.
        if (previousEmail is not null && facts.Realm == AccountRealm.Members)
            await services.GetRequiredService<Swoovr.Infrastructure.Email.IEmailSender>().SendAsync(new Swoovr.Infrastructure.Email.EmailMessage(previousEmail,
                "Your Swoovr email address was changed",
                """
                The email address on your Swoovr account was just changed, so this address won't get Swoovr emails any more.

                If this wasn't you, contact Swoovr support straight away.

                — Swoovr (RePass Cloud Pty Ltd)
                """), cancellationToken);
        // External mail must not hold the global preservation barrier.
        if (emailChanged && profile is { FoundingMember: true })
            await services.GetRequiredService<Founders>().ReissueForEmailChangeAsync(account,profile,now,cancellationToken);
        var roles = await Db.RoleGrants.Where(r => r.AccountId == account.Id).ToListAsync(cancellationToken);
        var beta = await BetaAsync(account, facts, email, now, cancellationToken);
        return new(facts, result, email, account, roles, profile, beta) { LocalPasswordStaff = LocalPasswordStaff };
    }

    private async Task<Account> CreateMemberAsync(string issuer,ValidatedSessionFacts facts,string? email,DateTimeOffset now,CancellationToken token)
    {
        await using var registration = await AccountLifecycleLease.AcquireRegistrationAsync(Db!,issuer,facts.Subject,token);
        var existing = await Db!.Accounts.SingleOrDefaultAsync(a => a.Issuer == issuer && a.Subject == facts.Subject,token);
        if (existing is not null) return existing;
        var id = Guid.NewGuid();
        await using var state = await AccountLifecycleLease.AcquireAsync(Db,id,token);
        await using var barrier = await PreservationLease.SharedAsync(Db,token);
        if (email is not null) await CheckEmailAsync(email,issuer,facts.Subject,null,now,token);
        // Keep the committed quota attempt, but never commit a partial account/profile registration.
        await using var transaction = await Db.Database.BeginTransactionAsync(token);
        var account = await CreateAsync(issuer,facts,email,now,token,id);
        if (account.Id != id) throw new DbUpdateConcurrencyException("Registration changed. Retry.");
        await CreateProfileAsync(id,facts,email,now,token);
        await transaction.CommitAsync(token);
        return account;
    }

    private Task CheckEmailAsync(string email,string issuer,string subject,Guid? account,DateTimeOffset now,CancellationToken token)
    {
        var context = services.GetService<IHttpContextAccessor>()?.HttpContext;
        var address = context is null ? null : PhoneEndpoints.ClientAddress(context,configuration);
        return services.GetRequiredService<BannedIdentifierRegistry>().RequireAllowedAsync(BannedIdentifierType.Email,email,
            issuer + "\0" + subject,address,account,now,token);
    }

    /// <summary>
    /// Evaluates the beta gate and, the first time a verified email matches an unbound
    /// enrolment, binds it to this account so a later email change keeps access (D62).
    /// </summary>
    private async Task<BetaAccess> BetaAsync(Account account, ValidatedSessionFacts facts, string? email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (facts.Realm != AccountRealm.Members || !BetaEnabled) return BetaAccess.NotRequired;
        var candidates = await Db!.BetaEnrolments
            .Where(e => e.RevokedAt == null && (e.AccountId == account.Id || e.AccountId == null && email != null && e.Email == email))
            .ToListAsync(cancellationToken);
        var access = BetaAccessPolicy.Evaluate(true, true, facts.Realm, account.Id, facts.EmailVerified, email, candidates);
        if (access == BetaAccess.Enrolled && candidates.All(e => e.AccountId != account.Id))
        {
            var enrolment = candidates.First(e => e.AccountId is null);
            (enrolment.AccountId, enrolment.ActivatedAt) = (account.Id, now);
            Audit.Record(Db, now, account.Id, "account", "beta.activated", "beta-enrolment", enrolment.Id.ToString());
            try { await Db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException exception)
            {
                // Another request bound it first; access is still decided by this evaluation.
                log.LogInformation(exception, "Beta activation raced.");
                Db.ChangeTracker.Clear();
            }
        }
        return access;
    }

    private async Task<Account> CreateAsync(string issuer, ValidatedSessionFacts facts, string? email, DateTimeOffset now, CancellationToken cancellationToken, Guid? proposedId = null)
    {
        var account = new Account
        {
            Id = proposedId ?? Guid.NewGuid(), Issuer = issuer, Subject = facts.Subject, Realm = facts.Realm, Email = email,
            Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now,
        };
        Db!.Accounts.Add(account);
        Audit.Record(Db, now, null, "system", "account.created", "account", account.Id.ToString(), facts.Realm.ToString());
        try { await Db.SaveChangesAsync(cancellationToken); return account; }
        catch (DbUpdateException)
        {
            // A concurrent first request created it; (issuer, subject) is unique, so read theirs.
            Db.ChangeTracker.Clear();
            return await Db.Accounts.SingleAsync(a => a.Issuer == issuer && a.Subject == facts.Subject, cancellationToken);
        }
    }

    private async Task<MemberProfile> CreateProfileAsync(Guid accountId, ValidatedSessionFacts facts, string? email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = Db!.Database.CurrentTransaction is null
            ? await Db.Database.BeginTransactionAsync(cancellationToken) : null;
        var profile = new MemberProfile
        {
            AccountId = accountId,
            AgeStatus = facts.AdultAttestedAt is null ? AgeStatus.Unconfirmed : AgeStatus.Attested,
            AgeStatusAt = facts.AdultAttestedAt ?? now,
        };
        // D127: an age-assurance flag (or an under-18 lock) on another account with the same verified email can't be
        // escaped by registering again; the new profile starts flagged. Matching is by address only, never a merge (D38).
        if (email is not null && AgeAssurance.Inherits(await (from other in Db!.Accounts
                join p in Db.MemberProfiles on other.Id equals p.AccountId
                where other.Realm == AccountRealm.Members && other.Email == email && other.Id != accountId
                select p.AgeStatus).ToListAsync(cancellationToken)))
        {
            (profile.AgeStatus, profile.AgeStatusAt) = (AgeStatus.NeedsVerification, now);
            Audit.Record(Db, now, null, "system", "age.needs-verification", "account", accountId.ToString(), "same-email");
        }
        Db!.MemberProfiles.Add(profile);
        try
        {
            await Db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Db.ChangeTracker.Clear();
            var raced = await Db.MemberProfiles.SingleAsync(p => p.AccountId == accountId, cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return raced;
        }
        // Legacy flag preservation remains required until D217 backfill/workflows replace it. A flag failure must
        // abort this transaction, not be mistaken for a concurrent profile insert and silently committed.
        await services.GetRequiredService<AccountDeletion>().FlagIfReturningAsync(accountId, email, now, cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
        return profile;
    }

    /// <summary>
    /// Binds pending venue or staff invitations for this verified email to this account, once.
    /// After acceptance the role belongs to (issuer, subject); the email is not consulted again.
    /// </summary>
    private async Task AcceptInvitationsAsync(Account account, ValidatedSessionFacts facts, SessionEvidence result, string? email,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (email is null) return;
        var pending = await Db!.Invitations
            .Where(i => i.Realm == facts.Realm && i.Email == email && i.AcceptedAt == null && i.RevokedAt == null && i.ExpiresAt > now)
            .ToListAsync(cancellationToken);
        var accepted = false;
        foreach (var invitation in pending.Where(i => InvitationPolicy.CanAccept(i, facts.Realm, result.Method, facts.EmailVerified, email, now, LocalPasswordStaff)))
        {
            invitation.AcceptedAt = now;
            invitation.AcceptedBy = account.Id;
            if (!await Db.RoleGrants.AnyAsync(r => r.AccountId == account.Id && r.Role == invitation.Role && r.VenueId == invitation.VenueId, cancellationToken))
                Db.RoleGrants.Add(new RoleGrant { Id = Guid.NewGuid(), AccountId = account.Id, Role = invitation.Role, VenueId = invitation.VenueId, GrantedAt = now });
            Audit.Record(Db, now, account.Id, "account", "invitation.accepted", "invitation", invitation.Id.ToString(), invitation.Role.ToString());
            accepted = true;
        }
        if (!accepted) return;
        try { await Db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException exception)
        {
            log.LogWarning(exception, "Invitation acceptance raced; the next request retries.");
            Db.ChangeTracker.Clear();
        }
    }
}

public static class Audit
{
    public static void Record(SwoovrDbContext db, DateTimeOffset at, Guid? actor, string actorKind, string action,
        string? targetType = null, string? targetId = null, string? detail = null) =>
        db.AuditEntries.Add(new AuditEntry
        {
            At = at, ActorAccountId = actor, Actor = actorKind, Action = action,
            TargetType = targetType, TargetId = targetId, Detail = detail,
        });
}
