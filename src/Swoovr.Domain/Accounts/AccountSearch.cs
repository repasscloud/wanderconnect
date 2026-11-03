namespace Swoovr.Domain;

/// <summary>A parsed D307 staff account search: "phone" (E.164), "email" (normalised) or "name" (ILIKE pattern, `\` escape).</summary>
public sealed record AccountSearchQuery(string Kind, string Value);

/// <summary>
/// D307 (owner, 3 November 2026): one search box for mobile, email or registered first/last name. Mobile accepts a leading
/// `+` or not and ignores spaces; email is lower-cased; for names `%` is the only wildcard (like `*`), every other
/// character — including `_` and `\` — is literal, and a value without `%` is an exact case-insensitive match.
/// Empty and wildcard-only searches are refused so staff cannot list every account.
/// </summary>
public static class AccountSearch
{
    public const int MaxLength = 120;

    public static AccountSearchQuery? Parse(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxLength || text.Any(char.IsControl)) return null;
        if (text.Contains('@')) return EmailAddress.Normalise(text) is { } email ? new("email", email) : null;
        if (text.All(c => char.IsAsciiDigit(c) || c is ' ' or '+' or '-' or '(' or ')' or '.') && text.Any(char.IsAsciiDigit))
            return Phone(text) is { } phone ? new("phone", phone) : null;
        if (text.Replace("%", "").Trim().Length == 0) return null;
        return new("name", NamePattern(text));
    }

    /// <summary>Canonical E.164 using the verification rules, plus international digits typed without the `+`.</summary>
    public static string? Phone(string text)
    {
        if (PhoneVerificationPolicy.Normalise(text) is { } normal) return normal;
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        return !text.TrimStart().StartsWith('+') && digits.Length is >= 8 and <= 15 && digits[0] != '0' ? "+" + digits : null;
    }

    /// <summary>Escapes `\` and `_` for ILIKE (escape character `\`); `%` stays the wildcard.</summary>
    public static string NamePattern(string text) => text.Replace("\\", "\\\\").Replace("_", "\\_");
}
