namespace Swoovr.Contracts;

// Legal holds, the evidence store and the disclosure register (policy §3, §13; D189). Admin API only (D136): never on the
// member BFF allow-list. Only the `legal` role reads holds; an admin may open a provisional one from a case.

/// <summary>
/// A new hold. AccountIds (1–20) are application account ids; ConversationIds (optional) narrow it to those
/// conversations; From/Until bound the preserved period; ReviewDue defaults to 90 days (at most a year). An admin must
/// give CaseId and only that case's subject and reporters, and gets a provisional hold.
/// </summary>
/// <remarks>D195 (§7): LeCaseId links a law-enforcement case (its agency becomes the requesting body); Kind
/// "police-preservation" is a temporary police preservation hold (verified case, review within 30 days).</remarks>
public sealed record NewLegalHold(string? Reason, List<Guid>? AccountIds, List<Guid>? ConversationIds, DateTimeOffset? From, DateTimeOffset? Until,
    DateTimeOffset? ReviewDue, string? ReleaseConditions, Guid? CaseId, Guid? LeCaseId = null, string? Kind = null, string? LawfulBasis = null);

/// <summary>Status is provisional, active or released; source is staff or serious-incident.</summary>
public sealed record LegalHoldSummary(Guid Id, string Status, string Source, Guid? CaseId, DateTimeOffset PlacedAt, DateTimeOffset ReviewDue, bool Overdue,
    int Accounts, int Items);

/// <summary>Item metadata only (kind, provenance, capture time and SHA-256); content leaves only in an export.</summary>
public sealed record EvidenceItemView(Guid Id, int Capture, string Kind, string Source, string RecordId, Guid? AccountId, DateTimeOffset CapturedAt, string Sha256);

public sealed record CustodyEventView(long Sequence, DateTimeOffset At, Guid? Actor, string Action, string? Detail, string Hash);

public sealed record DisclosureView(Guid Id, Guid HoldId, string Agency, string Officer, string Reference, string LegalBasis, string VerifiedHow,
    Guid DisclosedBy, DateTimeOffset DisclosedAt);

public sealed record LegalHoldDetail(Guid Id, string Status, string Source, string Reason, string? ReleaseConditions, Guid? CaseId, Guid? PlacedBy,
    string PlacedByRole, DateTimeOffset PlacedAt, Guid? ConfirmedBy, DateTimeOffset? ConfirmedAt, DateTimeOffset ReviewDue, bool Overdue,
    DateTimeOffset? From, DateTimeOffset? Until, List<Guid> AccountIds, List<Guid> ConversationIds, List<EvidenceItemView> Items,
    List<CustodyEventView> Custody, bool ChainIntact, List<DisclosureView> Disclosures, DateTimeOffset? ReleasedAt, string? ReleaseReason,
    Guid? LeCaseId = null, string? Kind = null, string? LawfulBasis = null, string? RequestingBody = null, int Overlaps = 0, Guid? ReleasedBy = null,
    List<EvidenceExportView>? Exports = null, DateTimeOffset? RetainUntil = null, string? RetentionReason = null,
    DateTimeOffset? NoReviewRetentionConfirmedAt = null, bool MayConfirmNoReview = false);

/// <summary>An export made from a hold (D195): who, when, whether downloaded, its package hash, and what it corrects.</summary>
public sealed record EvidenceExportView(Guid Id, Guid CreatedBy, DateTimeOffset CreatedAt, DateTimeOffset? DownloadedAt, string? PackageSha256,
    Guid? LeCaseId, Guid? SupplementsExportId, int Redactions);

/// <summary>For the admin and legal home pages: how many holds are live and how many are overdue for review.</summary>
public sealed record HoldOverview(int Live, int Overdue);

public sealed record HoldReviewRequest(DateTimeOffset? ReviewDue);

/// <summary>OverlapsChecked (D195, §7): required when other live holds cover the same accounts.</summary>
public sealed record HoldReleaseRequest(string? Reason, bool OverlapsChecked = false);

/// <summary>LegalBasis: app-6.2e, warrant, subpoena, court-order, serious-threat or consent.</summary>
public sealed record DisclosureRequest(string? Agency, string? Officer, string? Reference, string? LegalBasis, string? VerifiedHow);

/// <summary>Purpose is required; Disclosure records a law-enforcement disclosure in the register.</summary>
/// <remarks>D195 (§6, §8): LeCaseId ties it to a case; SupplementsExportId with CorrectionReason makes a traceable correction
/// that never overwrites the original; Redactions name item fields (dotted JSON paths) blanked in the disclosure copy only.</remarks>
public sealed record EvidenceExportRequest(string? Purpose, DisclosureRequest? Disclosure, Guid? LeCaseId = null, Guid? SupplementsExportId = null,
    string? CorrectionReason = null, List<RedactionRequest>? Redactions = null, Guid? ApprovalId = null);

public sealed record RedactionRequest(Guid ItemId, List<string>? Fields, string? Reason);

/// <summary>A one-time link: GET /api/admin/evidence-exports/{Token} within 15 minutes, by the same officer.</summary>
public sealed record EvidenceExportLink(string Token, DateTimeOffset ExpiresAt);

/// <summary>Explicit independent legal disposition, never inferred merely from missing case links.</summary>
public sealed record HoldRetentionDispositionRequest(string? Reason, bool Confirmed);
