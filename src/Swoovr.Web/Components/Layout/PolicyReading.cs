using System.Globalization;
using System.Text.RegularExpressions;
using Swoovr.Client;

namespace Swoovr.Web.Components.Layout;

/// <summary>Presentation-only shaping of the approved policy text (D288/D290). The generated wording is never edited:
/// a leading "N. Heading." becomes a numbered heading and the remaining sentences its body.</summary>
public static partial class PolicyReading
{
    public sealed record Section(string Id, string? Number, string Title, IReadOnlyList<string> Paragraphs, IReadOnlyList<string> Points);
    public sealed record Related(string Href, string Title, string Description);
    public sealed record Meta(string? Updated, string? Version);

    public static readonly IReadOnlyList<Related> Pages =
    [
        new("/terms", "Terms of Use", "The agreement between you and Swoovr."),
        new("/community-guidelines", "Community Guidelines", "How everyone on Swoovr is expected to behave."),
        new("/privacy", "Privacy Notice", "What we collect, why, and your choices."),
        new("/safety/advice", "Safety Advice", "Before you connect with or meet someone."),
        new("/safety", "Safety & Support", "Help and contacts in your country."),
    ];

    /// <summary>"N. Heading. Body…" → numbered section; text without that shape keeps its full wording as body.</summary>
    public static Section Numbered(string prefix, string paragraph, int fallbackNumber)
    {
        var match = NumberedPattern().Match(paragraph);
        var number = match.Success ? match.Groups[1].Value : fallbackNumber.ToString(CultureInfo.InvariantCulture);
        var rest = match.Success ? match.Groups[2].Value : paragraph;
        var (title, body) = Lead(rest);
        return new($"{prefix}-{number}", number, title.TrimEnd('.'), body.Length == 0 ? [] : [body], []);
    }

    /// <summary>Splits the first sentence (the clause heading) from the rest without changing either.</summary>
    public static (string Title, string Body) Lead(string text)
    {
        var match = LeadPattern().Match(text);
        return match.Success ? (match.Groups[1].Value, match.Groups[2].Value) : (text, "");
    }

    /// <summary>Terms/guidelines "2026-11-02-0.2" and privacy "0.17, 2 November 2026" become a date and a version.</summary>
    public static Meta Describe(string version)
    {
        var dated = DatedVersion().Match(version);
        if (dated.Success && DateOnly.TryParseExact(dated.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return new(Long(date), dated.Groups[2].Value.Length > 0 ? dated.Groups[2].Value : null);
        var parts = version.Split(',', 2, StringSplitOptions.TrimEntries);
        if (parts.Length == 2 && DateOnly.TryParseExact(parts[1], "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var notice))
            return new(Long(notice), parts[0]);
        return new(null, version);
    }

    private static string Long(DateOnly date) => date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(\d+)\.\s+(.+)$", RegexOptions.Singleline)]
    private static partial Regex NumberedPattern();

    [GeneratedRegex(@"^(.+?[.!?])\s+([A-Z""'].*)$", RegexOptions.Singleline)]
    private static partial Regex LeadPattern();

    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})(?:[-.](.+))?$")]
    private static partial Regex DatedVersion();
}
