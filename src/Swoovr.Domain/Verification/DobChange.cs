using System.Security.Cryptography;
using System.Text.Json;

namespace Swoovr.Domain;

/// <summary>D299: a member-bound, revocable one-use correction window; contains no date.</summary>
public sealed class DobChangeGrant
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid GrantedBy { get; set; }
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}

/// <summary>D299: prior profile dates are encrypted in the restricted schema, never ordinary audit data.</summary>
public sealed class DobChangeHistory
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? GrantId { get; set; }
    public byte[] Sealed { get; set; } = [];
    public DateTimeOffset ChangedAt { get; set; }
    public DateTimeOffset? RetainUntil { get; set; }
}

public static class DobChangePolicy
{
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);
    public static bool MayGrant(AccountRole role) => role == AccountRole.Admin;
    public static bool Live(DobChangeGrant? grant, Guid member, DateTimeOffset now) =>
        grant is not null && grant.AccountId == member && grant.CreatedAt <= now && grant.ExpiresAt > now
        && grant.ConsumedAt is null && grant.RevokedAt is null;
    // Presence itself locks all populated legacy dates; no migration-created free edit window.
    public static bool CanSave(DateOnly? previous, bool confirmed, DobChangeGrant? grant, Guid member, DateTimeOffset now) =>
        confirmed && (previous is null || Live(grant, member, now));

    private sealed record Payload(Guid AccountId, DateOnly DateOfBirth);
    public static byte[] Seal(Guid member, DateOnly previous, byte version, byte[] key) =>
        ContactHistoryPolicy.Encrypt(version, key, JsonSerializer.Serialize(new Payload(member, previous)));
    public static DateOnly Open(Guid member, IReadOnlyDictionary<byte, byte[]> keys, byte[] value)
    {
        var data = JsonSerializer.Deserialize<Payload>(ContactHistoryPolicy.Decrypt(keys, value));
        if (data is null || data.AccountId != member) throw new CryptographicException("DOB history ownership mismatch.");
        return data.DateOfBirth;
    }
}
