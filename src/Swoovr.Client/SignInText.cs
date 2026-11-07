using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>#185 (D341): words for Security &amp; access → Active sessions and Linked sign-ins, shared by every client.</summary>
public static class SignInText
{
    public static string Device(string device) => device switch
    {
        "web" => "Web browser",
        "app" => "Swoovr app",
        _ => "Other sign-in",
    };

    /// <summary>"Active now" within five minutes, then minutes, hours, "yesterday", or the date.</summary>
    public static string LastActive(DateTimeOffset lastActive, DateTimeOffset now)
    {
        var age = now - lastActive;
        if (age < TimeSpan.FromMinutes(5)) return "Active now";
        if (age < TimeSpan.FromHours(1)) return $"Active {(int)age.TotalMinutes} minutes ago";
        if (age < TimeSpan.FromHours(2)) return "Active 1 hour ago";
        if (age < TimeSpan.FromDays(1)) return $"Active {(int)age.TotalHours} hours ago";
        if (age < TimeSpan.FromDays(2)) return "Active yesterday";
        return "Active " + lastActive.ToString("d MMM yyyy");
    }

    public static string Started(DateTimeOffset started) => "Signed in " + started.ToString("d MMM yyyy");

    public const string NoLinked = "No Apple or Google sign-in is linked to this account.";
    public const string OnlyMethod = "This is your only way to sign in. Add a password or passkey before removing it.";
    public const string Unknown = "We can't tell which session is this device. Sign in again to manage your sessions.";

    public static string RemoveConfirm(string label) =>
        $"Remove {label} sign-in? You won't be able to sign in with {label} until you link it again.";

    public static string Problem(string? code) => code switch
    {
        ProblemCodes.LastSignInMethod => OnlyMethod,
        ProblemCodes.CurrentSession => "That's this device. Use Sign out under Account management instead.",
        ProblemCodes.NotConfigured => "Session details aren't available right now.",
        _ => "That didn't go through. Try again in a moment.",
    };
}
