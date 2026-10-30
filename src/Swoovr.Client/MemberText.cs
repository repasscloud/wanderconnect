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
        ProblemCodes.PhoneVerificationRequired => "Verify your mobile number under Me to keep using Swoovr.",
        ProblemCodes.AccountDeletionPending => "Your account is being deleted. You can cancel under Me → Your data and account.",
        _ => null,
    };

    /// <summary>The restricted screen's heading for an account state (D160); null when the account is active.</summary>
    public static string? RestrictedTitle(string accountState) => accountState switch
    {
        "active" => null,
        "suspended" => "Your account is suspended",
        "terminated" => "Your account has been closed",
        "pendingDeletion" => "Your account is being deleted",
        _ => "Your account is restricted",
    };

    /// <summary>D148: the phone step's words, shared by the web and the app.</summary>
    public const string PhoneIntro = "We text a six-digit code to check your mobile number. Your number is never shown to other members.";
    public const string PhoneSimulated = "Test mode: no text message is sent. The code goes to the local development mail sink.";

    public static string PhoneProblem(string? code) => code switch
    {
        ProblemCodes.InvalidPhone => "Enter a mobile number, for example 0412 345 678 or +61 412 345 678.",
        ProblemCodes.PhoneCodeCooldown => "Wait a minute before asking for another code.",
        ProblemCodes.PhoneCodeLimit => "Too many codes. Try again in an hour.",
        ProblemCodes.PhoneCodeWrong => "That code isn't right. Check it and try again.",
        ProblemCodes.PhoneCodeExpired => "That code has expired. Ask for a new one.",
        ProblemCodes.TooManyAttempts => "Too many wrong codes. Ask for a new one.",
        ProblemCodes.PhoneInUse => "That number can't be used. Try another.",
        ProblemCodes.PhoneAlreadyVerified => "That number is already verified.",
        ProblemCodes.PhoneSendFailed => "We couldn't send a code just now. Try again.",
        _ => "That didn't work. Try again.",
    };

    /// <summary>What a connection is called in each mode (D176); the rules are the same in every mode for now.</summary>
    public static string ConnectionLabel(string modeId) => modeId switch
    {
        "dating" => "Match", "friends" => "Friend", "activities" => "Activity partner", "business" => "Contact", _ => "Connection",
    };

    /// <summary>D176: what disconnecting does, shown before the member confirms.</summary>
    public const string DisconnectExplained = "You'll no longer be connected in this mode. They aren't told, and they can't ask again here for 30 days. If no other mode connects you, your chat closes and stays readable.";

    public const string RestrictedBody = "You can't use Swoovr until this is lifted. Read the notice to see why; you can appeal within 30 days.";

    /// <summary>D170: a pending deletion says how to change your mind instead of pointing at a notice.</summary>
    public static string RestrictedBodyFor(string? accountState) => accountState == "pendingDeletion"
        ? "You can still change your mind: open Your data and account to cancel."
        : RestrictedBody;

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
        "contentRemoved" => "Content removed by Swoovr",
        "sanctionLifted" => "An action on your account was lifted",
        _ => "Notice",
    };

    /// <summary>What a member sees of their own report (D131): its review status.</summary>
    public static string ReportStatus(string status) => status switch { "submitted" => "Submitted", "underReview" => "Under review", "reviewed" => "Reviewed", _ => "Closed" };

    /// <summary>D190: the general outcome of a closed report, never which action.</summary>
    public static string ReportLine(MyReportView report) =>
        $"{report.CreatedAt:d MMM yyyy}{(report.Serious ? " (serious incident)" : "")}: {ReportStatus(report.Status)}" + report.Outcome switch
        {
            "actionTaken" => ". We took action on this report.",
            "noBreachFound" => ". We reviewed it and didn't find a breach of our rules.",
            _ => "",
        };

    /// <summary>
    /// D190 (policy §9): shown before the Serious Safety Incident form, on the web and in the app. Australian services
    /// only for the Adelaide beta; the app makes the numbers tap-to-call. Swoovr never contacts police for the member.
    /// </summary>
    /// <summary>#103 (D204): the country's contacts come from the API (SafetyText); this line never names a number.</summary>
    public const string ImmediateDanger = "If you're in immediate danger, contact your local emergency services now.";
    public const string SeriousPromise = "We'll preserve what's on Swoovr about this, review it urgently, and won't tell the other person you reported them. We don't contact police for you, but we will help police if they ask.";

    public static string PoliceAnswer(string answer) => answer switch
    {
        "contacted" => "I've contacted police",
        "will-contact" => "I want to contact police",
        _ => "Not now",
    };

    /// <summary>D190: how someone the member dealt with stands now.</summary>
    public static string SafetyState(string state) => state switch
    {
        "connected" => "Connected", "pending" => "Request pending", "blocked" => "Blocked", "disconnected" => "Disconnected",
        "declined" => "Declined", "deleted" => "Account deleted", _ => "No longer connected",
    };

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
