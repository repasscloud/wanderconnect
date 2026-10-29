using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

public enum ContactType { Email, Mobile }

/// <summary>Only newer validated token evidence may replace an already accepted email; authentication remains Keycloak's job.</summary>
public static class EmailClaimPolicy
{
    public static DateTimeOffset AcceptanceClock(string? issuedAt, DateTimeOffset now) =>
        IssuedAt(issuedAt,now) is {} issued ? (issued > now ? issued : now) : now.AddSeconds(5);

    public static bool CanAdopt(string? issuedAt, DateTimeOffset? lastChange, DateTimeOffset now)
        => IssuedAt(issuedAt,now) is {} issued && lastChange is not null && issued > lastChange;

    private static DateTimeOffset? IssuedAt(string? issuedAt, DateTimeOffset now)
    {
        if (!long.TryParse(issuedAt, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seconds)
            || seconds <= 0 || seconds > DateTimeOffset.MaxValue.ToUnixTimeSeconds()) return null;
        var issued = DateTimeOffset.FromUnixTimeSeconds(seconds);
        return issued <= now.AddSeconds(5) ? issued : null;
    }
}

/// <summary>
/// A previous verified email address or mobile number (internal trust and safety policy §4, owner Q72/D184, D187). The
/// value is kept only encrypted (AES-GCM, key outside the database) with a keyed HMAC for exact-match lookups, for a
/// limited security period, never in ordinary logs. It records when the value was active and why it ended.
/// </summary>
public sealed class ContactHistory
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public ContactType ContactType { get; set; }
    /// <summary>Key version (1 byte), nonce (12), tag (16), ciphertext.</summary>
    public byte[] EncryptedValue { get; set; } = [];
    public byte[] LookupHmac { get; set; } = [];
    public DateTimeOffset? ValidFrom { get; set; }
    public DateTimeOffset ValidUntil { get; set; }
    /// <summary>The account that made the change (the member, or staff); null for the system.</summary>
    public Guid? ChangedBy { get; set; }
    /// <summary>"changed" (the member verified a new value).</summary>
    public string ChangeReason { get; set; } = "";
    public DateTimeOffset RetainUntil { get; set; }
}

/// <summary>Encryption and lookup for contact history (D187). Keys come from configuration, never the database.</summary>
public static class ContactHistoryPolicy
{
    /// <summary>DEFAULT security period, matching the other security records (D144, D170).</summary>
    public static readonly TimeSpan Retention = TimeSpan.FromDays(730);

    public static string Canonical(ContactType type, string value) =>
        type == ContactType.Email ? EmailAddress.Normalise(value) ?? value.Trim().ToLowerInvariant() : value.Trim();

    public static byte[] Lookup(byte[] lookupKey, ContactType type, string value) =>
        HMACSHA256.HashData(lookupKey, Encoding.UTF8.GetBytes($"{type}|{Canonical(type, value)}"));

    public static byte[] Encrypt(byte version, byte[] key, string value)
    {
        var plain = Encoding.UTF8.GetBytes(value);
        var nonce = RandomNumberGenerator.GetBytes(12);
        var (cipher, tag) = (new byte[plain.Length], new byte[16]);
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag, new byte[] { version });
        return [version, .. nonce, .. tag, .. cipher];
    }

    public static string Decrypt(IReadOnlyDictionary<byte, byte[]> keys, byte[] sealedValue)
    {
        var version = sealedValue[0];
        var plain = new byte[sealedValue.Length - 29];
        using (var aes = new AesGcm(keys[version], 16))
            aes.Decrypt(sealedValue.AsSpan(1, 12), sealedValue.AsSpan(29), sealedValue.AsSpan(13, 16), plain, new byte[] { version });
        return Encoding.UTF8.GetString(plain);
    }

    public static ContactHistory Record(Guid accountId, ContactType type, string previous, DateTimeOffset? validFrom, DateTimeOffset now,
        Guid? changedBy, string reason, byte version, byte[] encryptionKey, byte[] lookupKey) => new()
    {
        Id = Guid.NewGuid(), AccountId = accountId, ContactType = type, EncryptedValue = Encrypt(version, encryptionKey, Canonical(type, previous)),
        LookupHmac = Lookup(lookupKey, type, previous), ValidFrom = validFrom, ValidUntil = now, ChangedBy = changedBy, ChangeReason = reason,
        RetainUntil = now + Retention,
    };
}
