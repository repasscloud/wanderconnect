using System.Text.RegularExpressions;

namespace Swoovr.Domain;

/// <summary>Kinds of external help (#103, issue spec), shown in this order with Emergency first.</summary>
public enum SafetyResourceCategory { Emergency, PoliceNonEmergency, Crisis, SexualViolence, DomesticViolence, OnlineSafety, ChildExploitation, Other }

/// <summary>
/// How far a country's resource pack can be relied on (#103): Ready is checked and may be launched; NotReady is
/// populated but not yet verified (shown with a warning); Demo is reference data only, never a launch commitment.
/// </summary>
public enum SafetyReadiness { NotReady, Ready, Demo }

/// <summary>A country in the Safety &amp; Support selector. Disabled countries aren't offered and their resources aren't served.</summary>
public sealed class SafetyCountry
{
    /// <summary>ISO 3166-1 alpha-2, upper case.</summary>
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Enabled { get; set; }
    public SafetyReadiness Readiness { get; set; }
    public DateOnly? LastReviewedOn { get; set; }
    public DateOnly? ReviewDueOn { get; set; }
    /// <summary>Who at Swoovr owns the pack (a role or team, not a member).</summary>
    public string? Owner { get; set; }
    public string? Notes { get; set; }
    /// <summary>Goes up with every change to the country or its resources, so cached packs can tell they are old.</summary>
    public int Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>One external contact for a country (#103): data, edited by admins, never hard-coded in a client.</summary>
public sealed class SafetyResource
{
    public Guid Id { get; set; }
    public string CountryCode { get; set; } = "";
    /// <summary>A state or province when it only applies there; null for the whole country.</summary>
    public string? RegionCode { get; set; }
    public string LanguageCode { get; set; } = "en";
    public SafetyResourceCategory Category { get; set; }
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>Dialable digits (a leading + allowed), e.g. 000 or 1800737732.</summary>
    public string? Phone { get; set; }
    /// <summary>How the number is written for people, e.g. "1800 737 732".</summary>
    public string? PhoneDisplay { get; set; }
    public string? WebsiteUrl { get; set; }
    /// <summary>The official page the details were checked against.</summary>
    public string SourceUrl { get; set; } = "";
    public int DisplayOrder { get; set; }
    public bool Enabled { get; set; }
    public DateOnly? LastVerifiedOn { get; set; }
    public DateOnly? ReviewDueOn { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}

/// <summary>An immutable record of every change to a country or a resource (who, when, what it became).</summary>
public sealed class SafetyRevision
{
    public long Id { get; set; }
    public string CountryCode { get; set; } = "";
    public Guid? ResourceId { get; set; }
    /// <summary>country.updated, resource.created or resource.updated.</summary>
    public string Action { get; set; } = "";
    public Guid? ActorAccountId { get; set; }
    public DateTimeOffset At { get; set; }
    public string SnapshotJson { get; set; } = "";
}

/// <summary>Where the country shown came from: manual (the member chose it), cloudflare, fallback (deployment setting) or unknown.</summary>
public sealed record SafetyCountryChoice(string? Code, string Source);

public static partial class SafetyPolicy
{
    public const int NameMax = 100, DescriptionMax = 300, UrlMax = 300, NotesMax = 500, PhoneMax = 20;
    /// <summary>DEFAULT (D204): a pack's review comes round every 90 days.</summary>
    public static readonly TimeSpan ReviewInterval = TimeSpan.FromDays(90);

    [GeneratedRegex("^[A-Z]{2}$")] private static partial Regex Iso();
    [GeneratedRegex(@"^\+?[0-9]{2,15}$")] private static partial Regex Dialable();
    [GeneratedRegex("^[a-z]{2}(-[A-Z]{2})?$")] private static partial Regex Language();

    /// <summary>A well-formed two-letter code, upper-cased; XX, T1 and anything else malformed are not a country.</summary>
    public static string? Normalise(string? code)
    {
        var value = code?.Trim().ToUpperInvariant();
        return value is null || !Iso().IsMatch(value) || value is "XX" ? null : value;
    }

    /// <summary>
    /// #103: a manual choice wins; else Cloudflare's CF-IPCountry, but only when the host is behind Cloudflare and has
    /// said so (D153; never trusted otherwise, so a client can't supply it); else the deployment's fallback (AU locally);
    /// else unknown. A country Swoovr doesn't list stays itself (shown with neutral guidance), never another country's numbers.
    /// </summary>
    public static SafetyCountryChoice Resolve(string? manual, string? cloudflareHeader, bool cloudflareTrusted, string? fallback)
    {
        if (Normalise(manual) is { } chosen) return new(chosen, "manual");
        if (cloudflareTrusted && Normalise(cloudflareHeader) is { } fromNetwork) return new(fromNetwork, "cloudflare");
        if (Normalise(fallback) is { } configured) return new(configured, "fallback");
        return new(null, "unknown");
    }

    public static bool ValidPhone(string? phone) => phone is null || Dialable().IsMatch(phone);
    public static bool ValidLanguage(string language) => Language().IsMatch(language);

    /// <summary>Official links only over HTTPS.</summary>
    public static bool ValidUrl(string? url, bool required) =>
        url is null ? !required : url.Length <= UrlMax && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && !string.IsNullOrEmpty(uri.Host);

    /// <summary>What stops a resource being saved (null when it can be).</summary>
    public static string? CheckResource(SafetyResource r)
    {
        if (Normalise(r.CountryCode) != r.CountryCode) return "country";
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Length > NameMax) return "name";
        if (string.IsNullOrWhiteSpace(r.Description) || r.Description.Length > DescriptionMax) return "description";
        if (!ValidPhone(r.Phone) || r.PhoneDisplay is { Length: > PhoneMax }) return "phone";
        if (!ValidUrl(r.WebsiteUrl, false)) return "website";
        if (!ValidUrl(r.SourceUrl, true)) return "source";
        if (r.Phone is null && r.WebsiteUrl is null) return "contact";
        if (!ValidLanguage(r.LanguageCode)) return "language";
        if (r.RegionCode is { Length: > 10 }) return "region";
        if (r.DisplayOrder is < 0 or > 1000) return "order";
        if (r.LastVerifiedOn is { } verified && r.ReviewDueOn is { } due && due < verified) return "review-date";
        return null;
    }

    /// <summary>
    /// #103 launch readiness: a verified, enabled emergency resource with a number; every enabled resource verified and not
    /// past its review; the pack itself reviewed and not past its review date. Returns the problems (none = may be Ready).
    /// </summary>
    public static IReadOnlyList<string> LaunchProblems(SafetyCountry country, IReadOnlyCollection<SafetyResource> resources, DateOnly today)
    {
        var problems = new List<string>();
        var enabled = resources.Where(r => r.Enabled).ToList();
        if (!enabled.Any(r => r.Category == SafetyResourceCategory.Emergency && r.Phone is not null && r.LastVerifiedOn is not null))
            problems.Add("no-verified-emergency-number");
        if (enabled.Any(r => r.LastVerifiedOn is null)) problems.Add("unverified-resources");
        if (enabled.Any(r => r.ReviewDueOn is { } due && due < today)) problems.Add("resource-review-overdue");
        if (country.LastReviewedOn is null) problems.Add("pack-not-reviewed");
        if (country.ReviewDueOn is { } packDue && packDue < today) problems.Add("pack-review-overdue");
        return problems;
    }

    /// <summary>Whether a pack shown to people must carry the "not verified or out of date" warning.</summary>
    public static bool Stale(SafetyCountry country, IReadOnlyCollection<SafetyResource> shown, DateOnly today) =>
        country.Readiness != SafetyReadiness.Ready || LaunchProblems(country, shown, today).Count > 0;
}
