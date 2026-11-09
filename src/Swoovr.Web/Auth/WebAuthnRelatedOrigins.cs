namespace Swoovr.Web.Auth;

/// <summary>D355 keeps the UAT passkey RP while authentication moves to a sibling host.</summary>
public static class WebAuthnRelatedOrigins
{
    public static void Map(WebApplication app)
    {
        // Exact configuration and request host guards prevent UAT trust leaking into production.
        var enabled = app.Configuration["Hosting:PublicOrigin"]?.TrimEnd('/') == "https://uat.swoovr.app"
            && app.Configuration["Keycloak:Authority"]?.TrimEnd('/') == "https://auth-uat.swoovr.app/realms/swoovr-members";
        app.MapGet("/.well-known/webauthn", (HttpContext context) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            var origins = enabled && string.Equals(context.Request.Host.Value, "uat.swoovr.app", StringComparison.OrdinalIgnoreCase)
                ? new[] { "https://auth-uat.swoovr.app" } : Array.Empty<string>();
            return Results.Json(new { origins }, statusCode: origins.Length == 1 ? 200 : 404);
        }).AllowAnonymous();
    }
}
