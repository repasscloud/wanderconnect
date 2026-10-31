using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Swoovr.Infrastructure.Messaging;

// D210: provider acknowledgement is not delivery, display or read evidence. No provider body/token enters logs.
public sealed record PushOutcome(string Status, bool InvalidToken = false, TimeSpan? RetryAfter = null)
{
    public static readonly PushOutcome Accepted = new("accepted");
    public static readonly PushOutcome Unknown = new("unknown");
}
public sealed record ApnsOptions(string KeyId, string TeamId, string BundleId, string PrivateKeyPem, bool Sandbox)
{ public override string ToString() => "APNs options (credentials redacted)"; }
public sealed record FcmOptions(string ProjectId, string ClientEmail, string PrivateKeyId, string PrivateKeyPem)
{ public override string ToString() => "FCM options (credentials redacted)"; }

internal static class PushWire
{
    public static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static string Part(object value) => Encode(JsonSerializer.SerializeToUtf8Bytes(value));
    public static bool SafeLink(string link) => link.StartsWith('/') && !link.StartsWith("//") && !link.Contains('\\') && !link.Any(char.IsControl);
    public static TimeSpan? RetryAfter(HttpResponseMessage response, DateTimeOffset now)
    {
        var delay = response.Headers.RetryAfter?.Delta ?? response.Headers.RetryAfter?.Date - now;
        return delay is { } d ? d > TimeSpan.Zero ? d : TimeSpan.Zero : null;
    }
    public static async Task<JsonDocument?> JsonAsync(HttpResponseMessage response, CancellationToken token)
    {
        // Provider bodies can contain rejected tokens; read only in memory, never log/return their text.
        try
        {
            var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
            if (document.RootElement.ValueKind == JsonValueKind.Object) return document;
            document.Dispose(); return null;
        }
        catch (JsonException) { return null; }
    }
}

public sealed class ApnsPushSender(HttpClient http, ApnsOptions options, TimeProvider clock) : IPushSender
{
    public string Kind => "apns";
    private readonly object signing = new();
    private string? bearer;
    private DateTimeOffset issued;
    private string Bearer()
    {
        lock (signing)
        {
            var now = clock.GetUtcNow();
            if (bearer is not null && now >= issued && now - issued < TimeSpan.FromMinutes(45)) return bearer;
            using var key = ECDsa.Create(); key.ImportFromPem(options.PrivateKeyPem);
            var unsigned = PushWire.Part(new { alg = "ES256", kid = options.KeyId }) + "." + PushWire.Part(new { iss = options.TeamId, iat = now.ToUnixTimeSeconds() });
            bearer = unsigned + "." + PushWire.Encode(key.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
            issued = now;
            return bearer;
        }
    }
    public async Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        if (message.Platform != "ios" || !PushWire.SafeLink(message.Link)) return new("invalid-request");
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://" + (options.Sandbox ? "api.sandbox.push.apple.com" : "api.push.apple.com") + "/3/device/" + Uri.EscapeDataString(message.Token));
        request.Version = HttpVersion.Version20;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        try { request.Headers.Authorization = new AuthenticationHeaderValue("bearer", Bearer()); }
        catch (CryptographicException) { return new("auth-failed"); }
        request.Headers.Add("apns-topic", options.BundleId);
        request.Headers.Add("apns-push-type", "alert");
        request.Headers.Add("apns-priority", message.HighPriority ? "10" : "5");
        request.Headers.Add("apns-expiration", (clock.GetUtcNow() + TimeSpan.FromHours(1)).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("apns-id", message.AttemptId.ToString());
        request.Content = JsonContent.Create(new { aps = new { alert = new { title = message.Title, body = message.Body }, sound = "default" }, swoovr_path = message.Link });
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.OK) return PushOutcome.Accepted;
            using var body = await PushWire.JsonAsync(response, cancellationToken);
            var reason = body?.RootElement.TryGetProperty("reason", out var r) == true ? r.ValueKind == JsonValueKind.String ? r.GetString() : null : null;
            if (reason is "Unregistered" or "BadDeviceToken" or "DeviceTokenNotForTopic") return new("invalid-token", true);
            if ((int)response.StatusCode is 429 or 500 or 503) return new("retry", RetryAfter: PushWire.RetryAfter(response, clock.GetUtcNow()) ?? TimeSpan.FromMinutes(1));
            return new("rejected");
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException) { return PushOutcome.Unknown; }
    }
}

public sealed class FcmPushSender(HttpClient http, FcmOptions options, TimeProvider clock) : IPushSender, IDisposable
{
    public string Kind => "fcm";
    private readonly SemaphoreSlim authLock = new(1, 1);
    private string? bearer;
    private DateTimeOffset expires;
    private sealed class AuthRetry(TimeSpan delay) : Exception { public TimeSpan Delay { get; } = delay; }
    private async Task<string?> BearerAsync(CancellationToken token)
    {
        await authLock.WaitAsync(token);
        try
        {
            var now = clock.GetUtcNow();
            if (bearer is not null && expires > now + TimeSpan.FromMinutes(2)) return bearer;
            using var key = RSA.Create(); key.ImportFromPem(options.PrivateKeyPem);
            var unsigned = PushWire.Part(new { alg = "RS256", typ = "JWT", kid = options.PrivateKeyId }) + "." + PushWire.Part(new
            {
                iss = options.ClientEmail, scope = "https://www.googleapis.com/auth/firebase.messaging", aud = "https://oauth2.googleapis.com/token",
                iat = now.ToUnixTimeSeconds(), exp = (now + TimeSpan.FromHours(1)).ToUnixTimeSeconds(),
            });
            var assertion = unsigned + "." + PushWire.Encode(key.SignData(Encoding.ASCII.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            { Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer", ["assertion"] = assertion }) };
            using var response = await http.SendAsync(request, token);
            if ((int)response.StatusCode is 429 or 500 or 503) throw new AuthRetry(PushWire.RetryAfter(response, now) ?? TimeSpan.FromMinutes(1));
            if (!response.IsSuccessStatusCode) return null;
            using var body = await PushWire.JsonAsync(response, token);
            if (body is null || !body.RootElement.TryGetProperty("access_token", out var access) || access.ValueKind != JsonValueKind.String
                || !body.RootElement.TryGetProperty("expires_in", out var lifetime) || !lifetime.TryGetInt32(out var seconds) || seconds < 60) return null;
            bearer = access.GetString(); expires = now + TimeSpan.FromSeconds(Math.Min(seconds, 3600));
            return bearer;
        }
        finally { authLock.Release(); }
    }
    public async Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        if (message.Platform != "android" || !PushWire.SafeLink(message.Link)) return new("invalid-request");
        try
        {
            var access = await BearerAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(access)) return new("auth-failed");
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://fcm.googleapis.com/v1/projects/" + Uri.EscapeDataString(options.ProjectId) + "/messages:send");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            request.Content = JsonContent.Create(new { message = new { token = message.Token,
                notification = new { title = message.Title, body = message.Body }, data = new { swoovr_path = message.Link },
                android = new { priority = message.HighPriority ? "HIGH" : "NORMAL", ttl = "3600s", notification = new { channel_id = "swoovr", tag = message.AttemptId.ToString() } } } });
            using var response = await http.SendAsync(request, cancellationToken);
            using var body = await PushWire.JsonAsync(response, cancellationToken);
            if (response.IsSuccessStatusCode)
                return body?.RootElement.TryGetProperty("name", out var name) == true && name.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(name.GetString()) ? PushOutcome.Accepted : PushOutcome.Unknown;
            if (response.StatusCode == HttpStatusCode.Unauthorized) { await authLock.WaitAsync(cancellationToken); try { if (bearer == access) bearer = null; } finally { authLock.Release(); } }
            if (body?.RootElement.TryGetProperty("error", out var error) == true && error.ValueKind == JsonValueKind.Object && error.TryGetProperty("details", out var details) && details.ValueKind == JsonValueKind.Array
                && details.EnumerateArray().Any(d => d.ValueKind == JsonValueKind.Object && d.TryGetProperty("@type", out var type) && type.GetString() == "type.googleapis.com/google.firebase.fcm.v1.FcmError"
                    && d.TryGetProperty("errorCode", out var code) && code.GetString() == "UNREGISTERED")) return new("invalid-token", true);
            // INVALID_ARGUMENT can describe the payload, so it never automatically removes a device token.
            if ((int)response.StatusCode is 429 or 500 or 503) return new("retry", RetryAfter: PushWire.RetryAfter(response, clock.GetUtcNow()) ?? TimeSpan.FromMinutes(1));
            return new("rejected");
        }
        catch (CryptographicException) { return new("auth-failed"); }
        catch (AuthRetry retry) { return new("retry", RetryAfter: retry.Delay); }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException) { return PushOutcome.Unknown; }
    }
    public void Dispose() => authLock.Dispose();
}

public sealed class DisabledPushSender : IPushSender
{
    public string Kind => "disabled";
    public Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken) => Task.FromResult(new PushOutcome("disabled"));
}

/// <summary>Platform routing is configuration, independent of notification/privacy business logic.</summary>
public sealed class PlatformPushSender(IPushSender ios, IPushSender android) : IPushSender
{
    public string Kind => "providers";
    public Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken) => (message.Platform == "ios" ? ios : android).SendAsync(message, cancellationToken);
}
