namespace Swoovr.Contracts;

/// <summary>A badge the API decided the viewer may see (D55); clients draw only these.</summary>
public sealed record BadgeView(string Badge, DateTimeOffset? CheckedAt);

/// <summary>The signed-in member's own account. Carries no email, issuer, subject or token.</summary>
/// <param name="DateOfBirthVerified">An approved ID check reported the same date of birth (D158).</param>
/// <param name="AccountState">active, restricted, suspended or terminated (D160); anything but active shows the restricted screen;
/// pendingDeletion (D170) shows the deletion screen instead.</param>
/// <param name="DobCheck">"none", "correctionNeeded" (show the one-time prompt) or "manualReview" (D166).</param>
/// <param name="PhoneRequired">D148: a verified mobile number is required and missing; clients show the phone step first.</param>
public sealed record MemberAccount(
    string AgeStatus,
    int? Age,
    DateOnly? DateOfBirth,
    int? DeclaredAge,
    IReadOnlyList<BadgeView> Badges,
    bool DateOfBirthVerified = false,
    string AccountState = "active",
    string DobCheck = "none",
    bool PhoneVerified = false,
    bool PhoneRequired = false);

/// <summary>
/// The member's account deletion (D144, D170): "none" or "pending" with when it was asked for and when final deletion
/// runs. Founder says whether a founder recovery code applies (D173).
/// </summary>
public sealed record DeletionView(string State, DateTimeOffset? RequestedAt, DateTimeOffset? DueAt, bool Founder, bool DeleteFounderToken,
    bool SignedOutEverywhere = false, bool FounderCodeEmailed = false);

/// <summary>Confirm must be true. Immediately skips the 30-day recovery period; DeleteFounderToken also removes the founder token (#22).</summary>
public sealed record DeletionRequest(bool Confirm, bool Immediately, bool DeleteFounderToken);

/// <summary>
/// The member's mobile number (D148): masked (country code and last three digits), whether it's verified, a number
/// waiting for its code, and which sender delivers codes ("twilio", or "simulated", which never delivers, D174).
/// </summary>
public sealed record PhoneView(string? Number, bool Verified, DateTimeOffset? VerifiedAt, string? PendingNumber, DateTimeOffset? CodeExpiresAt,
    string Sender, bool Required);

/// <summary>Sends a code to this number (Australian 04xx or international +…).</summary>
public sealed record PhoneStart(string? Number);

/// <summary>The six-digit code for the number it was sent to.</summary>
public sealed record PhoneConfirm(string? Number, string? Code);

/// <summary>A one-time export link (D172): the path works once, before ExpiresAt, for the member who asked.</summary>
public sealed record ExportTicket(string Path, DateTimeOffset ExpiresAt);

/// <summary>D166: the member's one correction of their date of birth to the one on their verified ID.</summary>
public sealed record DobCorrectionRequest(DateOnly? DateOfBirth);

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
    public const string AccountDeletionPending = "account-deletion-pending";
    public const string RecentlyDisconnected = "recently-disconnected";
    public const string InvalidPhone = "invalid-phone";
    public const string PhoneCodeCooldown = "phone-code-cooldown";
    public const string PhoneCodeLimit = "phone-code-limit";
    public const string PhoneCodeWrong = "phone-code-wrong";
    public const string PhoneCodeExpired = "phone-code-expired";
    public const string PhoneInUse = "phone-in-use";
    public const string PhoneAlreadyVerified = "phone-already-verified";
    public const string PhoneSendFailed = "phone-send-failed";
    public const string PhoneVerificationRequired = "phone-verification-required";
    public const string NotConnected = "not-connected";
    public const string DeletionNotAllowed = "deletion-not-allowed";
    public const string DeletionAlreadyPending = "deletion-already-pending";
    public const string DeletionNotPending = "deletion-not-pending";
    public const string ConfirmationRequired = "confirmation-required";
    public const string ExportTooSoon = "export-too-soon";
    public const string ExportUnavailable = "export-unavailable";
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
    /// <summary>D166: there's no date-of-birth correction open for this member (none was needed, or it was used).</summary>
    public const string DobCorrectionNotOpen = "dob-correction-not-open";
    /// <summary>D166: the date of birth is changed through the one-time correction prompt, not the ordinary form.</summary>
    public const string DobCorrectionRequired = "dob-correction-required";
    /// <summary>D166: an admin is reviewing the date of birth; it can't be changed meanwhile.</summary>
    public const string DobUnderReview = "dob-under-review";
    /// <summary>D189: a hold needs a reason (up to 1,000 characters).</summary>
    public const string HoldReasonRequired = "hold-reason-required";
    /// <summary>D189: a hold names 1–20 existing accounts; named conversations must involve one of them; an admin's only the case's people.</summary>
    public const string HoldScopeInvalid = "hold-scope-invalid";
    /// <summary>D189: the period must run forwards and the review fall due within a year.</summary>
    public const string HoldDatesInvalid = "hold-dates-invalid";
    /// <summary>D189: an admin opens only a provisional hold, and only from a case.</summary>
    public const string HoldCaseRequired = "hold-case-required";
    /// <summary>D189: the hold has been released.</summary>
    public const string HoldReleased = "hold-released";
    /// <summary>D189: only a provisional hold can be confirmed.</summary>
    public const string HoldNotProvisional = "hold-not-provisional";
    /// <summary>D189: an export needs a purpose; a disclosure needs agency, officer, reference, a listed legal basis and how it was verified.</summary>
    public const string DisclosureInvalid = "disclosure-invalid";
    /// <summary>D189: no evidence key is configured, so nothing can be preserved.</summary>
    public const string EvidenceNotConfigured = "evidence-not-configured";
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
