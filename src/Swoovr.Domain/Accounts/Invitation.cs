namespace Swoovr.Domain;

/// <summary>
/// A venue-partner or staff invitation created by an admin. The verified email in the
/// invited realm's token is matched once, on first sign-in; after that the role belongs to
/// the account's (issuer, subject) and the email plays no further part (Keycloak design).
/// </summary>
public sealed class Invitation
{
    public Guid Id { get; set; }
    public AccountRealm Realm { get; set; }
    /// <summary>Normalised.</summary>
    public string Email { get; set; } = "";
    public AccountRole Role { get; set; }
    public Guid? VenueId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public Guid? AcceptedBy { get; set; }
}

public static class InvitationPolicy
{
    /// <summary>DEFAULT lifetime of a new invitation.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    /// <summary>The realm an invited role belongs to, or Unknown for a role that is never invited.</summary>
    public static AccountRealm RealmFor(AccountRole role) => role switch
    {
        AccountRole.VenuePartner => AccountRealm.Venues,
        AccountRole.Admin or AccountRole.Support => AccountRealm.Staff,
        _ => AccountRealm.Unknown,
    };

    /// <summary>Whether a new invitation is well formed: an invitable role, its realm, a venue only for partners.</summary>
    public static bool IsValidRequest(AccountRole role, string? email, Guid? venueId) =>
        RealmFor(role) != AccountRealm.Unknown && EmailAddress.Normalise(email) is not null
        && (role == AccountRole.VenuePartner ? venueId is { } id && id != Guid.Empty : venueId is null);

    /// <summary>
    /// Whether this sign-in may accept the invitation. The session must come from the invited
    /// realm with a verified email equal to the invitation's, using that realm's own sign-in
    /// method (staff: Entra SSO; venues: email and password or email link). A member-realm or
    /// social session can never accept one, whatever its email (D31, D38).
    /// </summary>
    public static bool CanAccept(Invitation? invitation, AccountRealm realm, SignInMethod method,
        bool emailVerified, string? verifiedEmail, DateTimeOffset now)
    {
        if (invitation is null || invitation.RevokedAt is not null || invitation.AcceptedAt is not null
            || now >= invitation.ExpiresAt || realm != invitation.Realm || realm != RealmFor(invitation.Role)
            || !emailVerified || EmailAddress.Normalise(verifiedEmail) is not { } email
            || !string.Equals(email, invitation.Email, StringComparison.Ordinal))
            return false;
        return realm switch
        {
            AccountRealm.Staff => method == SignInMethod.CorporateSso,
            AccountRealm.Venues => method is SignInMethod.EmailPassword or SignInMethod.EmailLink,
            _ => false,
        };
    }
}
