namespace Swoovr.Contracts;

/// <summary>Restricted staff metadata only: never the fingerprint, plaintext contact, or a matching-person assertion.</summary>
public sealed record BannedIdentifierView(Guid Id, string IdentifierType, Guid CaseId, string ReasonCode, string Status,
    DateTimeOffset BannedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? LastReviewedAt, DateTimeOffset ReviewDue,
    DateTimeOffset? LastMatchedAt, long MatchCount, DateTimeOffset? RevokedAt, Guid? RevokedBy);
public sealed record IdentifierLookup(string Type, string Value, string Purpose);
public sealed record IdentifierReviewRequest(string Reason);
public sealed record IdentifierReleaseRequest(IReadOnlyList<Guid> IdentifierIds, string Reason);
public sealed record AccountReinstatementRequest(IReadOnlyList<Guid> RevokeIdentifierIds, string Reason);
public sealed record IdentifierWorkflowResult(int Changed);
public sealed record BanHistoryView(Guid Id, Guid CaseId, Guid? ActorId, DateTimeOffset At, string Action, string? OldStatus, string NewStatus, string Reason);
public sealed record BanNoteView(Guid CaseId, Guid TransitionId, Guid? AuthorId, DateTimeOffset At, string Text);
public sealed record BannedIdentifierDetail(BannedIdentifierView Entry, IReadOnlyList<BanHistoryView> History, IReadOnlyList<BanNoteView> Notes);
