namespace Swoovr.Contracts;

/// <summary>A badge the API decided the viewer may see (D55); clients draw only these.</summary>
public sealed record BadgeView(string Badge, DateTimeOffset? CheckedAt);

/// <summary>The signed-in member's own account. Carries no email, issuer, subject or token.</summary>
/// <param name="DateOfBirthVerified">An approved ID check reported the same date of birth (D158).</param>
/// <param name="AccountState">active, restricted, suspended or terminated (D160); anything but active shows the restricted screen.</param>
public sealed record MemberAccount(
    string AgeStatus,
    int? Age,
    DateOnly? DateOfBirth,
    int? DeclaredAge,
    IReadOnlyList<BadgeView> Badges,
    bool DateOfBirthVerified = false,
    string AccountState = "active");

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
    public const string UnknownPlace = "unknown-place";
    public const string InvalidTripDates = "invalid-trip-dates";
    public const string ItineraryNotBackToBack = "itinerary-not-back-to-back";
    public const string TripOverlaps = "trip-overlaps";
    public const string TripCancelled = "trip-cancelled";
    public const string NotLocal = "not-local";
    public const string TooManyTrips = "too-many-trips";
    public const string ModeNotEnabled = "mode-not-enabled";
    public const string FounderPlacesFull = "founder-places-full";
    public const string AlreadyFounder = "already-founder";
    public const string NoVerifiedEmail = "no-verified-email";
    public const string NotConfigured = "not-configured";
    public const string HomeCityRequired = "home-city-required";
    public const string MarketNotOpen = "market-not-open";
    public const string HomeMarketLocked = "home-market-locked";
    public const string UnknownMarket = "unknown-market";
    public const string AgeVerificationRequired = "age-verification-required";
    public const string VerificationNotConfigured = "verification-not-configured";
    public const string VerificationProviderFailed = "verification-provider-failed";
    public const string VerificationAttemptUsed = "verification-attempt-used";
    public const string AlreadyVerified = "already-verified";
    public const string FounderNotEligible = "founder-not-eligible";
    public const string AccountSuspended = "account-suspended";
    public const string AccountRestricted = "account-restricted";
    public const string Restricted = "restricted";
    public const string UnknownCategory = "unknown-category";
    public const string UnsupportedSubject = "unsupported-subject";
    public const string InvalidReport = "invalid-report";
    public const string ModerationNotPermitted = "moderation-not-permitted";
    public const string AppealNotAllowed = "appeal-not-allowed";
    public const string InvalidEntitlement = "invalid-entitlement";
    public const string DateOfBirthRequired = "date-of-birth-required";
    public const string DateOfBirthLocked = "date-of-birth-locked";
    public const string DatingDetailsRequired = "dating-details-required";
    public const string InvalidFilter = "invalid-filter";
    public const string PlusRequired = "plus-required";
    /// <summary>D142, D149: a traveller may browse a destination but not start contact there until arrival is confirmed.</summary>
    public const string ArrivalRequired = "arrival-required";
    /// <summary>D149: the trip's arrival date hasn't been reached at the destination.</summary>
    public const string ArrivalTooEarly = "arrival-too-early";
    /// <summary>D149: no usable device location (missing, stale, from the future or too inaccurate).</summary>
    public const string LocationUnusable = "location-unusable";
    /// <summary>D149: the device location is outside the destination market.</summary>
    public const string LocationOutsideMarket = "location-outside-market";
    /// <summary>D142, D149: the trip is cancelled, over, or in a market that isn't open.</summary>
    public const string TripNotLive = "trip-not-live";
    /// <summary>D142: Traveller Search opens 14 days before arrival, while the trip is visible, and not in the member's own market.</summary>
    public const string SearchNotOpen = "search-not-open";
    /// <summary>D143: a message must be 1–2,000 characters of text.</summary>
    public const string InvalidMessage = "invalid-message";
    /// <summary>D143: delete for both only by the sender within five minutes.</summary>
    public const string DeleteWindowPassed = "delete-window-passed";
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

/// <summary>Admin view and replacement of one market (D125, D146): status planned, beta, active or suspended; one or more
/// areas (a GeoNames city and a radius); the modes it offers. HomeMembers counts members, never names them.</summary>
public sealed record MarketAreaView(int CentreCityId, double RadiusKm);
public sealed record MarketAdminView(string Id, string Name, string Status, IReadOnlyList<MarketAreaView> Areas, IReadOnlyList<string> Modes, int HomeMembers);
public sealed record MarketUpdate(string? Name, string? Status, IReadOnlyList<MarketAreaView>? Areas, IReadOnlyList<string>? Modes);
/// <summary>The markets an official bot is assigned to; `*` is every open market (D125 rules 38–41).</summary>
public sealed record BotMarketsRequest(IReadOnlyList<string>? Markets);
public sealed record BotMarketsView(IReadOnlyList<string> Markets);

/// <summary>
/// The member's ID verification (D126–D129): their status, the latest attempt's state, Didit's hosted link while a
/// session is open, whether they may start one (or why not), and whether this is Didit's test mode (D147; never
/// presented as a live check). AgeCheckRequired is the age-assurance gate (D127).
/// </summary>
public sealed record IdVerificationView(string IdStatus, DateTimeOffset? CheckedAt, string? AttemptState, string? Url, bool CanStart,
    string? Refusal, bool Available, bool TestMode, bool AgeCheckRequired);
/// <summary>"web" or "app": picks a configured return address, never one from the client.</summary>
public sealed record StartVerification(string Client);
/// <summary>An attempt as admins see it (D129): outcome and operational metadata only, never document data.</summary>
public sealed record IdAttemptAdminView(Guid Id, string State, string ProviderStatus, bool Started, bool SelfService, bool? Adult,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, DateTimeOffset? CompletedAt);
public sealed record AdminVerificationView(string IdStatus, bool CanRequestIdVerification, string AgeStatus, IReadOnlyList<IdAttemptAdminView> Attempts);
public sealed record AgeAssuranceRequest(bool NeedsVerification);
/// <summary>The founder allocation job's result (D130): counts only.</summary>
public sealed record FounderAllocationView(int Stamped, int Allocated, int Skipped, int PlacesLeft);
