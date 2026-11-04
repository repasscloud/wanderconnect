using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Infrastructure.Ticketing;

/// <summary>Zammad 7.2.2 X-Hub-Signature: HMAC-SHA1 over the exact request bytes.</summary>
public static class ZammadSignature
{
    public static bool Verify(ReadOnlySpan<byte> body, string? signature, string? secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < 32 || signature is null ||
            signature.Length != 45 || !signature.StartsWith("sha1=", StringComparison.Ordinal)) return false;
        Span<byte> supplied = stackalloc byte[20];
        if (Convert.FromHexString(signature.AsSpan(5), supplied, out var consumed, out var written) != System.Buffers.OperationStatus.Done || consumed != 40 || written != 20) return false;
        var expected = HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), body);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }
}
