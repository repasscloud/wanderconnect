namespace Swoovr.Domain;

/// <summary>
/// A pre-authorised email for the private beta (D62). It activates automatically the first
/// time a members-realm account signs in with that verified address, and is then bound to
/// that account, so a later email change keeps access. Revoking ends access.
/// </summary>
public sealed class BetaEnrolment
{
    public Guid Id { get; set; }
    /// <summary>Normalised.</summary>
    public string Email { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>The adding account, or null for the CLI.</summary>
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public Guid? AccountId { get; set; }
    // Staff report of external private-distribution work; never Apple evidence or an access grant.
    public string IosDistributionState { get; set; } = "not-recorded";
    public long IosDistributionRevision { get; set; }
    public DateTimeOffset? IosDistributionChangedAt { get; set; }
    public Guid? IosDistributionChangedBy { get; set; }
}

public enum BetaAccess { NotRequired, Enrolled, NotEnrolled, Unavailable }

/// <summary>
/// Private beta gate for the members area (D62). Venue and staff realms are not gated.
/// With the beta on, a member needs an active enrolment bound to their account, or one for
/// their current verified email that is not bound to another account. Without an
/// application database the answer is Unavailable, which denies access (fail closed).
/// </summary>
public static class BetaAccessPolicy
{
    public static BetaAccess Evaluate(bool betaEnabled, bool persistence, AccountRealm realm, Guid accountId,
        bool emailVerified, string? verifiedEmail, IEnumerable<BetaEnrolment> candidates)
    {
        if (realm != AccountRealm.Members || !betaEnabled) return BetaAccess.NotRequired;
        if (!persistence) return BetaAccess.Unavailable;
        var email = emailVerified ? EmailAddress.Normalise(verifiedEmail) : null;
        return candidates.Any(e => e.RevokedAt is null
            && (e.AccountId == accountId || e.AccountId is null && email is not null && e.Email == email))
            ? BetaAccess.Enrolled : BetaAccess.NotEnrolled;
    }

    public static bool Allows(BetaAccess access) => access is BetaAccess.NotRequired or BetaAccess.Enrolled;
}
