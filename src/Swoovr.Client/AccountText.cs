using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>
/// Wording for the signed-in account screen, shared by the web page (Web.Client) and the app (Mobile) so both say
/// the same thing about the same API answer. Labels only: the API decides badges, beta and access (D55).
/// </summary>
public static class AccountText
{
    public const string BetaUrl = "https://swoovr.com/beta/";

    /// <summary>D77: the same words whatever the reason, so the screen never says whether an address was ever enrolled.</summary>
    public static bool BetaBlocked(SessionSummary? session) => session?.Beta is "notEnrolled" or "unavailable";

    public static string BadgeKind(string badge) => badge switch
    {
        "founder" => "founding", "photoVerified" => "photo", "idVerified" => "id", "staff" => "staff", _ => "bot",
    };

    public static string BadgeLabel(string badge) => badge switch
    {
        "founder" => "Swoovr Founder", "photoVerified" => "Photo verified", "idVerified" => "ID verified",
        "staff" => "Swoovr staff", _ => "Swoovr guide (bot)",
    };

    public static string MethodLabel(string method) => method switch
    {
        // Keycloak 26.8.1 reports a passkey used on the sign-in form as the password method.
        "emailPassword" => "Password or passkey",
        "passkey" => "Passkey",
        "emailLink" => "Email sign-in link",
        "apple" => "Apple",
        "google" => "Google",
        // Keycloak records no method for the session it starts right after email-first sign-up.
        _ => "New account (just created)",
    };

    public static string AgeSaved(MemberAccount? account) =>
        account?.Age is { } years ? $"Saved. Your age shows as {years}." : "Saved. No age is shown.";

    public const string InvalidAge = "Check the date or age: it can't be in the future or over 120.";

    /// <summary>Result kind (for tests and markup hooks) and message for a coupon attempt.</summary>
    public static (string Result, string Message) Coupon(ApiResult<MemberAccount> result) => result.Code switch
    {
        null when result.Value is not null => ("ok", result.Value.Badges.Any(b => b.Badge == "founder") ? "Your founding status is restored." : "Code applied."),
        ProblemCodes.TooManyAttempts => ("limited", "Too many attempts. Try again in an hour."),
        _ => ("invalid", "That code can't be used."),
    };
}
