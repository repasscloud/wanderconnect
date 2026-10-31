using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Media;

/// <summary>R2 settings (D201): from configuration only (locally `SWOOVR_R2_*` in .local/docker.env through the host's environment).</summary>
public sealed record R2Options(string? Endpoint, string? Bucket, string? AccessKeyId, string? SecretAccessKey)
{
    public bool Configured => !string.IsNullOrWhiteSpace(Endpoint) && !string.IsNullOrWhiteSpace(Bucket)
        && !string.IsNullOrWhiteSpace(AccessKeyId) && !string.IsNullOrWhiteSpace(SecretAccessKey);
}

/// <summary>
/// A private folder (Development and tests): `.local/media` by default. Keys are checked so nothing escapes the folder.
/// </summary>
public sealed class LocalMediaStore(string root) : IMediaStore
{
    public string Name => "local";

    private string PathFor(string key)
    {
        if (!MediaKeys.Valid(key)) throw new ArgumentException("Invalid media key.");
        return Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
    }

    public async Task PutAsync(string key, byte[] data, string contentType, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, data, cancellationToken);
        if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken) =>
        File.Exists(PathFor(key)) ? await File.ReadAllBytesAsync(PathFor(key), cancellationToken) : null;

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try { File.Delete(PathFor(key)); }
        catch (DirectoryNotFoundException) { /* An absent key/parent is already deleted, as with S3 DELETE. */ }
        return Task.FromResult(true);
    }
}

/// <summary>
/// Cloudflare R2 through its S3-compatible API (path-style URLs on the account endpoint, region `auto`), signed with AWS
/// Signature Version 4 and the payload's SHA-256. Objects are only ever read back by Swoovr; the bucket stays private.
/// </summary>
public sealed class R2MediaStore(HttpClient http, R2Options options, ILogger<R2MediaStore> log) : IMediaStore
{
    public string Name => "r2";

    public async Task PutAsync(string key, byte[] data, string contentType, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Put, key, data, contentType, cancellationToken);
        if (!response.IsSuccessStatusCode) throw new IOException($"R2 put returned {(int)response.StatusCode}.");
    }

    public async Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(HttpMethod.Get, key, null, null, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new IOException($"R2 get returned {(int)response.StatusCode}.");
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Delete, key, null, null, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return true;
            log.LogWarning("R2 delete returned {Status}.", (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("R2 delete failed: {Type}.", exception.GetType().Name);
            return false;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string key, byte[]? body, string? contentType, CancellationToken cancellationToken)
    {
        if (!MediaKeys.Valid(key)) throw new ArgumentException("Invalid media key.");
        var endpoint = new Uri(options.Endpoint!.TrimEnd('/') + "/");
        var path = "/" + Uri.EscapeDataString(options.Bucket!) + "/" + string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        var request = new HttpRequestMessage(method, new Uri(endpoint, path));
        var payload = body ?? [];
        var payloadHash = Hex(SHA256.HashData(payload));
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var host = endpoint.IsDefaultPort ? endpoint.Host : $"{endpoint.Host}:{endpoint.Port}";
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new(contentType ?? "application/octet-stream");
        }
        request.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        request.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        const string signed = "host;x-amz-content-sha256;x-amz-date";
        var canonical = $"{method.Method}\n{path}\n\nhost:{host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{amzDate}\n\n{signed}\n{payloadHash}";
        var scope = $"{date}/auto/s3/aws4_request";
        var toSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{scope}\n{Hex(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)))}";
        var key0 = HMACSHA256.HashData(Encoding.UTF8.GetBytes("AWS4" + options.SecretAccessKey), Encoding.UTF8.GetBytes(date));
        var key1 = HMACSHA256.HashData(key0, "auto"u8);
        var key2 = HMACSHA256.HashData(key1, "s3"u8);
        var signing = HMACSHA256.HashData(key2, "aws4_request"u8);
        var signature = Hex(HMACSHA256.HashData(signing, Encoding.UTF8.GetBytes(toSign)));
        request.Headers.TryAddWithoutValidation("Authorization",
            $"AWS4-HMAC-SHA256 Credential={options.AccessKeyId}/{scope}, SignedHeaders={signed}, Signature={signature}");
        return await http.SendAsync(request, cancellationToken);
    }

    private static string Hex(byte[] data) => Convert.ToHexStringLower(data);
}

/// <summary>Object keys: `quarantine/`, `photos/` or `evidence/` and a media id. Nothing else is accepted.</summary>
public static class MediaKeys
{
    public static string Quarantine(Guid id) => $"quarantine/{id:N}";
    public static string Photo(Guid id) => $"photos/{id:N}";
    public static string Evidence(Guid id) => $"evidence/{id:N}";

    public static bool Valid(string key)
    {
        var parts = key.Split('/');
        return parts.Length == 2 && parts[0] is "quarantine" or "photos" or "evidence" or "selftest" && Guid.TryParseExact(parts[1], "N", out _);
    }
}
