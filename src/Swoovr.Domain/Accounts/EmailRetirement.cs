namespace Swoovr.Domain;

public enum EmailRetirementState { Pending, NeedsReview, Completed }

/// <summary>Durable restricted intent committed before provider effects; ids do not establish contact ownership.</summary>
public sealed class EmailRetirementOperation
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid RegistryId { get; set; }
    public Guid CaseId { get; set; }
    public Guid RequestedBy { get; set; }
    public Guid AccessGrantId { get; set; }
    /// <summary>Optional later immutable approval; original requester/grant remain unchanged.</summary>
    public Guid? CurrentReviewId { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public EmailRetirementState State { get; set; }
    public int Attempts { get; set; }
    /// <summary>Machine outcome only, never provider response/exception text or contacts.</summary>
    public string Outcome { get; set; } = "prepared";
    /// <summary>Contact-history key envelope containing exact CAS and bound operation/source provenance.</summary>
    public byte[] EncryptedCommand { get; set; } = [];
}

/// <summary>Restricted immutable approval to resume the same command, not proof of identity or a replacement CAS.</summary>
public sealed class EmailRetirementReview
{
    public Guid Id { get; set; }
    public Guid OperationId { get; set; }
    public Guid ActorId { get; set; }
    public Guid AccessGrantId { get; set; }
    public DateTimeOffset ReviewedAt { get; set; }
    public string Reason { get; set; } = "";
}

public static class EmailRetirementPolicy
{
    public static void ApproveResume(EmailRetirementOperation operation,EmailRetirementReview review,DateTimeOffset now)
    {
        if(operation.State==EmailRetirementState.Completed||operation.CompletedAt is not null||review.Id==Guid.Empty
            ||operation.CurrentReviewId==review.Id||review.OperationId!=operation.Id||review.ActorId==Guid.Empty||review.AccessGrantId==Guid.Empty
            ||review.ReviewedAt>now||review.ReviewedAt<operation.RequestedAt||string.IsNullOrWhiteSpace(review.Reason)
            ||review.Reason.Trim().Length>BannedIdentifierPolicy.ReasonMax)
            throw new InvalidOperationException("A bounded current review of this unfinished command is required.");
        operation.CurrentReviewId=review.Id;operation.State=EmailRetirementState.Pending;operation.UpdatedAt=now;
        operation.NextAttemptAt=now;operation.Outcome="review-approved";
    }
    public static bool BlocksAccess(EmailRetirementOperation operation)=>operation.State!=EmailRetirementState.Completed;
    public static void Observe(EmailRetirementOperation operation,IdentityContactState observation,DateTimeOffset now)
    {
        if(operation.State==EmailRetirementState.Completed)return;
        operation.UpdatedAt=now;operation.Attempts=checked(operation.Attempts+1);
        operation.NextAttemptAt=now.AddMinutes(5);operation.Outcome=observation.ToString();
        if(observation==IdentityContactState.Retired){operation.State=EmailRetirementState.Completed;operation.CompletedAt=now;}
        else if(observation is IdentityContactState.BindingChanged or IdentityContactState.Conflict or IdentityContactState.NotSupported)
            operation.State=EmailRetirementState.NeedsReview;
    }
}
