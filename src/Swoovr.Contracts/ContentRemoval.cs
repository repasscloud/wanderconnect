namespace Swoovr.Contracts;

// Stable paths/date contract. ExpectedSourceSha256 identifies the exact staff-reviewed text, never raw text in a URL.
public sealed record ContentRemovalRequest(Guid CommandId,Guid ReportId,string Field,string? ModeId,Guid? MessageId,
    string ExpectedSourceSha256,string Reason,ModerationReportingChoice? Reporting=null);
public sealed record ContentRemovalReceipt(Guid Id,Guid CaseId,string Field,DateTimeOffset RemovedAt,bool AlreadyApplied);

/// <summary>Only currently authorised case source text, body-only over no-store staff transport.</summary>
public sealed record ContentRemovalSource(Guid ReportId,string Field,string? ModeId,Guid? MessageId,string Text,string Sha256);
public sealed record ContentRemovalEvidence(Guid Id,Guid CaseId,string Field,string? ModeId,Guid? MessageId,DateTimeOffset RemovedAt,string Text,string Reason);
