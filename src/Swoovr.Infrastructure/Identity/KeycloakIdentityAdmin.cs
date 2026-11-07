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
public partial class KeycloakIdentityAdmin(HttpClient http, IdentityAdminOptions options, TimeProvider clock, ILogger<KeycloakIdentityAdmin> log, ContactPolicyContract? contactPolicy = null) : IIdentityAdmin, IContactIdentityAdmin, IIdentityDirectory
{
    private string? token;
    private DateTimeOffset tokenExpires;
    private readonly SemaphoreSlim gate = new(1, 1);

    public bool Configured => options.Configured;

    public async Task<bool> SignOutEverywhereAsync(string subject, CancellationToken cancellationToken) =>
        Guid.TryParse(subject, out var id) && await SendAsync(HttpMethod.Post, $"users/{id}/logout", "logout", notFoundIsDone: true, cancellationToken);

    /// <summary>GET /admin/realms/{realm}/users/{id}/sessions, then DELETE /admin/realms/{realm}/sessions/{session} for each other one.</summary>
    public async Task<bool> SignOutOtherSessionsAsync(string subject, string? keepSessionId, CancellationToken cancellationToken)
    {
        if (!Configured || !Guid.TryParse(subject, out var id)) return false;
        try
        {
            var bearer = await TokenAsync(cancellationToken);
            if (bearer is null) return false;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl.TrimEnd('/')}/admin/realms/{options.Realm}/users/{id}/sessions");
            request.Headers.Authorization = new("Bearer", bearer);
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) { log.LogWarning("Keycloak sessions returned {Status}.", (int)response.StatusCode); return false; }
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            var ok = true;
            foreach (var session in body.RootElement.EnumerateArray())
                // Keycloak 26 session ids are URL-safe tokens, not GUIDs; anything else is skipped and reported as not done.
                if (session.TryGetProperty("id", out var sid) && sid.GetString() is { } other && other != keepSessionId)
                    ok &= SessionId().IsMatch(other)
                        && await SendAsync(HttpMethod.Delete, $"sessions/{other}", "session logout", notFoundIsDone: true, cancellationToken);
            return ok;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("Keycloak sessions failed: {Kind}.", exception.GetType().Name);
            return false;
        }
    }

    public async Task<bool> DeleteUserAsync(string subject, CancellationToken cancellationToken) =>
        !Guid.TryParse(subject, out var id) || await SendAsync(HttpMethod.Delete, $"users/{id}", "delete", notFoundIsDone: true, cancellationToken);

    /// <summary>
    /// #185: GET users/{id}/sessions, users/{id}/federated-identity and users/{id}/credentials (realm-management view-users).
    /// Only session ids, times and client ids, provider aliases and credential types are kept: never addresses, provider user
    /// ids or names, credential data or labels.
    /// </summary>
    public async Task<IdentitySignIns?> SignInsAsync(string subject, CancellationToken cancellationToken)
    {
        if (!Configured || !Guid.TryParse(subject, out var id)) return null;
        using var sessions = await GetAsync($"users/{id}/sessions", "sessions", cancellationToken);
        using var linked = sessions is null ? null : await GetAsync($"users/{id}/federated-identity", "linked sign-ins", cancellationToken);
        using var credentials = linked is null ? null : await GetAsync($"users/{id}/credentials", "credential types", cancellationToken);
        if (sessions is null || linked is null || credentials is null) return null;
        try
        {
            var open = new List<IdentitySession>();
            foreach (var session in sessions.RootElement.EnumerateArray())
            {
                if (session.TryGetProperty("id", out var sid) && sid.GetString() is { } sessionId && SignInPolicy.ValidSessionId(sessionId))
                {
                    var clients = session.TryGetProperty("clients", out var map) && map.ValueKind == JsonValueKind.Object
                        ? map.EnumerateObject().Select(c => c.Value.GetString()).OfType<string>().ToList() : [];
                    open.Add(new(sessionId, Millis(session, "start"), Millis(session, "lastAccess"), clients));
                }
            }
            var providers = linked.RootElement.EnumerateArray()
                .Select(l => l.TryGetProperty("identityProvider", out var alias) ? alias.GetString() : null)
                .Where(SignInPolicy.ValidProvider).OfType<string>().Distinct().ToList();
            var types = credentials.RootElement.EnumerateArray()
                .Select(c => c.TryGetProperty("type", out var type) ? type.GetString() : null).OfType<string>().ToHashSet();
            return new(open.OrderByDescending(s => s.LastActive).ToList(), providers, types.Contains("password"), types.Contains("webauthn-passwordless"));
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException or KeyNotFoundException)
        {
            log.LogWarning("Keycloak sign-ins response was not understood: {Kind}.", exception.GetType().Name);
            return null;
        }
    }

    /// <summary>#185: DELETE sessions/{session}. The caller has already matched the id against this user's own sessions.</summary>
    public async Task<bool> EndSessionAsync(string subject, string sessionId, CancellationToken cancellationToken) =>
        Guid.TryParse(subject, out _) && SignInPolicy.ValidSessionId(sessionId)
        && await SendAsync(HttpMethod.Delete, $"sessions/{sessionId}", "session logout", notFoundIsDone: true, cancellationToken);

    /// <summary>
    /// #185: re-reads the user, then DELETE users/{id}/federated-identity/{alias} only if another sign-in method stays. The API holds
    /// the account's lifecycle lease around this call so two removals can't both pass the check.
    /// </summary>
    public async Task<UnlinkOutcome> UnlinkProviderAsync(string subject, string alias, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(subject, out var id) || !SignInPolicy.ValidProvider(alias)) return UnlinkOutcome.Failed;
        if (await SignInsAsync(subject, cancellationToken) is not { } current) return UnlinkOutcome.Failed;
        if (!current.LinkedProviders.Contains(alias)) return UnlinkOutcome.NotLinked;
        if (!SignInPolicy.CanUnlink(current, alias)) return UnlinkOutcome.LastSignInMethod;
        return await SendAsync(HttpMethod.Delete, $"users/{id}/federated-identity/{alias}", "unlink", notFoundIsDone: true, cancellationToken)
            ? UnlinkOutcome.Unlinked : UnlinkOutcome.Failed;
    }

    private static DateTimeOffset Millis(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.TryGetInt64(out var ms) && ms > 0
            ? DateTimeOffset.FromUnixTimeMilliseconds(Math.Min(ms, DateTimeOffset.MaxValue.ToUnixTimeMilliseconds())) : DateTimeOffset.UnixEpoch;

    /// <summary>A bounded admin GET returning a JSON array, or null (status only is logged).</summary>
    private async Task<JsonDocument?> GetAsync(string path, string what, CancellationToken cancellationToken, bool array = true)
    {
        try
        {
            var bearer = await TokenAsync(cancellationToken);
            if (bearer is null) return null;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{options.BaseUrl.TrimEnd('/')}/admin/realms/{options.Realm}/{path}");
            request.Headers.Authorization = new("Bearer", bearer);
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("Keycloak {What} returned {Status}.", what, (int)response.StatusCode);
                if (response.StatusCode == HttpStatusCode.Unauthorized) token = null;
                return null;
            }
            if (response.Content.Headers.ContentLength > MaxAdminResponse) { log.LogWarning("Keycloak {What} response was too large.", what); return null; }
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream();
            var chunk = new byte[16 * 1024];
            for (int read; (read = await stream.ReadAsync(chunk, cancellationToken)) > 0;)
            {
                if (buffer.Length + read > MaxAdminResponse) { log.LogWarning("Keycloak {What} response was too large.", what); return null; }
                buffer.Write(chunk, 0, read);
            }
            var document = JsonDocument.Parse(buffer.ToArray());
            if (document.RootElement.ValueKind == (array ? JsonValueKind.Array : JsonValueKind.Object)) return document;
            document.Dispose();
            log.LogWarning("Keycloak {What} response was not a list.", what);
            return null;
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException
            || exception is TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            log.LogWarning("Keycloak {What} failed: {Kind}.", what, exception.GetType().Name);
            return null;
        }
    }

    private const int MaxAdminResponse = 256 * 1024;

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

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial System.Text.RegularExpressions.Regex SessionId();
}
