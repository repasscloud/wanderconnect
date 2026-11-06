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
    /// <summary>D333/D337: set in the same transaction as final account deletion; the retirement worker then retires the customer.</summary>
    public DateTimeOffset? RetirementRequestedAt { get; set; }
    /// <summary>136.3 3a: the opaque `retired-…` login, chosen before the first attempt so retries are idempotent.</summary>
    public string? RetiredLogin { get; set; }
    public DateTimeOffset? RetiredAt { get; set; }
    public int RetirementAttempts { get; set; }
    public DateTimeOffset? NextRetirementAt { get; set; }
    public string? RetirementFailure { get; set; }
    /// <summary>The retired customer itself was deleted once none of its tickets remained.</summary>
    public DateTimeOffset? CustomerDeletedAt { get; set; }
    /// <summary>The Zammad data privacy task deleting the retired customer.</summary>
    public long? CustomerDeletionTaskId { get; set; }
    public bool Retiring => RetirementRequestedAt is not null;
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
    /// <summary>Selects which customer to re-fetch as; the helpdesk's own access rules then decide what is visible.</summary>
    public long? ProviderCustomerId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int Attempts { get; set; }
    public string? FailureCode { get; set; }
}
