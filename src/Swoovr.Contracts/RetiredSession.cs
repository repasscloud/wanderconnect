using System.Globalization;

namespace Swoovr.Contracts;

/// <summary>
/// #201: what the member web does when the API refuses the token of a web session that is still signed in (sign out
/// everywhere, a sign-in lock or another session cutoff retired it). The web host ends its own session and the member signs
/// in again; after a fresh sign-in the API answers with the account's real state, so a locked member sees the D323 locked
/// screen and reference instead of "We couldn't load your account".
/// </summary>
public static class RetiredSession
{
    /// <summary>Set by the backend-for-frontend on the 401 when it has ended the web session; the value is where to go next.</summary>
    public const string Header = "X-Swoovr-Session-Ended";
    public const string SignInPath = "/account/sign-in";
    public const string HomePath = "/";

    /// <summary>
    /// A session that signed in within this window and is already refused isn't sent straight back to sign-in: something other
    /// than a cutoff is refusing it, and an automatic sign-in would loop. It goes home, where the member can sign in.
    /// </summary>
    public static readonly TimeSpan FreshSignIn = TimeSpan.FromMinutes(1);

    /// <summary>Where to send the member, from the session's `auth_time` claim (seconds since 1970).</summary>
    public static string Next(string? authTime, DateTimeOffset now)
    {
        if (!long.TryParse(authTime, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0 || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return HomePath;
        return now - DateTimeOffset.FromUnixTimeSeconds(seconds) < FreshSignIn ? HomePath : SignInPath;
    }

    /// <summary>Only the two local destinations are followed; anything else in the header is ignored.</summary>
    public static string? Destination(string? header) => header is SignInPath or HomePath ? header : null;
}
