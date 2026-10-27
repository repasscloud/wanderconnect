using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace Swoovr.Client;

/// <summary>
/// Where the app signs in: the members realm (D31) as a public client with PKCE (design: "Mobile (members only)").
/// No client secret exists; the redirect is the app's private-use scheme (DEFAULT, RFC 8252 7.1).
/// </summary>
public sealed record NativeSignInOptions(Uri Authority, string ClientId, Uri RedirectUri, Uri SignedOutUri, string Scope = "openid profile email");

/// <summary>One sign-in attempt. Kept in memory only, never stored: a new attempt replaces it.</summary>
public sealed record SignInRequest(Uri AuthorizeUri, string State, string Nonce, string CodeVerifier);

/// <summary>What the app keeps in secure storage. The API validates the access token on every call (D35, D55).</summary>
public sealed record NativeTokens(string AccessToken, string? RefreshToken, string IdToken, DateTimeOffset ExpiresAt)
{
    public bool NearlyExpired(DateTimeOffset now) => now >= ExpiresAt - TimeSpan.FromSeconds(30);
}

/// <summary>A refused or malformed sign-in. The message is for logs; members see a fixed sentence.</summary>
public sealed class NativeSignInException(string message) : Exception(message);

/// <summary>
/// Authorization code + PKCE (S256) against Keycloak for a native app. Endpoints come from the realm's discovery
/// document, never from built-in paths. Keycloak issues every token; this class only asks for and refreshes them,
/// and the Swoovr API never mints or exchanges them (D35).
/// </summary>
public sealed class NativeSignIn(HttpClient http, NativeSignInOptions options, TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private Discovery? discovery;

    /// <param name="reauthenticate">D169: ask Keycloak to sign the member in again (prompt=login, max_age=0), for deletion and export.</param>
    public async Task<SignInRequest> BeginAsync(CancellationToken cancellationToken = default, bool reauthenticate = false)
    {
        var endpoints = await DiscoverAsync(cancellationToken);
        var (state, nonce, verifier) = (Random(32), Random(32), Random(32));
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId, ["response_type"] = "code", ["scope"] = options.Scope,
            ["redirect_uri"] = options.RedirectUri.ToString(), ["state"] = state, ["nonce"] = nonce,
            ["code_challenge"] = Challenge(verifier), ["code_challenge_method"] = "S256",
        };
        if (reauthenticate) (parameters["prompt"], parameters["max_age"]) = ("login", "0");
        var query = Query(parameters);
        return new(new Uri(endpoints.AuthorizationEndpoint + "?" + query), state, nonce, verifier);
    }

    /// <summary>Checks the callback against the attempt, then redeems the code once.</summary>
    public async Task<NativeTokens> CompleteAsync(SignInRequest request, IReadOnlyDictionary<string, string> callback, CancellationToken cancellationToken = default)
    {
        if (!callback.TryGetValue("state", out var state) || !FixedTimeEquals(state, request.State))
            throw new NativeSignInException("State did not match this sign-in attempt.");
        if (callback.TryGetValue("error", out var error)) throw new NativeSignInException("Keycloak refused the sign-in: " + error);
        if (!callback.TryGetValue("code", out var code) || code.Length == 0) throw new NativeSignInException("No authorization code returned.");
        var endpoints = await DiscoverAsync(cancellationToken);
        var tokens = await TokenAsync(endpoints, new()
        {
            ["grant_type"] = "authorization_code", ["client_id"] = options.ClientId, ["code"] = code,
            ["redirect_uri"] = options.RedirectUri.ToString(), ["code_verifier"] = request.CodeVerifier,
        }, cancellationToken) ?? throw new NativeSignInException("Keycloak refused the authorization code.");
        CheckIdToken(tokens.IdToken, endpoints.Issuer, request.Nonce);
        return tokens;
    }

    /// <summary>Rotates the refresh token (the realm revokes the old one). Null means sign in again.</summary>
    public async Task<NativeTokens?> RefreshAsync(NativeTokens current, CancellationToken cancellationToken = default)
    {
        if (current.RefreshToken is null) return null;
        var endpoints = await DiscoverAsync(cancellationToken);
        var tokens = await TokenAsync(endpoints, new()
        {
            ["grant_type"] = "refresh_token", ["client_id"] = options.ClientId, ["refresh_token"] = current.RefreshToken,
        }, cancellationToken);
        if (tokens is null) return null;
        // Keycloak may omit the ID token on refresh; keep the one from sign-in for the end-session hint.
        return tokens with { IdToken = tokens.IdToken.Length == 0 ? current.IdToken : tokens.IdToken };
    }

    /// <summary>The browser page that ends the Keycloak session and returns to the app.</summary>
    public async Task<Uri> EndSessionUriAsync(NativeTokens tokens, CancellationToken cancellationToken = default)
    {
        var endpoints = await DiscoverAsync(cancellationToken);
        if (endpoints.EndSessionEndpoint is null) throw new NativeSignInException("The realm publishes no end-session endpoint.");
        return new Uri(endpoints.EndSessionEndpoint + "?" + Query(new()
        {
            ["client_id"] = options.ClientId, ["id_token_hint"] = tokens.IdToken, ["post_logout_redirect_uri"] = options.SignedOutUri.ToString(),
        }));
    }

    private async Task<Discovery> DiscoverAsync(CancellationToken cancellationToken)
    {
        if (discovery is not null) return discovery;
        var document = await http.GetFromJsonAsync<Discovery>(options.Authority.ToString().TrimEnd('/') + "/.well-known/openid-configuration", cancellationToken)
            ?? throw new NativeSignInException("Empty discovery document.");
        if (document.Issuer != options.Authority.ToString().TrimEnd('/'))
            throw new NativeSignInException("Discovery issuer does not match the configured realm.");
        return discovery = document;
    }

    private async Task<NativeTokens?> TokenAsync(Discovery endpoints, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(endpoints.TokenEndpoint, new FormUrlEncodedContent(form), cancellationToken);
        if (!response.IsSuccessStatusCode) return null;
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken);
        if (body is not { AccessToken.Length: > 0, TokenType: var type } || !string.Equals(type, "Bearer", StringComparison.OrdinalIgnoreCase))
            throw new NativeSignInException("Token response is missing a bearer access token.");
        return new(body.AccessToken, body.RefreshToken, body.IdToken ?? "", time.GetUtcNow().AddSeconds(body.ExpiresIn));
    }

    /// <summary>
    /// The ID token arrived directly from the token endpoint over the app's own connection, so its claims are checked
    /// (issuer, audience, nonce) but not its signature (DEFAULT). Nothing here grants access: the API decides.
    /// </summary>
    private void CheckIdToken(string idToken, string issuer, string nonce)
    {
        var parts = idToken.Split('.');
        if (parts.Length != 3) throw new NativeSignInException("ID token is not a JWT.");
        using var claims = JsonDocument.Parse(FromBase64Url(parts[1]));
        var root = claims.RootElement;
        if (Text(root, "iss") != issuer) throw new NativeSignInException("ID token issuer mismatch.");
        var audience = root.TryGetProperty("aud", out var aud) ? aud : default;
        var audiences = audience.ValueKind == JsonValueKind.Array ? audience.EnumerateArray().Select(a => a.GetString()) : [audience.ValueKind == JsonValueKind.String ? audience.GetString() : null];
        if (!audiences.Contains(options.ClientId)) throw new NativeSignInException("ID token audience mismatch.");
        if (Text(root, "nonce") is not { } returned || !FixedTimeEquals(returned, nonce)) throw new NativeSignInException("ID token nonce mismatch.");
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
    private static string Random(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));
    private static string Base64Url(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromBase64Url(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(padded + new string('=', (4 - padded.Length % 4) % 4));
    }
    private static string Query(Dictionary<string, string> values) =>
        string.Join("&", values.Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value)));

    private sealed record Discovery(
        [property: JsonPropertyName("issuer")] string Issuer,
        [property: JsonPropertyName("authorization_endpoint")] string AuthorizationEndpoint,
        [property: JsonPropertyName("token_endpoint")] string TokenEndpoint,
        [property: JsonPropertyName("end_session_endpoint")] string? EndSessionEndpoint);

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("refresh_token")] string? RefreshToken,
        [property: JsonPropertyName("id_token")] string? IdToken);
}

/// <summary>Device storage for the app's tokens (Keychain/Keystore through MAUI SecureStorage on the phone).</summary>
public interface ITokenStore
{
    Task<string?> ReadAsync();
    Task WriteAsync(string value);
    void Clear();
}

/// <summary>The signed-in state on a device: stored tokens, refreshed before they expire.</summary>
public sealed class NativeSession(NativeSignIn signIn, ITokenStore store, TimeProvider? clock = null)
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);
    private NativeTokens? tokens;
    private bool loaded;

    public async Task<bool> IsSignedInAsync() => await CurrentAsync() is not null;

    public async Task SaveAsync(NativeTokens value)
    {
        await gate.WaitAsync();
        try { (tokens, loaded) = (value, true); await store.WriteAsync(JsonSerializer.Serialize(value)); }
        finally { gate.Release(); }
    }

    /// <summary>Forgets the tokens on this device. Ending the Keycloak session is the browser's job.</summary>
    public NativeTokens? Forget()
    {
        var previous = tokens;
        (tokens, loaded) = (null, true);
        store.Clear();
        return previous;
    }

    /// <summary>A usable access token, refreshing once when it is about to expire; null means sign in again.</summary>
    public async Task<string?> AccessTokenAsync(bool forceRefresh = false, CancellationToken cancellationToken = default) =>
        (await CurrentAsync(forceRefresh, cancellationToken))?.AccessToken;

    private async Task<NativeTokens?> CurrentAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!loaded)
            {
                loaded = true;
                try { tokens = await store.ReadAsync() is { } json ? JsonSerializer.Deserialize<NativeTokens>(json) : null; }
                catch (JsonException) { tokens = null; store.Clear(); }
            }
            if (tokens is null || (!forceRefresh && !tokens.NearlyExpired(time.GetUtcNow()))) return tokens;
            NativeTokens? refreshed;
            try { refreshed = await signIn.RefreshAsync(tokens, cancellationToken); }
            catch (HttpRequestException) { return null; } // Offline: keep the stored tokens for later, but don't send expired ones.
            if (refreshed is null) { tokens = null; store.Clear(); return null; }
            tokens = refreshed;
            await store.WriteAsync(JsonSerializer.Serialize(refreshed));
            return refreshed;
        }
        finally { gate.Release(); }
    }
}

/// <summary>Adds the member's bearer token to API calls and retries once with a refreshed token after a 401.</summary>
public sealed class BearerHandler(NativeSession session) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (await session.AccessTokenAsync(cancellationToken: cancellationToken) is not { } token)
            return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized) { RequestMessage = request };
        request.Headers.Authorization = new("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized || request.Content is not null) return response;
        // Only bodiless requests are replayed, so a form is never sent twice.
        if (await session.AccessTokenAsync(forceRefresh: true, cancellationToken) is not { } fresh || fresh == token) return response;
        response.Dispose();
        request.Headers.Authorization = new("Bearer", fresh);
        return await base.SendAsync(request, cancellationToken);
    }
}
