namespace Swoovr.Contracts;
public sealed record TextHarmCandidateSummary(Guid Id,string Source,string? ModeId,DateTimeOffset CreatedAt,DateTimeOffset? ReviewedAt,string? Outcome);
public sealed record TextHarmCandidateEvidence(Guid Id,Guid CaseId,string Source,string? ModeId,DateTimeOffset CreatedAt,string Text,string[] Rules,string[] Categories,DateTimeOffset? ReviewedAt,string? Outcome);
public sealed record TextHarmCandidateReviewReceipt(Guid Id,Guid CaseId,string Outcome,DateTimeOffset ReviewedAt,bool AlreadyApplied);
public sealed record TextHarmCandidateReviewRequest(string Outcome,Guid? SanctionId,Guid? RemovalId);
