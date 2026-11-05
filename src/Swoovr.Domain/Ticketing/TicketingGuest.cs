using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>
/// A support guest: someone without a Swoovr membership who verified an email address. Never a member identity and never
/// merged with one, even when the address matches. Only a keyed lookup hash and a protected copy of the address are kept
/// (the helpdesk customer has no email, so it can't collide with a member's helpdesk customer).
/// </summary>
public sealed class TicketingGuest
{
    public Guid Id { get; set; }
    public byte[] EmailLookup { get; set; } = [];
    public byte[] EmailProtected { get; set; } = [];
    public long? ProviderUserId { get; set; }
    public TicketingOperationState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}

/// <summary>One emailed code. Hash only; single use; superseded by a newer code; five wrong tries end it.</summary>
public sealed class TicketingGuestChallenge
{
    public Guid Id { get; set; }
    public byte[] EmailLookup { get; set; } = [];
    public byte[] AddressLookup { get; set; } = [];
    public byte[] CodeHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset? SupersededAt { get; set; }
    public int FailedAttempts { get; set; }
}

/// <summary>A limited-purpose follow-up grant for one guest's own requests. Hash only, expiring and revocable.</summary>
public sealed class TicketingGuestGrant
{
    public Guid Id { get; set; }
    public Guid GuestId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public static class TicketingGuestPolicy
{
    /// <summary>DEFAULT lifetimes and quotas (reversible). Codes: 10 minutes; grants: 2 hours.</summary>
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(10), GrantLifetime = TimeSpan.FromHours(2);
    public const int MaximumFailures = 5, ActiveGrants = 5, CreatesPerDay = 5, RepliesPerHour = 30;
    public const int CodesPerMinute = 1, CodesPerHour = 3, CodesPerDay = 10, AddressCodesPerHour = 10, AddressCodesPerDay = 50;
    public const string Scheme = "SwoovrGuest";

    /// <summary>DEFAULT public categories: a small routing subset, never the internal queue list.</summary>
    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        ["account"] = "General Support",
        ["recovery"] = "Account Recovery",
        ["billing"] = "Billing & Subscriptions",
        ["safety"] = "Trust & Safety",
        ["privacy"] = "Privacy & Data Protection",
        ["media"] = "Public Relations & Media",
        ["partnerships"] = "Partnerships & Business Development",
        ["other"] = "General Support",
    };

    public static string Login(Guid guest) => "swoovr-guest-" + guest.ToString("D");
    public static string Code() => RandomNumberGenerator.GetInt32(100_000_000).ToString("D8", System.Globalization.CultureInfo.InvariantCulture);
    public static bool CodeShape(string? code) => code is { Length: 8 } && code.All(char.IsAsciiDigit);
    public static byte[] EmailLookup(byte[] key, string normalisedEmail) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("swoovr:support-guest-email:v1|" + normalisedEmail));
    public static byte[] AddressLookup(byte[] key, string? address) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("swoovr:support-guest-address:v1|" + (address ?? "unknown")));
    public static byte[] CodeHash(byte[] key, Guid challenge, string code) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"swoovr:support-guest-code:v1|{challenge:N}|{code}"));
    public static string Token() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static bool TokenShape(string? token) => token is { Length: 43 } && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    public static byte[] TokenHash(string token) => SHA256.HashData(Encoding.UTF8.GetBytes("swoovr:support-guest-grant:v1|" + token));

    /// <summary>Single use; wrong codes count; superseded, used, expired or exhausted challenges never succeed.</summary>
    public static bool Check(TicketingGuestChallenge challenge, byte[] key, string? code, DateTimeOffset now)
    {
        if (challenge.UsedAt != null || challenge.SupersededAt != null || challenge.ExpiresAt <= now || challenge.FailedAttempts >= MaximumFailures) return false;
        if (!CodeShape(code) || !CryptographicOperations.FixedTimeEquals(challenge.CodeHash, CodeHash(key, challenge.Id, code!))) { challenge.FailedAttempts++; return false; }
        challenge.UsedAt = now;
        return true;
    }
}
