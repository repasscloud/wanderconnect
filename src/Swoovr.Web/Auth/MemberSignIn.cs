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
    private const string KeycloakActionItem = "swoovr.kc_action";
    /// <summary>Allow-listed Keycloak application-initiated actions (issue 180, Q126 A).</summary>
    public static readonly IReadOnlyDictionary<string, string> KeycloakActions = new Dictionary<string, string>
    {
        ["password"] = "UPDATE_PASSWORD",
        ["passkey"] = "webauthn-register-passwordless",
        // "email" (UPDATE_EMAIL) is added once the realm enables update-email (Q126).
    };
    /// <summary>Session cookie marking that this browser session has seen the launch splash.</summary>
    public const string IntroCookie = "swoovr.intro";
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
            // A sign-in that can't be finished here (most often Keycloak's email-first registration completed from the
            // verification link in another browser or after the 15-minute correlation cookie expired) goes back to the
            // account page to sign in again, instead of an exception page. The Keycloak account itself already exists.
            options.Events.OnRemoteFailure = context =>
            {
                context.Response.Redirect("/account?signin=retry");
                context.HandleResponse();
                return Task.CompletedTask;
            };
            // issue 180 (Q126 A): Security & access opens Keycloak's own secure steps (application-initiated actions). Only
            // the allow-listed actions set by /account/action/{kind} are forwarded; nothing comes from the query string.
            options.Events.OnRedirectToIdentityProvider = context =>
            {
                if (context.Properties.Items.TryGetValue(KeycloakActionItem, out var action) && action is not null && KeycloakActions.Values.Contains(action))
                    context.ProtocolMessage.SetParameter("kc_action", action);
                return Task.CompletedTask;
            };
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

    public static void UseAccountEntry(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value?.TrimEnd('/') ?? "";
            var publicPage = path is "" or "/terms" or "/community-guidelines" or "/privacy" or "/safety/advice" or "/safety";
            // Protocol operations and static assets are not navigable guest pages. BFF retains its own 401/CSRF gate.
            var protocol = path is "/account/sign-in" or "/account/create" or "/account/sign-out"
                or "/signin-oidc" or "/signout-callback-oidc" or "/signout-oidc"
                or "/health/live" or "/health/ready";
            var resource = context.GetEndpoint()?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() is not null
                || context.Request.Path.StartsWithSegments("/bff");
            if (context.User.Identity?.IsAuthenticated != true && !publicPage && !protocol && !resource)
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Redirect("/");
                return;
            }
            await next(context);
        });
    }

    public static void MapMemberSignIn(WebApplication app)
    {
        var configured = IsConfigured(app.Configuration);
        app.MapGet("/account/sign-in", (HttpContext context) =>
        {
            if (!configured) return Results.Redirect("/");
            // Launch splash (docs/design/reference/UI-STYLE.md): the first sign-in visit of a browser
            // session plays it here, before the hand-off, because Keycloak pages can't run it between
            // redirects. The page returns with intro=done; the cookie stops replays on later visits.
            if (!context.Request.Cookies.ContainsKey(IntroCookie) && context.Request.Query["intro"] != "done")
            {
                var launch = app.Environment.WebRootFileProvider.GetFileInfo("launch/index.html");
                if (launch.Exists)
                {
                    context.Response.Cookies.Append(IntroCookie, "1", new CookieOptions
                    {
                        HttpOnly = true, SameSite = SameSiteMode.Lax, Path = "/account", IsEssential = true,
                        Secure = !app.Environment.IsDevelopment() || context.Request.IsHttps,
                    });
                    context.Response.Headers.CacheControl = "no-store";
                    return Results.Stream(launch.CreateReadStream(), "text/html; charset=utf-8");
                }
            }
            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/explore" }, [OpenIdConnectDefaults.AuthenticationScheme]);
        });
        app.MapGet("/account/create", async (HttpContext context,IHttpClientFactory clients) =>
        {
            if (!configured) return Results.Redirect("/");
            context.Response.Headers.CacheControl="no-store";
            Swoovr.Contracts.RegistrationAvailabilityView? availability=null;
            try
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);deadline.CancelAfter(TimeSpan.FromSeconds(5));
                using var response=await clients.CreateClient(Bff.ApiClient).GetAsync("api/registration",deadline.Token);
                if(response.IsSuccessStatusCode)availability=await response.Content.ReadFromJsonAsync<Swoovr.Contracts.RegistrationAvailabilityView>(deadline.Token);
            }
            catch(Exception failure)when(failure is HttpRequestException or OperationCanceledException or JsonException){ }
            if(availability is not{Revision:>=0}||!availability.PublicEnabled&&!(availability.BetaEnabled&&context.Request.Query["invited"]=="true"))
            {
                // issue 178: same Swoovr layout and components as the rest of the site.
                return Results.Content(InviteOnlyPage.Html(availability is{BetaEnabled:true},SwoovrBuild.Display),"text/html",statusCode:availability is null?503:200);
            }
            var properties = new AuthenticationProperties { RedirectUri = "/explore" };
            properties.Parameters["prompt"] = "create";
            return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
        });
        // D169: deletion, cancelling it and the data export need a sign-in within five minutes. This asks Keycloak to sign the
        // member in again (prompt=login: password, passkey or a linked Apple/Google identity) and returns to a local page.
        app.MapGet("/account/reauthenticate", (string? returnUrl) =>
        {
            if (!configured) return Results.Redirect("/");
            var target = returnUrl is { Length: > 1 } url && url[0] == '/' && url[1] != '/' && url[1] != '\\' ? url : "/account";
            var properties = new AuthenticationProperties { RedirectUri = target };
            properties.Parameters["prompt"] = "login";
            properties.Parameters["max_age"] = "0";
            return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
        });
        // issue 180: Keycloak-hosted security steps for a signed-in member, then back to Account settings. Keycloak itself
        // re-checks the member's sign-in before any change; the web app never sees passwords or credentials.
        app.MapGet("/account/action/{kind}", (string kind, HttpContext context) =>
        {
            if (!configured || context.User.Identity?.IsAuthenticated != true || !KeycloakActions.TryGetValue(kind, out var action))
                return Results.Redirect("/account");
            var properties = new AuthenticationProperties { RedirectUri = "/account?updated=" + kind + "#security" };
            properties.Items[KeycloakActionItem] = action;
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
