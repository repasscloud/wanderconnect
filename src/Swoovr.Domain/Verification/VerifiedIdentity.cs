using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>
/// The fields kept from an approved ID check (owner Q78, D191; policy §1 principle 2, §2.1): legal first and last name,
/// the document's date of birth, document type, issuing country and expiry date. Never the document number, address,
/// images, selfie, liveness or biometric data.
/// </summary>
public sealed record VerifiedIdentityFields(string? FirstName, string? LastName, DateOnly? DateOfBirth, string? DocumentType, string? IssuingCountry,
    DateOnly? ExpiryDate, string? Method = null);

/// <summary>
/// One member's minimum verified identity record (D191), in the restricted schema. The fields are sealed together
/// (AES-GCM, versioned key held outside the database); the Didit session, date, method, decision and issuing country are
/// in clear; a keyed HMAC of the normalised name and date of birth supports exact ban-enforcement matches only. Kept for
/// the account's life; erased at final deletion unless the account was terminated (2 years) or a legal hold covers it.
/// </summary>
public sealed class VerifiedIdentity
{
    public Guid AccountId { get; set; }
    public string SessionId { get; set; } = "";
    public Guid AttemptId { get; set; }
    public DateTimeOffset VerifiedAt { get; set; }
    /// <summary>Didit's verification method (for example "document"), or null.</summary>
    public string? Method { get; set; }
    public string Decision { get; set; } = "Approved";
    /// <summary>ISO 3166 alpha-3 as Didit reports it; shown to admins and support.</summary>
    public string? IssuingCountry { get; set; }
    /// <summary>Key version (1 byte), nonce (12), tag (16), ciphertext of the fields as JSON.</summary>
    public byte[] Sealed { get; set; } = [];
    /// <summary>Null when the name or date of birth is missing.</summary>
    public byte[]? MatchHmac { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Set at final deletion of a terminated account (ban enforcement); null while the account is active.</summary>
    public DateTimeOffset? RetainUntil { get; set; }
}

/// <summary>
/// Every decryption of a verified identity record (policy §2.3, §11): who, when, whose and the recorded purpose. Ids and
/// the purpose only, never the fields.
/// </summary>
public sealed class IdentityAccess
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid ActorAccountId { get; set; }
    public string Purpose { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

/// <summary>Rules for the verified identity store (D191).</summary>
public static class VerifiedIdentityPolicy
{
    public static readonly TimeSpan TerminatedRetention = TimeSpan.FromDays(730);
    public const int PurposeMax = 500;

    /// <summary>Only the security / legal officer decrypts, with a recorded purpose (D191, policy §11).</summary>
    public static bool MayDecrypt(AccountRole role, string? purpose) =>
        role == AccountRole.Legal && !string.IsNullOrWhiteSpace(purpose) && purpose.Trim().Length <= PurposeMax;

    /// <summary>At final deletion: terminated accounts keep the record two years for ban enforcement; everyone else's goes.</summary>
    public static DateTimeOffset? RetainAfterDeletion(bool terminated, DateTimeOffset now) => terminated ? now + TerminatedRetention : null;

    /// <summary>
    /// The name and date of birth as compared for ban matching: letters only, accents removed, case folded, given and
    /// family name each collapsed; null without a full name and a date of birth.
    /// </summary>
    public static string? MatchKey(VerifiedIdentityFields fields)
    {
        if (fields.DateOfBirth is not { } born || Fold(fields.FirstName) is not { Length: > 0 } first || Fold(fields.LastName) is not { Length: > 0 } last) return null;
        return $"{first}|{last}|{born:yyyy-MM-dd}";
    }

    private static string? Fold(string? name)
    {
        if (name is null) return null;
        var builder = new StringBuilder();
        foreach (var c in name.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) is UnicodeCategory.LowercaseLetter or UnicodeCategory.UppercaseLetter or UnicodeCategory.OtherLetter)
                builder.Append(char.ToLowerInvariant(c));
        return builder.ToString();
    }

    public static byte[]? Match(byte[] hmacKey, VerifiedIdentityFields fields) =>
        MatchKey(fields) is { } key ? HMACSHA256.HashData(hmacKey, Encoding.UTF8.GetBytes(key)) : null;

    public static VerifiedIdentity Record(Guid account, IdVerificationAttempt attempt, VerifiedIdentityFields fields, DateTimeOffset now,
        byte version, byte[] encryptionKey, byte[] hmacKey) => new()
    {
        AccountId = account, SessionId = attempt.SessionId, AttemptId = attempt.Id, VerifiedAt = now, Method = fields.Method, Decision = "Approved",
        IssuingCountry = fields.IssuingCountry is { Length: <= 8 } country ? country : null,
        Sealed = ContactHistoryPolicy.Encrypt(version, encryptionKey, System.Text.Json.JsonSerializer.Serialize(fields)),
        MatchHmac = Match(hmacKey, fields), CreatedAt = now,
    };

    public static VerifiedIdentityFields Open(IReadOnlyDictionary<byte, byte[]> keys, byte[] sealedValue) =>
        System.Text.Json.JsonSerializer.Deserialize<VerifiedIdentityFields>(ContactHistoryPolicy.Decrypt(keys, sealedValue))!;
}
