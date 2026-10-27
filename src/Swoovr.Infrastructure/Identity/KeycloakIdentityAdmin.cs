using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Identity;

/// <summary>
/// The members realm's service-account client (Q61, D169): `swoovr-api-accounts`, holding only realm-management
/// `view-users` and `manage-users`. Unconfigured without a secret; deletion then can't finish and says so.
/// </summary>
public sealed record IdentityAdminOptions(string BaseUrl, string Realm, string ClientId, string? ClientSecret)
{
    public bool Configured => !string.IsNullOrWhiteSpace(ClientSecret);
}

/// <summary>
/// Keycloak's Admin REST API (keycloak.org/docs-api/latest/rest-api): a client-credentials token from the realm's
/// token endpoint, then POST /admin/realms/{realm}/users/{id}/logout and DELETE /admin/realms/{realm}/users/{id}.
/// The user id is the token's subject. Statuses only are logged: Keycloak's error bodies can echo user data.
/// </summary>
public sealed class KeycloakIdentityAdmin(HttpClient http, IdentityAdminOptions options, TimeProvider clock, ILogger<KeycloakIdentityAdmin> log) : IIdentityAdmin
{
    private string? token;
    private DateTimeOffset tokenExpires;
    private readonly SemaphoreSlim gate = new(1, 1);

    public bool Configured => options.Configured;

    public async Task<bool> SignOutEverywhereAsync(string subject, CancellationToken cancellationToken) =>
        Guid.TryParse(subject, out var id) && await SendAsync(HttpMethod.Post, $"users/{id}/logout", "logout", notFoundIsDone: true, cancellationToken);

    public async Task<bool> DeleteUserAsync(string subject, CancellationToken cancellationToken) =>
        !Guid.TryParse(subject, out var id) || await SendAsync(HttpMethod.Delete, $"users/{id}", "delete", notFoundIsDone: true, cancellationToken);

    private async Task<bool> SendAsync(HttpMethod method, string path, string what, bool notFoundIsDone, CancellationToken cancellationToken)
    {
        if (!Configured) return false;
        try
        {
            var bearer = await TokenAsync(cancellationToken);
            if (bearer is null) return false;
            using var request = new HttpRequestMessage(method, $"{options.BaseUrl.TrimEnd('/')}/admin/realms/{options.Realm}/{path}");
            request.Headers.Authorization = new("Bearer", bearer);
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || notFoundIsDone && response.StatusCode == HttpStatusCode.NotFound) return true;
            log.LogWarning("Keycloak {What} returned {Status}.", what, (int)response.StatusCode);
            if (response.StatusCode == HttpStatusCode.Unauthorized) token = null;
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("Keycloak {What} failed: {Kind}.", what, exception.GetType().Name);
            return false;
        }
    }

    private async Task<string?> TokenAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (token is not null && clock.GetUtcNow() < tokenExpires) return token;
            using var response = await http.PostAsync($"{options.BaseUrl.TrimEnd('/')}/realms/{options.Realm}/protocol/openid-connect/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials", ["client_id"] = options.ClientId, ["client_secret"] = options.ClientSecret!,
                }), cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Keycloak service token returned {Status}.", (int)response.StatusCode);
                return null;
            }
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            token = body.RootElement.GetProperty("access_token").GetString();
            // Renew 30 seconds early so a request never carries an expiring token.
            tokenExpires = clock.GetUtcNow().AddSeconds(Math.Max(0, body.RootElement.GetProperty("expires_in").GetInt32() - 30));
            return token;
        }
        finally { gate.Release(); }
    }
}
