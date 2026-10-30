using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>External resource verification only. Never a member credential or Keycloak session.</summary>
public sealed class MeetingSafetyContactChallenge
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public byte[] DestinationHash { get; set; } = [];
    public byte[] CodeHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public int FailedAttempts { get; set; }
}

/// <summary>Only the SHA256 digest persists. Every read must also validate the current link and consent.</summary>
public sealed class MeetingSafetyContactReadGrant
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public static class MeetingSafetyContactAccessPolicy
{
    public const int MaximumFailures = 5;
    public static string Code() => RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture);
    public static bool CodeShape(string? code) => code is { Length: 6 } && code.All(char.IsAsciiDigit);
    public static byte[] CodeHash(byte[] key, Guid recipient, Guid challenge, string code) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"swoovr:meeting-safety-access-code:v1|{recipient:N}|{challenge:N}|{code}"));
    public static byte[] DestinationHash(byte[] key, string channel, string destination) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"swoovr:meeting-safety-access-destination:v1|{channel}|{destination}"));
    public static bool Check(MeetingSafetyContactChallenge challenge, byte[] key, string? code, DateTimeOffset now)
    {
        if (challenge.UsedAt != null || challenge.SupersededAt != null || challenge.ExpiresAt <= now || challenge.FailedAttempts >= MaximumFailures) return false;
        if (!CodeShape(code) || !CryptographicOperations.FixedTimeEquals(challenge.CodeHash, CodeHash(key, challenge.RecipientId, challenge.Id, code!)))
        { challenge.FailedAttempts++; return false; }
        challenge.UsedAt = now;
        return true;
    }
}
