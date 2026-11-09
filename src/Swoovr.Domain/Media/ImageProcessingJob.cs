namespace Swoovr.Domain;

/// <summary>#294 staging foundation: separate from deliverable MediaItem until custody/review/delivery cutover is verified.</summary>
public sealed class ImageProcessingJob
{
    public Guid Id { get; set; }
    public Guid ImageId { get; set; }
    public Guid UploadId { get; set; }
    public Guid AccountId { get; set; }
    public string SourceKey { get; set; } = "";
    public string? SourceEtag { get; set; }
    public string? SourceSha256 { get; set; }
    public string Purpose { get; set; } = "profile";
    public string State { get; set; } = "awaiting-upload";
    public string PolicyVersion { get; set; } = "";
    public long LifecycleRevision { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UploadExpiresAt { get; set; }
    public DateTimeOffset? UploadedAt { get; set; }
    public string? ResultsJson { get; set; }
    public DateTimeOffset? ClaimedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public string? Outcome { get; set; }
    public string? Reason { get; set; }
    public string? VariantsJson { get; set; }
    public Guid? CompletedEventId { get; set; }
}

/// <summary>Immutable, minimum durable callback receipt. Digest binds the complete canonical report; no raw provider bodies.</summary>
public sealed class ImageProcessingReceipt
{
    public Guid EventId { get; set; }
    public Guid JobId { get; set; }
    public string PayloadDigest { get; set; } = "";
    public DateTimeOffset CommittedAt { get; set; }
}
