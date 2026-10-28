using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Infrastructure.Messaging;

/// <summary>Versioned AES-GCM with device/account/platform AAD; HMAC lookup key stays stable across encryption rotation.</summary>
public sealed class PushTokenProtector(IReadOnlyDictionary<int, byte[]> encryption, byte[] lookup)
{
    public int CurrentVersion => encryption.Keys.Max();
    public string Hash(string platform, string token) => Convert.ToHexStringLower(HMACSHA256.HashData(lookup, Encoding.UTF8.GetBytes("swoovr:push-token:" + platform + ":" + token)));
    private static byte[] Aad(Guid id, Guid account, string platform) => Encoding.UTF8.GetBytes($"swoovr:push:{id:D}:{account:D}:{platform}");
    public byte[] Protect(Guid id, Guid account, string platform, string token)
    {
        var raw = Encoding.UTF8.GetBytes(token); var bytes = new byte[12 + 16 + raw.Length]; RandomNumberGenerator.Fill(bytes.AsSpan(0, 12));
        using var aes = new AesGcm(encryption[CurrentVersion], 16);
        aes.Encrypt(bytes.AsSpan(0, 12), raw, bytes.AsSpan(28), bytes.AsSpan(12, 16), Aad(id, account, platform));
        CryptographicOperations.ZeroMemory(raw);
        return bytes;
    }
    public string Unprotect(Guid id, Guid account, string platform, int version, byte[] encrypted)
    {
        if (encrypted.Length < 28 || !encryption.TryGetValue(version, out var key)) throw new CryptographicException("Push token key unavailable.");
        var raw = new byte[encrypted.Length - 28];
        try { using var aes = new AesGcm(key, 16); aes.Decrypt(encrypted.AsSpan(0, 12), encrypted.AsSpan(28), encrypted.AsSpan(12, 16), raw, Aad(id, account, platform)); return Encoding.UTF8.GetString(raw); }
        finally { CryptographicOperations.ZeroMemory(raw); }
    }
}
