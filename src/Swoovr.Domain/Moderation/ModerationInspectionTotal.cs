namespace Swoovr.Domain;

/// <summary>Internal anonymous daily attempts, never personal receipts, accuracy or unique incidents.</summary>
public sealed class ModerationInspectionTotal
{
    public DateOnly Day { get; set; } // UTC inspection/attempt completion day, not source creation day.
    public string Environment { get; set; } = "unknown";
    public string Definition { get; set; } = "";
    public long CompletedInspections { get; set; }
    public long IncompleteHistory { get; set; }
    public long CreatedCandidates { get; set; }
    public long FailedProcessingAttempts { get; set; }
}
