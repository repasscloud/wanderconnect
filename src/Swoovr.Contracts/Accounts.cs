namespace Swoovr.Contracts;

/// <summary>A badge the API decided the viewer may see (D55); clients draw only these.</summary>
public sealed record BadgeView(string Badge, DateTimeOffset? CheckedAt);

/// <summary>The signed-in member's own account. Carries no email, issuer, subject or token.</summary>
public sealed record MemberAccount(
    string AgeStatus,
    int? Age,
    DateOnly? DateOfBirth,
    int? DeclaredAge,
    IReadOnlyList<BadgeView> Badges);

/// <summary>Both optional; a date of birth, when given, decides the age (owner, 23 October 2026).</summary>
public sealed record AgeUpdate(DateOnly? DateOfBirth, int? Age);

/// <summary>Problem codes the API returns in a problem response's "code" extension.</summary>
public static class ProblemCodes
{
    public const string PersistenceUnavailable = "persistence-unavailable";
    public const string AccountLocked = "account-locked";
    public const string BetaNotEnrolled = "beta-not-enrolled";
    public const string InvalidAge = "invalid-age";
    public const string StaleSession = "stale-session";
    public const string InvalidRequest = "invalid-request";
}

public sealed record BetaEnrolmentRequest(string Email);
public sealed record BetaEnrolmentView(Guid Id, string Email, DateTimeOffset CreatedAt, string State, DateTimeOffset? ActivatedAt);

public sealed record InvitationRequest(string Email, string Role, Guid? VenueId);
public sealed record InvitationView(Guid Id, string Email, string Realm, string Role, Guid? VenueId, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, string State);

/// <summary>An account as staff see it in search results. No email, issuer or subject.</summary>
public sealed record AccountView(Guid Id, string Realm, string Status, string? StatusReason, string? AgeStatus, DateTimeOffset CreatedAt);
