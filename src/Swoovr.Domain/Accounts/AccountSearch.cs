namespace Swoovr.Domain;

/// <summary>
/// A parsed D307 staff account search: "phone" (E.164), "email" (normalised, or a lower-cased ILIKE pattern when
/// <see cref="Wildcard"/>) or "name" (ILIKE pattern, `\` escape; with a `%` wildcard it also matches the email address).
/// </summary>
public sealed record AccountSearchQuery(string Kind, string Value, bool Wildcard = false);

/// <summary>
/// D307 (owner, 3 November 2026): one search box for mobile, email or registered first/last name. Mobile accepts a leading
/// `+` or not and ignores spaces; email is lower-cased; `%` is the only wildcard (like `*`), every other character —
/// including `_` and `\` — is literal, and a value without `%` is an exact case-insensitive match.
/// D365 (owner, issue 331): a `%` search also looks in email addresses. With `@` it is an email pattern
/// (`%name@company%`); without `@` it matches first name, last name or email (`%name%`).
/// Empty and wildcard-only searches are refused so staff cannot list every account.
/// </summary>
public static class AccountSearch
{
    public const int MaxLength = 120;

    public static AccountSearchQuery? Parse(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > MaxLength || text.Any(char.IsControl)) return null;
        var wildcard = text.Contains('%');
        // Nothing but wildcards, `@` and spaces would list every account.
        if (text.Replace("%", "").Replace("@", "").Trim().Length == 0) return null;
        if (text.Contains('@'))
        {
            if (!wildcard) return EmailAddress.Normalise(text) is { } email ? new("email", email) : null;
            return text.Any(char.IsWhiteSpace) ? null : new("email", Pattern(text.ToLowerInvariant()), true);
        }
        if (text.All(c => char.IsAsciiDigit(c) || c is ' ' or '+' or '-' or '(' or ')' or '.') && text.Any(char.IsAsciiDigit))
            return Phone(text) is { } phone ? new("phone", phone) : null;
        return new("name", Pattern(text), wildcard);
    }

    /// <summary>Canonical E.164 using the verification rules, plus international digits typed without the `+`.</summary>
    public static string? Phone(string text)
    {
        if (PhoneVerificationPolicy.Normalise(text) is { } normal) return normal;
        var digits = new string(text.Where(char.IsAsciiDigit).ToArray());
        return !text.TrimStart().StartsWith('+') && digits.Length is >= 8 and <= 15 && digits[0] != '0' ? "+" + digits : null;
    }

    /// <summary>Escapes `\` and `_` for ILIKE (escape character `\`); `%` stays the wildcard.</summary>
    public static string Pattern(string text) => text.Replace("\\", "\\\\").Replace("_", "\\_");

    /// <summary>The same pattern for the stored (lower-case) email column.</summary>
    public static string EmailPattern(AccountSearchQuery query) => query.Value.ToLowerInvariant();
}
