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
/// <summary>Typed client wrapper for the API's metadata array and opaque continuation header.</summary>
public sealed record BannedIdentifierPage(IReadOnlyList<BannedIdentifierView> Items, Guid? NextCursor);
/// <summary>Fresh restricted staff snapshot only; the action rechecks eligibility and exact selected IDs.</summary>
public sealed record AccountReinstatementPreview(Guid AccountId,string State,bool Eligible,string? BlockedCode,
    IReadOnlyList<Guid> CaseIds,IReadOnlyList<BannedIdentifierView> Identifiers,bool OtherSanctions,IReadOnlyList<Guid> RequiredIdentifierIds,IReadOnlyList<BanDispositionView>? Dispositions=null);

/// <summary>Lawfully minimized staff source metadata; no matchable identifier survives in this receipt.</summary>
public sealed record BanDispositionView(Guid Id,Guid CaseId,string IdentifierType,string Status,DateTimeOffset EffectiveEnd,DateTimeOffset DisposedAt,IReadOnlyList<BanDispositionReviewView> Reviews);
public sealed record BanDispositionReviewView(Guid Id,Guid CaseId,Guid ActorId,DateTimeOffset At,string Reason);

/// <summary>Recorded circumstances/ownership review, separate from identifier release.</summary>
public sealed record MobileBindingRetirementRequest(bool CircumstancesReviewed, string Reason);

public sealed record EmailBindingRetirementRequest(bool CircumstancesReviewed,string Reason);
/// <summary>Restricted operation metadata only; never raw contact, CAS snapshot or provider diagnostic.</summary>
public sealed record EmailBindingRetirementView(Guid OperationId,string State,DateTimeOffset RequestedAt,DateTimeOffset UpdatedAt,DateTimeOffset? CompletedAt);
