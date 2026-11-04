namespace Swoovr.Domain;

/// <summary>Integration identity only; email and conversation content are authoritative elsewhere.</summary>
public sealed class TicketingIdentity
{
    public Guid AccountId { get; set; }
    public long? ProviderUserId { get; set; }
    public TicketingOperationState State { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeactivatedAt { get; set; }
}
public enum TicketingOperationState { Pending, Applied, Uncertain, Rejected }

/// <summary>Durable write intent. Digest binds an idempotency key without storing the submitted support message.</summary>
public sealed class TicketingOperation
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Kind { get; set; } = "";
    public string RequestDigest { get; set; } = "";
    public TicketingOperationState State { get; set; }
    public long? ProviderTicketId { get; set; }
    public long? ProviderArticleId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Safe link to a Swoovr authoritative case; no conversation or evidence copy.</summary>
public sealed class TicketingCaseLink
{
    public Guid Id { get; set; }
    public long ProviderTicketId { get; set; }
    public Guid CaseId { get; set; }
    public Guid? AppealId { get; set; }
    public Guid LinkedBy { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
}

/// <summary>Durable ids-only event inbox. Accepted work is stored before HTTP acknowledgment.</summary>
public sealed class TicketingWebhookWork
{
    public Guid Id { get; set; }
    public string DeliveryId { get; set; } = "";
    public string BodyDigest { get; set; } = "";
    public long ProviderTicketId { get; set; }
    public long? ProviderArticleId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Attempts { get; set; }
    public string? FailureCode { get; set; }
}
