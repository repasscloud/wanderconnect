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

public static class EmailRetirementPolicy
{
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
