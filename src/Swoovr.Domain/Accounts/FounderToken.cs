using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>
/// One of the founding places (D63: 1,000). A place outlives the account that earned it, so a
/// returning founder takes back the same place instead of a new one (D65). Holds no personal data.
/// </summary>
public sealed class FounderPlace
{
    public int Place { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
}

public enum FounderTokenStatus { Valid, Invalidated }

/// <summary>
/// A founder recovery token (D65): the HMAC of a verified email under a versioned server
/// secret, stored with its status but never with the email. It survives account deletion.
/// </summary>
public sealed class FounderToken
{
    public Guid Id { get; set; }
    public int Place { get; set; }
    /// <summary>The full code as the member types it, for example FOUNDER-V1-&lt;64 hex&gt;.</summary>
    public string Code { get; set; } = "";
    public int SecretVersion { get; set; }
    public FounderTokenStatus Status { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
    /// <summary>"email-changed" or "admin".</summary>
    public string? InvalidatedReason { get; set; }
}

/// <summary>
/// Issues and checks founder codes (D65). Format (DEFAULT): FOUNDER-V{version}-{64 upper-case
/// hex}, where the hex is HMAC-SHA256(secret[version], "swoovr:founder:v{version}:{email}") and
/// the email is normalised with <see cref="EmailAddress.Normalise"/> both times.
/// </summary>
public static class FounderTokenCodec
{
    public const string Prefix = "FOUNDER-";

    /// <summary>Whether a typed coupon code is meant for the founder check (prefix, any case).</summary>
    public static bool IsFounderCode(string? code) =>
        code is not null && code.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase);

    public static string Issue(string email, int version, byte[] secret)
    {
        var normalised = EmailAddress.Normalise(email) ?? throw new ArgumentException("A valid email is required.", nameof(email));
        if (version < 1 || secret.Length < 32) throw new ArgumentException("A versioned secret of at least 32 bytes is required.");
        return $"{Prefix}V{version}-{Convert.ToHexString(Mac(normalised, version, secret))}";
    }

    /// <summary>Parses the typed code into its canonical stored form and secret version.</summary>
    public static bool TryParse(string? code, out string canonical, out int version)
    {
        (canonical, version) = ("", 0);
        if (code is null) return false;
        var text = code.Trim().ToUpperInvariant();
        if (!text.StartsWith(Prefix + "V", StringComparison.Ordinal)) return false;
        var dash = text.IndexOf('-', Prefix.Length);
        if (dash < 0 || !int.TryParse(text.AsSpan(Prefix.Length + 1, dash - Prefix.Length - 1), System.Globalization.NumberStyles.None, null, out version)
            || version < 1 || text.Length - dash - 1 != 64 || Convert.FromHexString(text.AsSpan(dash + 1), new byte[32], out _, out var written) != OperationStatus.Done || written != 32)
            return false;
        canonical = text;
        return true;
    }

    /// <summary>Recomputes the HMAC from the current verified email and compares in constant time.</summary>
    public static bool Matches(string canonical, int version, string? email, byte[] secret)
    {
        if (EmailAddress.Normalise(email) is not { } normalised || secret.Length < 32) return false;
        var supplied = new byte[32];
        if (Convert.FromHexString(canonical.AsSpan(canonical.LastIndexOf('-') + 1), supplied, out _, out var written) != OperationStatus.Done || written != 32)
            return false;
        return CryptographicOperations.FixedTimeEquals(supplied, Mac(normalised, version, secret));
    }

    private static byte[] Mac(string normalisedEmail, int version, byte[] secret) =>
        HMACSHA256.HashData(secret, Encoding.UTF8.GetBytes($"swoovr:founder:v{version}:{normalisedEmail}"));
}

public enum FounderRedeemOutcome { Restored, AlreadyFounder, Rejected }

/// <summary>
/// Whether a stored token restores founding status on this account (D65): the token row must
/// exist and be valid, the account's email verified, and the HMAC of that email must match.
/// </summary>
public static class FounderRedeemPolicy
{
    public static FounderRedeemOutcome Evaluate(FounderToken? token, string canonical, int version, bool emailVerified,
        string? verifiedEmail, byte[]? secret, MemberProfile profile)
    {
        if (token is not { Status: FounderTokenStatus.Valid } || token.SecretVersion != version || token.Code != canonical
            || !emailVerified || secret is null || !FounderTokenCodec.Matches(canonical, version, verifiedEmail, secret))
            return FounderRedeemOutcome.Rejected;
        return profile.FoundingMember && profile.FounderPlace == token.Place ? FounderRedeemOutcome.AlreadyFounder : FounderRedeemOutcome.Restored;
    }
}
