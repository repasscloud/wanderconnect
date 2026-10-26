using System.Text.Json;
using Microsoft.AspNetCore.Authentication;

namespace Swoovr.Web.Auth;

/// <summary>
/// Owner answer to Q55 (D160): a restricted (or suspended or terminated) member can't use the app and sees the restricted
/// screen until the action is lifted. Before rendering a signed-in page this asks the API for the member's account state
/// (`/api/v1/me`, which stays open to barred members) and redirects to /account/restricted. The API refuses every other
/// member route on its own; this only decides what the browser shows. The account page, notices (to read and appeal),
/// sign-in and sign-out, and static files stay reachable.
/// </summary>
public static class RestrictionGate
{
    private static readonly string[] Open = ["/account", "/me/notices", "/bff", "/signin-oidc", "/signout", "/not-found", "/error", "/_framework", "/_content", "/settings"];

    public static void UseRestrictionGate(this WebApplication app) => app.Use(async (context, next) =>
    {
        var path = context.Request.Path.Value ?? "/";
        if (!HttpMethods.IsGet(context.Request.Method) || context.User.Identity?.IsAuthenticated != true || path.Contains('.')
            || Open.Any(o => path.Equals(o, StringComparison.OrdinalIgnoreCase) || path.StartsWith(o + "/", StringComparison.OrdinalIgnoreCase))
            || await context.GetTokenAsync("access_token") is not { } token)
        {
            await next();
            return;
        }
        var state = await StateAsync(context, token);
        if (state is "restricted" or "suspended" or "terminated") { context.Response.Redirect("/account/restricted?state=" + state); return; }
        await next();
    });

    private static async Task<string?> StateAsync(HttpContext context, string token)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/me");
            request.Headers.Authorization = new("Bearer", token);
            using var response = await context.RequestServices.GetRequiredService<IHttpClientFactory>().CreateClient(Bff.ApiClient).SendAsync(request, context.RequestAborted);
            if (!response.IsSuccessStatusCode) return null;
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(context.RequestAborted), cancellationToken: context.RequestAborted);
            return body.RootElement.TryGetProperty("accountState", out var s) ? s.GetString() : null;
        }
        // The page itself still works without the API (its panels show their own errors); the API refuses barred members anyway.
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException) { return null; }
    }
}
