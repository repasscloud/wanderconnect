using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>A member's verified mobile number (D148): verified once per account, again only when the number changes.</summary>
public sealed class MemberPhone
{
    public Guid AccountId { get; set; }
    /// <summary>E.164, for example +61412345678. Never shown to other members.</summary>
    public string Number { get; set; } = "";
    public DateTimeOffset VerifiedAt { get; set; }
}

/// <summary>
/// One six-digit code sent to a number (D148), kept only as a keyed hash in its own record, never on the profile. The
/// sender's IP address is kept only as a keyed hash, for the per-address limit. Rows older than a day are purged.
/// </summary>
public sealed class PhoneCode
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Number { get; set; } = "";
    public byte[] CodeHash { get; set; } = [];
    public byte[] IpHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public enum PhoneSendRefusal { None, Cooldown, AccountLimit, NumberLimit, AddressLimit, AlreadyVerified }
public enum PhoneCheckResult { Verified, Wrong, Expired, NoCode, TooManyAttempts }

/// <summary>What the API knows when a member asks for a code.</summary>
public sealed record PhoneSendFacts(DateTimeOffset? LastToThisNumberByAccount, int ByAccountLastHour, int ToNumberLastHour, int FromAddressLastHour, bool SameNumberVerified);

/// <summary>
/// Mobile number verification at registration (Q45, D148, D153, D154). Swoovr generates, hashes, checks and expires its
/// own six-digit codes; a provider only delivers them (Twilio Programmable Messaging when configured, otherwise a clearly
/// labelled simulated sender that never claims delivery, D174). Codes expire after five minutes, allow five wrong
/// attempts, are single-use, and are limited by account, number and IP address with a resend cooldown (DEFAULT values
/// below, D181).
/// </summary>
public static class PhoneVerificationPolicy
{
    public static readonly TimeSpan CodeLifetime = TimeSpan.FromMinutes(5);
    public const int MaxWrongAttempts = 5;
    public static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);
    public const int PerAccountPerHour = 5, PerNumberPerHour = 5, PerAddressPerHour = 10;
    public static readonly TimeSpan CodeRetention = TimeSpan.FromDays(1);

    public static PhoneSendRefusal CanSend(PhoneSendFacts facts, DateTimeOffset now)
    {
        if (facts.SameNumberVerified) return PhoneSendRefusal.AlreadyVerified;
        if (facts.LastToThisNumberByAccount is { } last && now - last < ResendCooldown) return PhoneSendRefusal.Cooldown;
        if (facts.ByAccountLastHour >= PerAccountPerHour) return PhoneSendRefusal.AccountLimit;
        if (facts.ToNumberLastHour >= PerNumberPerHour) return PhoneSendRefusal.NumberLimit;
        return facts.FromAddressLastHour >= PerAddressPerHour ? PhoneSendRefusal.AddressLimit : PhoneSendRefusal.None;
    }

    /// <summary>A uniformly random six-digit code (leading zeros kept).</summary>
    public static string NewCode() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    /// <summary>The keyed hash stored for a code: bound to the account and the number, so a code works nowhere else.</summary>
    public static byte[] Hash(byte[] key, Guid accountId, string number, string code) =>
        HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{accountId:N}|{number}|{code}"));

    public static byte[] AddressHash(byte[] key, string? address) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("ip|" + (address ?? "")));

    /// <summary>Checks a code against the newest open code for this account and number, and marks the outcome on it.</summary>
    public static PhoneCheckResult Check(PhoneCode? latest, byte[] key, Guid accountId, string number, string? code, DateTimeOffset now)
    {
        if (latest is null || latest.UsedAt is not null) return PhoneCheckResult.NoCode;
        if (latest.FailedAttempts >= MaxWrongAttempts) return PhoneCheckResult.TooManyAttempts;
        if (latest.ExpiresAt <= now) return PhoneCheckResult.Expired;
        var digits = code?.Trim() ?? "";
        if (digits.Length != 6 || !digits.All(char.IsAsciiDigit)
            || !CryptographicOperations.FixedTimeEquals(Hash(key, accountId, number, digits), latest.CodeHash))
        {
            latest.FailedAttempts++;
            return latest.FailedAttempts >= MaxWrongAttempts ? PhoneCheckResult.TooManyAttempts : PhoneCheckResult.Wrong;
        }
        latest.UsedAt = now;
        return PhoneCheckResult.Verified;
    }

    /// <summary>
    /// DEFAULT (D181): Australian mobile numbers written locally (04xx xxx xxx) or any number in international form
    /// (+ then 8–15 digits). Spaces, dashes, dots and brackets are ignored. Anything else is null.
    /// </summary>
    public static string? Normalise(string? input)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > 32) return null;
        var plus = input.TrimStart().StartsWith('+');
        if (input.Any(c => !(char.IsAsciiDigit(c) || c is ' ' or '-' or '.' or '(' or ')' or '+'))) return null;
        var digits = new string(input.Where(char.IsAsciiDigit).ToArray());
        if (plus) return digits.Length is >= 8 and <= 15 && digits[0] != '0' ? "+" + digits : null;
        return digits.Length == 10 && digits.StartsWith("04") ? "+61" + digits[1..] : null;
    }

    /// <summary>The number as the member sees it back: country code and the last three digits only.</summary>
    public static string Masked(string number) =>
        number.Length < 7 ? "•••" : number[..3] + new string('•', number.Length - 6) + number[^3..];
}

/// <summary>Delivers a code (D153). Implementations never log the code or the full number.</summary>
public interface IVerificationMessageSender
{
    /// <summary>"twilio" or "simulated"; a simulated sender never claims delivery.</summary>
    string Kind { get; }
    Task<bool> SendCodeAsync(string number, string code, CancellationToken cancellationToken);
}
