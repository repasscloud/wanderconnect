namespace Swoovr.Domain;

/// <summary>Immutable after explicit operator publication. Holds no policy text, member data or legal acceptance.</summary>
public sealed class PolicyNoticePublication
{
    public Guid Id { get; set; }
    public string Version { get; set; } = "";
    public string Title { get; set; } = "";
    public string Link { get; set; } = "";
    public DateTimeOffset StagedAt { get; set; }
    public Guid? StagedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid? PublishedBy { get; set; }
    public DateTimeOffset? SendUntil { get; set; }
    public DateTimeOffset? PurgeAfter { get; set; }
    public DateTimeOffset? DisposedAt { get; set; }
}

/// <summary>Durable centre outbox and lifetime dedupe tombstone, not an email/push campaign.</summary>
public sealed class PolicyNoticeRecipient
{
    public Guid PublicationId { get; set; }
    public Guid AccountId { get; set; }
    public string Status { get; set; } = "queued";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public static class PolicyNoticePolicy
{
    public const int BatchSize = 100;
    public static bool ValidVersion(string? version) => version is { Length: >= 1 and <= 40 }
        && char.IsAsciiLetterOrDigit(version[0]) && version.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-');
    public static bool ValidTitle(string? title) => title is { Length: >= 1 and <= 120 }
        && !string.IsNullOrWhiteSpace(title) && !title.Any(char.IsControl);
    // Notice links are limited to the safety sources. The approved terms, guidelines and privacy notice (D288/D290) change
    // through a new approved version and its own significant-change notice, not through this broadcast path.
    public static bool AllowedLink(string? link) => link is "/safety" or "/safety/advice";
    public static string Dedupe(Guid publication) => "policy-notice:" + publication.ToString("N");
    public static bool CanPublish(PolicyNoticePublication row, bool sourceIsPublished) => sourceIsPublished
        && row.DisposedAt==null && row.PublishedAt == null && ValidVersion(row.Version) && ValidTitle(row.Title) && AllowedLink(row.Link);
    public static bool CanDisplay(PolicyNoticePublication? row, DateTimeOffset now) => row is { PublishedAt: not null, PurgeAfter: not null }
        && row.DisposedAt==null && row.PublishedAt <= now && row.PurgeAfter > now && ValidVersion(row.Version) && ValidTitle(row.Title) && AllowedLink(row.Link);
    public static bool Eligible(bool activeMember, DateTimeOffset accountCreated, DateTimeOffset publishedAt) => activeMember && accountCreated <= publishedAt;
}
