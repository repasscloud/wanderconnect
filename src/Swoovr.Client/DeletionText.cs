using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>
/// Member wording for data export and account deletion (D144, D170–D173), shared by the web and the app so both say the
/// same thing (as MemberText does, D163).
/// </summary>
public static class DeletionText
{
    public const string Title = "Your data and account";
    public const string ExportHeading = "Download your data";
    public const string ExportBody = "A file with your account, profile, preferences, connections, chats and reports you made. Other members appear by name only. The link works once, for 15 minutes; you can ask once a day.";
    public const string DeleteHeading = "Delete your account";
    public const string DeleteBody = "Your account is switched off at once: you leave discovery and are signed out everywhere. You have 30 days to change your mind; after that your profile, photos, chats, trips and connections are deleted for good, along with your sign-in.";
    public const string DeleteNowOption = "Delete everything now instead of waiting 30 days";
    public const string Kept = "Some records are kept for up to two years where safety or the law needs them: reports and moderation decisions about the account, and security logs. They hold ids, not your profile.";
    public const string FounderKept = "You're a founding member. We'll email your founder recovery code now: keep it to restore your founding status if you come back. The code's record is kept for that unless you tick the box below.";
    public const string FounderDeleteOption = "Also delete my founder recovery token (founding status can't be restored afterwards)";
    public const string Subscriptions = "No paid plans exist yet. Complimentary Plus ends when the account is deleted. Plans bought through the App Store or Google Play will need cancelling there.";
    public const string Confirm = "I understand that this deletes my Swoovr account";
    public const string SignInAgain = "For your security, sign in again first. It only takes a moment.";
    public const string TooSoon = "You can download your data once a day. Try again tomorrow.";
    public const string Unavailable = "That download link has expired or was already used. Ask for a new one.";
    public const string Failed = "Something went wrong. Try again.";

    public static string PendingTitle(DeletionView view) => view.DueAt is { } due && view.RequestedAt is { } asked && due <= asked
        ? "Your account is being deleted"
        : "Your account will be deleted";

    public static string PendingBody(DeletionView view) => view.DueAt is { } due
        ? (view.RequestedAt is { } asked && due <= asked ? "It will be deleted within the hour. Until then you can still cancel." : $"It will be deleted on {due.ToLocalTime():d MMMM yyyy}. Until then you can cancel and everything comes back.")
        : "Until it's deleted you can cancel and everything comes back.";

    /// <summary>The message for a refused export or deletion call; null when it's a stale sign-in (show the sign-in-again step).</summary>
    public static string? Refusal(string? code) => code switch
    {
        ProblemCodes.StaleSession => null,
        ProblemCodes.ExportTooSoon => TooSoon,
        ProblemCodes.ExportUnavailable => Unavailable,
        ProblemCodes.DeletionAlreadyPending => "Your account is already being deleted.",
        ProblemCodes.DeletionNotPending => "Your account isn't being deleted.",
        ProblemCodes.DeletionNotAllowed => "This account can't be deleted here. Contact Swoovr support.",
        _ => Failed,
    };
}
