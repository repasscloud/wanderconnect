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
    public const string CouponInvalid = "coupon-invalid";
    public const string TooManyAttempts = "too-many-attempts";
    public const string InvalidProfile = "invalid-profile";
    public const string UnknownMode = "unknown-mode";
    public const string InvalidMode = "invalid-mode";
    public const string TooManyIntentions = "too-many-intentions";
    public const string BotModeNotAuthorised = "bot-mode-not-authorised";
    public const string NotABot = "not-a-bot";
    public const string NotAvailable = "not-available";
    public const string BotCannotInitiate = "bot-cannot-initiate";
    public const string AlreadyConnected = "already-connected";
    public const string RequestPending = "request-pending";
    public const string RecentlyDeclined = "recently-declined";
    public const string TooManyRequests = "too-many-requests";
    public const string RequestSettled = "request-settled";
    public const string ModeNotEnabled = "mode-not-enabled";
    public const string FounderPlacesFull = "founder-places-full";
    public const string AlreadyFounder = "already-founder";
    public const string NoVerifiedEmail = "no-verified-email";
    public const string NotConfigured = "not-configured";
}

public sealed record CouponRequest(string Code);
public sealed record FounderGrantView(int Place, bool EmailSent);
/// <summary>A founder token as staff see it: never the code itself.</summary>
public sealed record FounderTokenView(Guid Id, int Place, int SecretVersion, string Status, DateTimeOffset IssuedAt,
    DateTimeOffset? InvalidatedAt, string? InvalidatedReason);

public sealed record BetaEnrolmentRequest(string Email);
public sealed record BetaEnrolmentView(Guid Id, string Email, DateTimeOffset CreatedAt, string State, DateTimeOffset? ActivatedAt);

public sealed record InvitationRequest(string Email, string Role, Guid? VenueId);
public sealed record InvitationView(Guid Id, string Email, string Realm, string Role, Guid? VenueId, DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt, string State);

/// <summary>An account as staff see it in search results. No email, issuer or subject.</summary>
public sealed record AccountView(Guid Id, string Realm, string Status, string? StatusReason, string? AgeStatus, DateTimeOffset CreatedAt);

/// <summary>Admin requests for official bots and staff affiliation (Q11, D108).</summary>
public sealed record BotClassificationRequest(bool Bot);
public sealed record BotModesRequest(IReadOnlyList<string>? Modes);
public sealed record BotModesView(IReadOnlyList<string> Modes);
public sealed record StaffAffiliationRequest(bool Active);
