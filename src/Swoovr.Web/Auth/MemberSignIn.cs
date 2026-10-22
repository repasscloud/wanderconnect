using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Swoovr.Web.Auth;

/// <summary>
/// Member sign-in against the Keycloak members realm (D31): authorization code + PKCE
/// with a confidential client. Tokens stay on the server; the browser cookie holds only
/// a session key. Disabled unless Keycloak:Authority and Keycloak:ClientSecret are set.
/// </summary>
public static class MemberSignIn
{
    public const string Cookie = "swoovr.member";
    public static bool IsConfigured(IConfiguration configuration) =>
        !string.IsNullOrWhiteSpace(configuration["Keycloak:Authority"]) && !string.IsNullOrWhiteSpace(configuration["Keycloak:ClientSecret"]);

    public static IServiceCollection AddMemberSignIn(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddMemoryCache();
        services.AddSingleton<MemoryTicketStore>();
        var auth = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        });
        // Plain HTTP is accepted only for the loopback development sandbox.
        var cookieSecurity = environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        auth.AddCookie(options =>
        {
            options.Cookie.Name = Cookie;
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = cookieSecurity;
            options.ExpireTimeSpan = TimeSpan.FromHours(12);
            options.SlidingExpiration = false;
            options.Events.OnValidatePrincipal = RefreshWhenExpiring;
        });
        auth.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<MemoryTicketStore>((options, store) => options.SessionStore = store);
        if (!IsConfigured(configuration)) return services;
        auth.AddOpenIdConnect(options =>
        {
            options.Authority = configuration["Keycloak:Authority"];
            options.ClientId = configuration["Keycloak:ClientId"] ?? "swoovr-web";
            options.ClientSecret = configuration["Keycloak:ClientSecret"];
            options.RequireHttpsMetadata = !environment.IsDevelopment() || configuration.GetValue("Keycloak:RequireHttpsMetadata", true);
            options.ResponseType = OpenIdConnectResponseType.Code;
            // Query response avoids cross-site form posts, so correlation cookies can stay Lax.
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.UsePkce = true;
            options.SaveTokens = true;
            options.MapInboundClaims = false;
            options.GetClaimsFromUserInfoEndpoint = false;
            options.TokenValidationParameters.NameClaimType = "given_name";
            options.Scope.Clear();
            foreach (var scope in new[] { "openid", "profile", "email" }) options.Scope.Add(scope);
            foreach (var cookie in new[] { options.CorrelationCookie, options.NonceCookie })
            {
                cookie.SameSite = SameSiteMode.Lax;
                cookie.SecurePolicy = cookieSecurity;
            }
        });
        return services;
    }

    public static void MapMemberSignIn(WebApplication app)
    {
        var configured = IsConfigured(app.Configuration);
        app.MapGet("/account/sign-in", (HttpContext context) => configured
            ? Results.Challenge(new AuthenticationProperties { RedirectUri = "/account" }, [OpenIdConnectDefaults.AuthenticationScheme])
            : Results.Redirect("/account"));
        app.MapGet("/account/create", (HttpContext context) =>
        {
            if (!configured) return Results.Redirect("/account");
            var properties = new AuthenticationProperties { RedirectUri = "/account" };
            // Keycloak's registration endpoint shares the authorization request (prompt=create).
            properties.Parameters["prompt"] = "create";
            return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
        });
        // POST with antiforgery: a cross-site link or image cannot sign someone out.
        // Keycloak logout runs first so it can still read the id_token hint from the ticket.
        app.MapPost("/account/sign-out", () => Results.SignOut(new AuthenticationProperties { RedirectUri = "/" }, configured
            ? [OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme]
            : [CookieAuthenticationDefaults.AuthenticationScheme]));
    }

    /// <summary>Refreshes the access token shortly before expiry; any failure ends the session.</summary>
    private static async Task RefreshWhenExpiring(CookieValidatePrincipalContext context)
    {
        var expiresAt = context.Properties.GetTokenValue("expires_at");
        if (!DateTimeOffset.TryParse(expiresAt, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var expiry))
        {
            context.RejectPrincipal();
            return;
        }
        if (expiry > DateTimeOffset.UtcNow.AddSeconds(30)) return;
        var refreshToken = context.Properties.GetTokenValue("refresh_token");
        var oidc = context.HttpContext.RequestServices.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpenIdConnectOptions>>()
            .Get(OpenIdConnectDefaults.AuthenticationScheme);
        if (string.IsNullOrEmpty(refreshToken) || oidc.ConfigurationManager is null)
        {
            context.RejectPrincipal();
            return;
        }
        var metadata = await oidc.ConfigurationManager.GetConfigurationAsync(context.HttpContext.RequestAborted);
        using var response = await oidc.Backchannel.PostAsync(metadata.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = oidc.ClientId!,
            ["client_secret"] = oidc.ClientSecret!,
        }), context.HttpContext.RequestAborted);
        if (!response.IsSuccessStatusCode)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
        var root = body.RootElement;
        context.Properties.UpdateTokenValue("access_token", root.GetProperty("access_token").GetString()!);
        // Keycloak rotates refresh tokens (revokeRefreshToken); the old one is now invalid.
        context.Properties.UpdateTokenValue("refresh_token", root.GetProperty("refresh_token").GetString()!);
        context.Properties.UpdateTokenValue("expires_at", DateTimeOffset.UtcNow.AddSeconds(root.GetProperty("expires_in").GetInt32()).ToString("o", CultureInfo.InvariantCulture));
        context.ShouldRenew = true;
    }
}

/// <summary>
/// Server-side authentication tickets keyed by a random id held in the cookie.
/// Process memory only: a restart signs everyone out. A shared store is needed before
/// running more than one web instance.
/// </summary>
public sealed class MemoryTicketStore(IMemoryCache cache) : ITicketStore
{
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        Put(key, ticket);
        return Task.FromResult(key);
    }
    public Task RenewAsync(string key, AuthenticationTicket ticket) { Put(key, ticket); return Task.CompletedTask; }
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult(cache.Get<AuthenticationTicket>(key));
    public Task RemoveAsync(string key) { cache.Remove(key); return Task.CompletedTask; }
    private void Put(string key, AuthenticationTicket ticket) =>
        cache.Set(key, ticket, new MemoryCacheEntryOptions { AbsoluteExpiration = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(12) });
}
