using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Auth;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>
/// The caller as the API sees it: validated token facts plus the application account they
/// map to. Account is null only when no database is configured. VerifiedEmail is the
/// token's email only when the token also says it is verified.
/// </summary>
public sealed record CurrentAccount(
    ValidatedSessionFacts Facts,
    SessionEvidence Evidence,
    string? VerifiedEmail,
    Account? Account,
    IReadOnlyList<RoleGrant> Roles,
    MemberProfile? Profile)
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

    public bool Allows(AccountRole area) => new AccountAccessPolicy().Allows(SessionFor(area), area);

    /// <summary>DEFAULT (Keycloak design): sensitive actions need a sign-in within the last 15 minutes.</summary>
    public bool RecentlyAuthenticated(DateTimeOffset now) =>
        now - Facts.AuthenticatedAt is var age && age >= TimeSpan.FromSeconds(-5) && age <= TimeSpan.FromMinutes(15);
}

public sealed class AccountResolver(IServiceProvider services, KeycloakOptions keycloak, SessionEvidencePolicy evidence,
    TimeProvider clock, ILogger<AccountResolver> log)
{
    private static readonly TimeSpan SeenInterval = TimeSpan.FromMinutes(5);
    public SwoovrDbContext? Db { get; } = services.GetService<SwoovrDbContext>();

    public async Task<CurrentAccount?> ResolveAsync(ClaimsPrincipal user, CancellationToken cancellationToken)
    {
        var facts = KeycloakAuthentication.Facts(user, keycloak);
        if (facts is null) return null;
        var now = clock.GetUtcNow();
        var result = evidence.Evaluate(facts, now);
        var email = facts.EmailVerified ? EmailAddress.Normalise(user.FindFirstValue("email")) : null;
        if (Db is null) return new(facts, result, email, null, [], null);

        var issuer = user.FindFirstValue("iss")!;
        var account = await Db.Accounts.SingleOrDefaultAsync(a => a.Issuer == issuer && a.Subject == facts.Subject, cancellationToken)
            ?? await CreateAsync(issuer, facts, email, now, cancellationToken);
        if (email is not null && account.Email != email || now - account.LastSeenAt > SeenInterval)
        {
            account.Email = email ?? account.Email;
            account.LastSeenAt = now;
            await Db.SaveChangesAsync(cancellationToken);
        }
        MemberProfile? profile = null;
        if (facts.Realm == AccountRealm.Members)
            profile = await Db.MemberProfiles.FindAsync([account.Id], cancellationToken)
                ?? await CreateProfileAsync(account.Id, facts, now, cancellationToken);
        else
            await AcceptInvitationsAsync(account, facts, result, email, now, cancellationToken);
        var roles = await Db.RoleGrants.Where(r => r.AccountId == account.Id).ToListAsync(cancellationToken);
        return new(facts, result, email, account, roles, profile);
    }

    private async Task<Account> CreateAsync(string issuer, ValidatedSessionFacts facts, string? email, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(), Issuer = issuer, Subject = facts.Subject, Realm = facts.Realm, Email = email,
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

    private async Task<MemberProfile> CreateProfileAsync(Guid accountId, ValidatedSessionFacts facts, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var profile = new MemberProfile
        {
            AccountId = accountId,
            AgeStatus = facts.AdultAttestedAt is null ? AgeStatus.Unconfirmed : AgeStatus.Attested,
            AgeStatusAt = facts.AdultAttestedAt ?? now,
        };
        Db!.MemberProfiles.Add(profile);
        try { await Db.SaveChangesAsync(cancellationToken); return profile; }
        catch (DbUpdateException)
        {
            Db.ChangeTracker.Clear();
            return await Db.MemberProfiles.SingleAsync(p => p.AccountId == accountId, cancellationToken);
        }
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
        foreach (var invitation in pending.Where(i => InvitationPolicy.CanAccept(i, facts.Realm, result.Method, facts.EmailVerified, email, now)))
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
