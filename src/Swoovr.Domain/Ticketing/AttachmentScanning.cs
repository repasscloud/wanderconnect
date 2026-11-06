namespace Swoovr.Domain;

/// <summary>D336 (#221): the owner's scan states. Only <see cref="Clean"/> releases a file.</summary>
public enum AttachmentScanState { Pending, Scanning, Clean, Infected, Failed, Unsupported }

/// <summary>
/// One scanner answer. <see cref="Detection"/> is the signature name when infected; <see cref="Failure"/> a short code
/// (for example "scanner-unavailable", "scan-timeout", "scan-limit"). A clean result is not a guarantee a file is harmless.
/// </summary>
public sealed record MalwareScanResult(AttachmentScanState State, string Scanner, string? EngineVersion, string? SignatureVersion,
    string? Detection = null, string? Failure = null)
{
    public bool Final => State is AttachmentScanState.Clean or AttachmentScanState.Infected or AttachmentScanState.Unsupported;
}

/// <summary>
/// D336: reusable malware scanning for untrusted files (support attachments now, partner documents later). Separate from
/// photo moderation, explicit-content screening and identity verification. Implementations never throw for scanner
/// trouble: they answer <see cref="AttachmentScanState.Failed"/> with a failure code, so nothing is ever released by error.
/// </summary>
public interface IAttachmentMalwareScanner
{
    /// <summary>`clamav` or `disabled` (Development and tests only).</summary>
    string Name { get; }
    Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken = default);
    /// <summary>True when the scanner answers its health check.</summary>
    Task<bool> HealthyAsync(CancellationToken cancellationToken = default);
}

/// <summary>Who sent a quarantined file, and so as whom it is released into the helpdesk.</summary>
public enum AttachmentSource { Member, Staff }

/// <summary>
/// D336: a support attachment held in private quarantine (the media store, key `attachments/…`) until scanned. The message's
/// text reaches the helpdesk at once; its files reach Zammad only after every file of the message has a final result, as a
/// follow-up article from the same author with the clean files. Infected, failed or unsupported files never leave quarantine.
/// </summary>
public sealed class SupportAttachmentHold
{
    public Guid Id { get; set; }
    /// <summary>The files of one message share a group; released together.</summary>
    public Guid GroupId { get; set; }
    public long ProviderTicketId { get; set; }
    /// <summary>The text article the files belong to (known once the message was accepted).</summary>
    public long? ProviderArticleId { get; set; }
    public AttachmentSource Source { get; set; }
    public Guid AuthorAccountId { get; set; }
    /// <summary>Staff private notes keep their files private.</summary>
    public bool Private { get; set; }
    public int Position { get; set; }
    public string Filename { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long Size { get; set; }
    public string Sha256 { get; set; } = "";
    /// <summary>Null once the quarantined copy is removed (after release or a final refusal).</summary>
    public string? ObjectKey { get; set; }
    public AttachmentScanState State { get; set; }
    public string? Scanner { get; set; }
    public string? EngineVersion { get; set; }
    public string? SignatureVersion { get; set; }
    public string? Detection { get; set; }
    public string? FailureCode { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? ScanStartedAt { get; set; }
    public DateTimeOffset? ScanCompletedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>The release operation (also the follow-up article's operation reference).</summary>
    public Guid? ReleaseOperationId { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public long? ReleaseArticleId { get; set; }
}

/// <summary>
/// D336: the latest scan of a file's content (SHA-256), used before any download from Swoovr: helpdesk files that never
/// passed Swoovr's quarantine (emailed attachments, files from before scanning) are scanned on first download.
/// </summary>
public sealed class AttachmentScanVerdict
{
    public string Sha256 { get; set; } = "";
    public AttachmentScanState State { get; set; }
    public string Scanner { get; set; } = "";
    public string? EngineVersion { get; set; }
    public string? SignatureVersion { get; set; }
    public string? Detection { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset ScannedAt { get; set; }
}

public static class AttachmentScanPolicy
{
    public const int MaximumAttempts = 12;
    /// <summary>A clean verdict is re-checked against newer signatures after this long, before another download.</summary>
    public static readonly TimeSpan CleanVerdictLifetime = TimeSpan.FromDays(1);
    /// <summary>Quarantined copies of refused files are removed after this long (the hold row keeps only the facts).</summary>
    public static readonly TimeSpan RefusedCopyRetention = TimeSpan.FromDays(30);
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromSeconds(Math.Min(3600, 15 * Math.Pow(2, Math.Max(0, attempts - 1))));
    /// <summary>Only a fresh clean verdict lets a file be downloaded.</summary>
    public static bool Downloadable(AttachmentScanVerdict? verdict, DateTimeOffset now) =>
        verdict is { State: AttachmentScanState.Clean } && verdict.ScannedAt > now - CleanVerdictLifetime;
    /// <summary>A group is released once every file has a final result; files that are still failing keep the group waiting.</summary>
    public static bool GroupSettled(IEnumerable<SupportAttachmentHold> files) => files.All(f => f.State is AttachmentScanState.Clean or AttachmentScanState.Infected or AttachmentScanState.Unsupported
        || f.State == AttachmentScanState.Failed && f.Attempts >= MaximumAttempts);
}
