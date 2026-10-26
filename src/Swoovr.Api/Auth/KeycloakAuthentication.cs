using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Swoovr.Domain;

namespace Swoovr.Api.Auth;

/// <summary>Keycloak realms trusted by the API (D31). Values come from configuration only.</summary>
public sealed class KeycloakOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8180";
    public string Audience { get; set; } = "swoovr-api";
    public string MembersRealm { get; set; } = "swoovr-members-dev";
    public string VenuesRealm { get; set; } = "swoovr-venues-dev";
    public string StaffRealm { get; set; } = "swoovr-staff-dev";
    public string? AppleAlias { get; set; }
    public string? GoogleAlias { get; set; }
    public string? StaffSsoAlias { get; set; }
    public bool RequireHttpsMetadata { get; set; } = true;

    public string Issuer(string realm) => BaseUrl.TrimEnd('/') + "/realms/" + realm;
    public IEnumerable<(string Scheme, AccountRealm Realm, string Issuer)> Realms() => [
        ("members", AccountRealm.Members, Issuer(MembersRealm)),
        ("venues", AccountRealm.Venues, Issuer(VenuesRealm)),
        ("staff", AccountRealm.Staff, Issuer(StaffRealm))];
}

/// <summary>Authenticates nobody; used when no trusted Keycloak issuer is configured.</summary>
public sealed class UnconfiguredHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
}

public static class KeycloakAuthentication
{
    public const string Scheme = "keycloak";

    public static IServiceCollection AddKeycloakAuthentication(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var options = configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();
        // Plain-HTTP discovery is a local development convenience only.
        if (!environment.IsDevelopment()) options.RequireHttpsMetadata = true;
        services.AddSingleton(options);
        services.AddSingleton(new SessionEvidencePolicy(new SessionEvidenceOptions(options.AppleAlias, options.GoogleAlias, options.StaffSsoAlias)));
        services.AddAuthorization();
        if (options.RequireHttpsMetadata && !options.BaseUrl.StartsWith("https://", StringComparison.Ordinal))
        {
            // Not configured for this environment: fail closed (401) instead of failing every request.
            services.AddAuthentication(Scheme).AddScheme<AuthenticationSchemeOptions, UnconfiguredHandler>(Scheme, null);
            return services;
        }
        var builder = services.AddAuthentication(Scheme).AddPolicyScheme(Scheme, "Keycloak realm selector", policy =>
        {
            // Routing only: the selected scheme still validates signature, issuer and audience.
            policy.ForwardDefaultSelector = context =>
            {
                var header = context.Request.Headers.Authorization.ToString();
                var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header["Bearer ".Length..].Trim() : HubToken(context.Request);
                if (token is null) return "members";
                var handler = new JsonWebTokenHandler();
                string issuer;
                try { issuer = handler.ReadJsonWebToken(token).Issuer; }
                catch (ArgumentException) { return "members"; } // Malformed: the members scheme rejects it with 401.
                return options.Realms().FirstOrDefault(r => r.Issuer == issuer).Scheme ?? "members";
            };
        });
        foreach (var (scheme, _, issuer) in options.Realms())
            builder.AddJwtBearer(scheme, jwt =>
            {
                jwt.Authority = issuer;
                jwt.Audience = options.Audience;
                jwt.RequireHttpsMetadata = options.RequireHttpsMetadata;
                jwt.MapInboundClaims = false;
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = issuer,
                    ValidAudience = options.Audience,
                    ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                    RequireSignedTokens = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "sub",
                };
                // D143: WebSocket clients can't always set headers, so the messaging hub (only) also takes the token from the query.
                jwt.Events = new JwtBearerEvents { OnMessageReceived = context => { context.Token ??= HubToken(context.Request); return Task.CompletedTask; } };
            });
        return services;
    }

    /// <summary>The access_token query value on the messaging hub's paths, and nowhere else.</summary>
    private static string? HubToken(HttpRequest request) =>
        request.Path.StartsWithSegments("/hubs/messages") && request.Query["access_token"].ToString() is { Length: > 0 } token ? token : null;

    /// <summary>Reads facts from a principal the JWT handler has already validated.</summary>
    public static ValidatedSessionFacts? Facts(ClaimsPrincipal user, KeycloakOptions options)
    {
        var issuer = user.FindFirstValue("iss");
        var realm = options.Realms().FirstOrDefault(r => r.Issuer == issuer).Realm;
        var subject = user.FindFirstValue("sub");
        if (realm == AccountRealm.Unknown || string.IsNullOrEmpty(subject)
            || !long.TryParse(user.FindFirstValue("auth_time"), out var authTime)) return null;
        var attested = long.TryParse(user.FindFirstValue("swoovr_adult_attested_at"), out var at) ? DateTimeOffset.FromUnixTimeSeconds(at) : (DateTimeOffset?)null;
        return new(realm, subject, user.FindFirstValue("email_verified") == "true",
            user.FindAll("amr").Select(c => c.Value).ToArray(), user.FindFirstValue("swoovr_idp"),
            DateTimeOffset.FromUnixTimeSeconds(authTime), attested);
    }
}
