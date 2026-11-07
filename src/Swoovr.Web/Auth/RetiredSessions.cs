using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Swoovr.Contracts;

namespace Swoovr.Web.Auth;

/// <summary>
/// #201: the API refused this web session's token (401) although the session is still signed in here, because a session cutoff
/// retired it (sign out everywhere, a sign-in lock, a contact retirement). The local session ends at once instead of
/// lingering until its token next fails to refresh; the member signs in again and the API then shows the account's real
/// state (for a lock, the D323 locked screen and reference).
/// </summary>
public static class RetiredSessions
{
    /// <summary>Ends the local session and returns where the member goes next (<see cref="RetiredSession.Next"/>).</summary>
    public static async Task<string> EndAsync(HttpContext context)
    {
        var next = RetiredSession.Next(context.User.FindFirst("auth_time")?.Value, DateTimeOffset.UtcNow);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        context.Response.Headers.CacheControl = "no-store";
        return next;
    }
}
