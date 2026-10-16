namespace DatingApp.Domain;

// Internal application types, not provider claim names or public wire contracts.
public enum AccountRole { Unknown, Member, VenuePartner, Admin, Support }
public enum SignInMethod { Unknown, Apple, Google, EmailPassword, EmailLink }

/// <summary>
/// Trusted server snapshot for one access evaluation. A future adapter must validate
/// the provider session and resolve application-owned role, active state and venue
/// memberships before constructing this value. Never bind it from an HTTP request.
/// IsActive includes session revocation and account suspension checks.
/// HasApprovedPrivilegedEmailEvidence is a future server policy verdict, not an
/// account type. Its evaluator is unimplemented pending Q10; keep it false in any
/// real adapter until dedicated-email policy and session evidence are validated.
/// Linked social identities do not establish the method used in this session.
/// </summary>
public sealed record AccessSession(
    AccountRole Role,
    SignInMethod Method,
    bool HasApprovedPrivilegedEmailEvidence,
    bool IsActive,
    IReadOnlySet<Guid> VenueIds);

/// <summary>
/// Coarse account-area gate, not authentication or permission to read arbitrary
/// records. Resource-specific permissions and fresh membership checks remain required.
/// No MFA requirement: enrolment is optional. Member email links remain disabled.
/// </summary>
public sealed class AccountAccessPolicy
{
    public bool Allows(AccessSession? session, AccountRole area, Guid? venueId = null)
    {
        if (session is not { IsActive: true } || session.Role != area)
            return false;

        if (area == AccountRole.Member)
            return venueId is null && session.Method is
                SignInMethod.Apple or SignInMethod.Google or SignInMethod.EmailPassword;

        if (area is not (AccountRole.VenuePartner or AccountRole.Admin or AccountRole.Support)
            || !session.HasApprovedPrivilegedEmailEvidence
            || session.Method is not (SignInMethod.EmailPassword or SignInMethod.EmailLink))
            return false;

        return area == AccountRole.VenuePartner
            ? venueId is { } id && id != Guid.Empty && session.VenueIds.Contains(id)
            : venueId is null;
    }
}
