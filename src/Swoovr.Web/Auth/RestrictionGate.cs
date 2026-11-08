using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Swoovr.Web.Auth;

/// <summary>
/// D325 (Q132 B, supersedes D160): a suspended or terminated member can't use the app (a restriction only pauses the features it names,
/// and a sign-in lock shows its own screen) and sees the restricted
/// screen until the action is lifted. Before rendering a signed-in page this asks the API for the member's account state
/// (`/api/me`, which stays open to barred members) and redirects to /account/restricted. The API refuses every other
/// member route on its own; this only decides what the browser shows. The account page, notices (to read and appeal), notifications, account standing (#49, #50), support requests,
/// sign-in and sign-out, and static files stay reachable. An account pending deletion (D170) goes to /account/deletion instead.
/// </summary>
public static class RestrictionGate
{
    private static readonly string[] Open = ["/safety", "/me/meeting-safety", "/account", "/me/notices", "/me/notifications", "/me/standing", "/me/support", "/bff", "/signin-oidc", "/signout", "/not-found", "/error", "/_framework", "/_content", "/settings", "/welcome"];

    /// <summary>
    /// Issue 254 (D342): while names, mobile or date of birth are unfinished, every other signed-in page goes to the
    /// onboarding screens at /welcome, including the account page itself. Its sub-routes (sign-in and sign-out, data,
    /// deletion, policies, the restricted screen), notices, support, safety and the public policies stay reachable.
    /// </summary>
    private static readonly string[] SetupOpen = ["/welcome", "/safety", "/me/notices", "/me/notifications", "/me/standing", "/me/support", "/bff", "/signin-oidc", "/signout", "/not-found", "/error", "/_framework", "/_content", "/terms", "/privacy", "/community-guidelines", "/settings/appearance"];

    /// <summary>The setup steps that keep a member in onboarding; required policies keep their own page (/account/policies).</summary>
    public static bool Onboarding(string? setupStep) => setupStep is "names" or "phone" or "dateOfBirth";

    private static bool Matches(string path, IEnumerable<string> prefixes) =>
        prefixes.Any(o => path.Equals(o, StringComparison.OrdinalIgnoreCase) || path.StartsWith(o + "/", StringComparison.OrdinalIgnoreCase));

    public static bool SetupAllows(string path) => Matches(path, SetupOpen) || path.StartsWith("/account/", StringComparison.OrdinalIgnoreCase);

    public static void UseRestrictionGate(this WebApplication app) => app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "/";
        var restrictionOpen = Matches(path, Open);
        if (!HttpMethods.IsGet(context.Request.Method) || context.User.Identity?.IsAuthenticated != true || path.Contains('.')
            || restrictionOpen && SetupAllows(path)
            || await context.GetTokenAsync("access_token") is not { } token)
        {
            await next();
            return;
        }
        var (state, setupStep, reference, refused) = await StateAsync(context, token);
        // #201: a session cutoff retired this session's token; end it here and sign in again (then the real state shows).
        if (refused) { context.Response.Redirect(await RetiredSessions.EndAsync(context)); return; }
        if (restrictionOpen)
        {
            // Only the onboarding check applies on these pages (the account page itself and settings).
            if (state is "active" or "restricted" && Onboarding(setupStep)) { context.Response.Redirect("/welcome"); return; }
            await next();
            return;
        }
        if(state=="mfa-required"){context.Response.Redirect("/account#mfa");return;}
        // D325 (Q132 B): only suspension and termination take over the app; a restriction limits named features in place.
        if (state is "suspended" or "terminated") { context.Response.Redirect("/account/restricted?state=" + state); return; }
        // issue 190 §10 (D323): a sign-in lock shows its own screen with the member's public reference.
        if (state == "locked") { context.Response.Redirect("/account/restricted?state=locked" + (reference is { } r ? "&ref=" + Uri.EscapeDataString(r) : "")); return; }
        // D170: while deletion is pending only the deletion screen (under /account) is open.
        if (state == "pendingDeletion") { context.Response.Redirect("/account/deletion"); return; }
        // D148, D300, D139 (issue 254, D342): unfinished setup goes to the onboarding screens, after every state above.
        if (Onboarding(setupStep)) { context.Response.Redirect("/welcome"); return; }
        await next();
    });

    private static async Task<(string? State, string? SetupStep, string? Reference, bool Refused)> StateAsync(HttpContext context, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/me");
            request.Headers.Authorization = new("Bearer", token);
            using var response = await context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(Bff.ApiClient).SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode) return (null, null, null, response.StatusCode == System.Net.HttpStatusCode.Unauthorized);
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(context.RequestAborted), cancellationToken: context.RequestAborted);
            if(body.RootElement.TryGetProperty("mfaAccessRequired",out var mfa)&&mfa.ValueKind==JsonValueKind.True)return("mfa-required",null,null,false);
            return (body.RootElement.TryGetProperty("accountState", out var s) ? s.GetString() : null,
                body.RootElement.TryGetProperty("setupStep", out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null,
                body.RootElement.TryGetProperty("statusReference", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null, false);
        }
        // The page itself still works without the API (its panels show their own errors); the API refuses barred members anyway.
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException) { return (null, null, null, false); }
    }
}
