namespace Swoovr.Domain;

// Internal application types, not provider claim names or public wire contracts.
// Legal: the security / legal officer of policy §11 (D189); holds, evidence exports and identity retrieval only.
public enum AccountRole { Unknown, Member, VenuePartner, Admin, Support, Legal }
public enum AccountRealm { Unknown, Members, Venues, Staff }
public enum SignInMethod { Unknown, Apple, Google, EmailPassword, EmailLink, Passkey, CorporateSso }

/// <summary>
/// Trusted server snapshot for one access evaluation. The API builds it only after
/// validating the token (signature, issuer, audience, expiry) and resolving the
/// application-owned role, root-admin flag, active state and venue memberships.
/// Never bind it from an HTTP request. Realm comes from the validated issuer (D31);
/// Method, MultiFactor and IsFresh come from <see cref="SessionEvidencePolicy"/>.
/// IsActive includes session revocation and account suspension checks.
/// </summary>
public sealed record AccessSession(
    AccountRole Role,
    AccountRealm Realm,
    SignInMethod Method,
    bool MultiFactor,
    bool EmailVerified,
    bool IsFresh,
    bool IsActive,
    bool IsRootAdmin,
    IReadOnlySet<Guid> VenueIds);

/// <summary>
/// Coarse account-area gate, not authentication or permission to read arbitrary
/// records. Resource-specific permissions and fresh membership checks remain required.
/// Each area accepts only its own realm, so a member's Apple/Google session can never
/// reach venue or staff areas. MFA stays optional except for the root admin (D36).
/// </summary>
/// <param name="localPasswordStaff">
/// D136: fictional support and admin users with a password may exist only in the local staff realm. The API passes true
/// only when it runs in Development with Staff:LocalPasswords set; everywhere else staff need Entra SSO (D32).
/// </param>
public sealed class AccountAccessPolicy(bool localPasswordStaff = false)
{
    public bool Allows(AccessSession? session, AccountRole area, Guid? venueId = null)
    {
        if (session is not { IsActive: true, EmailVerified: true } || session.Role != area)
            return false;

        // Members realm only; method evidence is a privileged concern since realms split (D42).
        if (area == AccountRole.Member)
            return venueId is null && session.Realm == AccountRealm.Members;

        if (!session.IsFresh)
            return false;

        if (area == AccountRole.VenuePartner)
            return session.Realm == AccountRealm.Venues
                && session.Method is SignInMethod.EmailPassword or SignInMethod.EmailLink
                && venueId is { } id && id != Guid.Empty && session.VenueIds.Contains(id);

        if (area is not (AccountRole.Admin or AccountRole.Support or AccountRole.Legal) || venueId is not null
            || session.Realm != AccountRealm.Staff)
            return false;

        // Employees use RePass Cloud Entra SSO (D32); the root admin uses password + OTP (D36).
        return session.Method == SignInMethod.CorporateSso && !session.IsRootAdmin
            || localPasswordStaff && !session.IsRootAdmin && session.Method == SignInMethod.EmailPassword
            // The root admin may also hold a granted `legal` role (D189); same password + OTP evidence.
            || area is (AccountRole.Admin or AccountRole.Legal) && session.IsRootAdmin
                && session.Method == SignInMethod.EmailPassword && session.MultiFactor;
    }
}
