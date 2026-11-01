using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Swoovr.Admin;

/// <summary>Renews server-held staff tokens without extending the absolute cookie or authentication freshness.</summary>
public sealed class StaffSessionRenewal(IMemoryCache cache, IOptionsMonitor<OpenIdConnectOptions> options)
{
    private readonly object sync = new();
    private sealed record Tokens(string Access, string Refresh, DateTimeOffset Expires);

    public async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var properties = context.Properties;
        if (!DateTimeOffset.TryParse(properties.GetTokenValue("expires_at"), CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out var expires))
        {
            await RejectAsync(context);
            return;
        }
        if (expires > DateTimeOffset.UtcNow.AddSeconds(30)) return;
        if (properties.GetTokenValue("refresh_token") is not { Length: > 0 } refresh)
        {
            await RejectAsync(context);
            return;
        }
        // Concurrent SSR requests must share one rotation, including when their tickets were read before it completed.
        // Cache only a digest as the key, and retain the result briefly to cover those in-flight requests.
        var key = "staff-refresh:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refresh)));
        Task<Tokens?> pending;
        lock (sync)
        {
            if (!cache.TryGetValue(key, out pending!))
            {
                pending = RefreshAsync(refresh);
                cache.Set(key, pending, TimeSpan.FromMinutes(2));
            }
        }
        var renewed = await pending;
        if (renewed is null || renewed.Expires <= DateTimeOffset.UtcNow)
        {
            await RejectAsync(context);
            return;
        }
        lock (sync)
        {
            // The in-memory ticket is shared: a later rotation must never be overwritten by an older response.
            if (properties.GetTokenValue("refresh_token") != refresh) return;
            properties.UpdateTokenValue("access_token", renewed.Access);
            properties.UpdateTokenValue("refresh_token", renewed.Refresh);
            properties.UpdateTokenValue("expires_at", renewed.Expires.ToString("o", CultureInfo.InvariantCulture));
            // Cookie middleware derives renewal duration from IssuedUtc/ExpiresUtc. Keep the original deadline.
            properties.IssuedUtc = DateTimeOffset.UtcNow;
            context.ShouldRenew = true;
        }
    }

    private async Task<Tokens?> RefreshAsync(string refresh)
    {
        try
        {
            var oidc = options.Get(OpenIdConnectDefaults.AuthenticationScheme);
            if (oidc.ConfigurationManager is null) return null;
            // Complete a shared rotation even if one requesting browser disconnects.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var metadata = await oidc.ConfigurationManager.GetConfigurationAsync(timeout.Token);
            using var response = await oidc.Backchannel.PostAsync(metadata.TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token", ["refresh_token"] = refresh,
                ["client_id"] = oidc.ClientId!, ["client_secret"] = oidc.ClientSecret!,
            }), timeout.Token);
            if (!response.IsSuccessStatusCode) return null;
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var root = body.RootElement;
            if (!root.TryGetProperty("access_token", out var access) || access.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(access.GetString())
                || !root.TryGetProperty("refresh_token", out var next) || next.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(next.GetString())
                || !root.TryGetProperty("expires_in", out var lifetime) || !lifetime.TryGetInt32(out var seconds) || seconds <= 0) return null;
            return new(access.GetString()!, next.GetString()!, DateTimeOffset.UtcNow.AddSeconds(seconds));
        }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
