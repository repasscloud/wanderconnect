using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>
/// Wording for the member screens (Explore, person pages, Connections, the restricted screen), shared by the app and
/// the web so both say the same thing about the same API answer (D141). Words only: every rule is the API's.
/// </summary>
public static class MemberText
{
    /// <summary>
    /// A refusal that replaces a whole member screen (D127, D160, D62, D139): restricted, suspended or terminated, the
    /// age check, the beta gate or a locked account. Null when the code isn't one of them.
    /// </summary>
    public static string? Barred(string? code) => code switch
    {
        ProblemCodes.AccountRestricted => "Your account is restricted. You can't use Swoovr until this is lifted. Read the notice under Me to see why; you can appeal within 30 days.",
        ProblemCodes.AccountSuspended => "Your account is suspended. Read the notice under Me to see why; you can appeal within 30 days.",
        ProblemCodes.AgeVerificationRequired => "We need to confirm you're 18 or over before you can keep using Swoovr. Verify your ID under Me.",
        ProblemCodes.BetaNotEnrolled => "This opens once your email is on the private beta list.",
        ProblemCodes.AccountLocked => "This account is locked.",
        _ => null,
    };

    /// <summary>The restricted screen's heading for an account state (D160); null when the account is active.</summary>
    public static string? RestrictedTitle(string accountState) => accountState switch
    {
        "active" => null,
        "suspended" => "Your account is suspended",
        "terminated" => "Your account has been closed",
        _ => "Your account is restricted",
    };

    public const string RestrictedBody = "You can't use Swoovr until this is lifted. Read the notice to see why; you can appeal within 30 days.";

    /// <summary>Why Explore shows no one for a mode (D125, D139, D157); null when the API answered with people.</summary>
    public static string? ExploreNotice(string? code, string modeLabel) => code switch
    {
        ProblemCodes.ModeNotEnabled => $"{modeLabel} is off for you, so you can't see who's here. Turn it on in your profile under Me.",
        ProblemCodes.DateOfBirthRequired => "Add your date of birth under Me to discover people. It's never shown.",
        ProblemCodes.DatingDetailsRequired => "Dating needs your gender and who you'd like to see. Set them under Me → Who I'd like to meet.",
        ProblemCodes.HomeCityRequired => "Add your home city to discover people in your community.",
        ProblemCodes.MarketNotOpen => "Swoovr isn't available in your area yet. We're expanding to new cities.",
        _ => null,
    };

    /// <summary>Why a request wasn't sent (D112, D125).</summary>
    public static string RequestProblem(string? code) => code switch
    {
        ProblemCodes.RequestPending => "There's already a request between you in this mode.",
        ProblemCodes.AlreadyConnected => "You're already connected in this mode.",
        ProblemCodes.TooManyRequests => "You've sent a lot of requests today. Try again later.",
        ProblemCodes.RecentlyDeclined => "You can't ask again in this mode yet.",
        ProblemCodes.BotCannotInitiate => "This account can't send requests.",
        ProblemCodes.ArrivalRequired => "You can send requests here once you've confirmed you've arrived.",
        _ => "That request couldn't be sent.",
    };

    /// <summary>Why confirm-arrival didn't confirm (D149); the trip stays upcoming and the member can try again or ask support.</summary>
    public static string ArrivalProblem(string? code) => code switch
    {
        ProblemCodes.ArrivalTooEarly => "You can confirm your arrival from your arrival date.",
        ProblemCodes.LocationUnusable => "We couldn't use your location. Turn location on for Swoovr and try again, or ask Swoovr support to look.",
        ProblemCodes.LocationOutsideMarket => "Your location isn't in this trip's city yet. Try again once you're there, or ask Swoovr support to look.",
        ProblemCodes.TripNotLive => "This trip is cancelled, over, or somewhere Swoovr isn't open.",
        ProblemCodes.TooManyAttempts => "That's a lot of location checks. Try again later.",
        _ => "We couldn't confirm your arrival. Try again.",
    };

    /// <summary>A trip's arrival as its owner reads it (D149), or null before any check.</summary>
    public static string? ArrivalState(string state) => state switch
    {
        "arrived" => "Arrived: you can send requests to locals.",
        "unconfirmed" => "Arrival not confirmed yet.",
        _ => null,
    };

    public const string Arrived = "You've arrived. You can now send requests to people here.";
    public const string BrowsingBeforeArrival = "You're looking before you arrive. You can send requests once you've confirmed you've arrived.";

    /// <summary>A home-location check's outcome (D150). An inconsistent one is never a penalty by itself.</summary>
    public static string HomeCheck(string outcome) => outcome switch
    {
        "consistent" => "Thanks. Your location matches your home city.",
        "inconsistent" => "Your location isn't in your home city's area. That's fine if you're away.",
        _ => "Nothing to check right now.",
    };

    /// <summary>Why Traveller Search isn't open through a trip (D142).</summary>
    public static string SearchProblem(string? code) => code switch
    {
        ProblemCodes.TripNotLive => "This trip is cancelled, over, or somewhere Swoovr isn't open.",
        ProblemCodes.SearchNotOpen => "Search opens 14 days before you arrive, while the trip is visible to locals, and never in your own city.",
        ProblemCodes.MarketNotOpen => "Swoovr doesn't offer this mode there.",
        _ => ExploreNotice(code, "This mode") ?? Barred(code) ?? "We couldn't search there. Try again later.",
    };

    /// <summary>What happened after accept, decline or withdraw; the same words on Connections and person pages.</summary>
    public static string Answered(string answer, bool ok) => !ok ? "That request isn't available any more." : answer switch
    {
        "accept" => "Connected.", "decline" => "Declined. They won't be told why.", _ => "Request withdrawn.",
    };

    public const string RequestUnavailable = "Requests open when you're in the same Swoovr market.";
    public const string BlockExplained = "You won't see each other anywhere on Swoovr, and any requests between you are cancelled. They aren't told.";
    public const string Blocked = "Blocked. You won't see each other. You can unblock from Connections.";

    /// <summary>A notice's heading for its kind (D135).</summary>
    public static string NoticeTitle(string kind) => kind switch
    {
        "warning" => "A warning on your account",
        "restriction" => "Your account is restricted",
        "suspension" => "Your account is suspended",
        "termination" => "Your account has been closed",
        "appealUpheld" => "Your appeal wasn't accepted",
        "appealOverturned" => "Your appeal was accepted",
        "sanctionLifted" => "An action on your account was lifted",
        _ => "Notice",
    };

    /// <summary>What a member sees of their own report (D131): never the outcome.</summary>
    public static string ReportStatus(string status) => status switch { "submitted" => "Submitted", "underReview" => "Under review", "reviewed" => "Reviewed", _ => "Closed" };

    /// <summary>Whether a notice's action may still be appealed from it (D135).</summary>
    public static bool Appealable(NoticeView notice) => notice.Sanction is { CanAppeal: true, AppealStatus: null } && notice.Kind is not ("appealUpheld" or "appealOverturned" or "sanctionLifted");

    public static string AppealStatus(string status) => status == "pending" ? "being reviewed" : status == "overturned" ? "accepted" : "not accepted";

    /// <summary>The ID check's state as the member reads it (D126).</summary>
    public static string VerificationState(IdVerificationView view) => (view.IdStatus, view.AttemptState) switch
    {
        ("verified", _) => $"Your ID is verified{(view.CheckedAt is { } at ? $" (checked {at:d MMM yyyy})" : "")}.",
        (_, "created") => "Your check is ready. Continue on Didit to start it.",
        (_, "inProgress") => "Your check is in progress on Didit.",
        (_, "inReview") => "Didit is reviewing your check. This can take a little while.",
        ("rejected", _) => "Your ID couldn't be verified.",
        (_, "expired" or "abandoned") => "Your last check wasn't finished.",
        _ => "You haven't verified your ID.",
    };

    /// <summary>Why a check couldn't start.</summary>
    public static string VerificationProblem(string? code) => code switch
    {
        ProblemCodes.VerificationAttemptUsed => "You've used your ID check. Contact Swoovr support if you'd like another.",
        ProblemCodes.AlreadyVerified => "Your ID is already verified.",
        ProblemCodes.VerificationProviderFailed => "We couldn't start the ID check. Try again shortly.",
        _ => "ID verification isn't available right now.",
    };
}
