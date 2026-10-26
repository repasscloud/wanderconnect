namespace Swoovr.Contracts;

/// <summary>A report category (D131) as members see it: id, label and nothing about priority handling.</summary>
public sealed record ReportCategoryView(string Id, string Label);

/// <summary>
/// A member's report (D131). SubjectType is "profile", "request" or "bot" today ("conversation", "message", "photo" and
/// "album" arrive with those features); SubjectId is the profile's public id or the request id. Text is optional, up to
/// 2,000 characters. Block also blocks the person in the same step.
/// </summary>
public sealed record NewReport(string SubjectType, string SubjectId, string Category, string? Text, bool Block);

/// <summary>What the reporter sees (D131): submitted, underReview, reviewed or closed; never the outcome.</summary>
public sealed record MyReportView(Guid Id, string SubjectType, string Category, string Status, DateTimeOffset CreatedAt);

/// <summary>
/// A notice about an action on the member's own account (D135). Sanction details are the member's own: the kind, the
/// staff-written reason, when it ends, what it restricts, and whether (and how) it may be appealed.
/// </summary>
public sealed record NoticeView(Guid Id, string Kind, DateTimeOffset CreatedAt, bool Read, SanctionNoticeView? Sanction);
public sealed record SanctionNoticeView(Guid Id, string Kind, string Reason, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool Live,
    bool HideFromDiscovery, bool NoNewRequests, bool NoMessages, bool CanAppeal, string? AppealStatus);
public sealed record NewAppeal(Guid SanctionId, string? Text);
public sealed record AppealView(Guid Id, Guid SanctionId, string Status, DateTimeOffset CreatedAt);

/// <summary>The member's effective plan from the entitlement service (D151): tier, where it comes from and until when.</summary>
public sealed record PlanView(string Tier, string? Source, DateTimeOffset? EndsAt, bool Complimentary);

// Staff views (admin API, D136). Ids and the minimum needed to decide; never email, tokens or verification evidence.

public sealed record CaseSummaryView(Guid Id, Guid SubjectAccountId, string? SubjectName, string Status, string Queue, string Priority,
    int Reports, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool AwaitingAcknowledgement);
public sealed record CaseReportView(Guid Id, Guid ReporterAccountId, string SubjectType, string SubjectRef, string Category, string Priority,
    string? Text, DateTimeOffset CreatedAt);
public sealed record CaseEvidenceView(Guid ReportId, string Kind, string Snapshot, DateTimeOffset CapturedAt);
public sealed record SanctionView(Guid Id, Guid AccountId, Guid? CaseId, string Kind, string Reason, DateTimeOffset StartsAt, DateTimeOffset? EndsAt,
    bool HideFromDiscovery, bool NoNewRequests, bool NoMessages, Guid CreatedBy, string CreatedByRole, DateTimeOffset? LiftedAt, bool Live);
public sealed record CaseDetailView(CaseSummaryView Case, bool StaffAffiliatedSubject, IReadOnlyList<CaseReportView> Reports,
    IReadOnlyList<CaseEvidenceView> Evidence, IReadOnlyList<SanctionView> Sanctions);

/// <summary>
/// A staff action (D133): "dismiss", "warn", "restrict", "suspend" or "terminate", with a reason (required except to
/// dismiss), days for a restriction (up to 30) or suspension (up to 90), and what a restriction covers.
/// </summary>
public sealed record ModerationActionRequest(string Action, string? Reason, int? Days, bool HideFromDiscovery, bool NoNewRequests, bool NoMessages);
public sealed record LiftRequest(string? Reason);
/// <summary>"uphold" or "overturn" (D135).</summary>
public sealed record AppealDecision(string Outcome);
public sealed record AppealAdminView(Guid Id, Guid SanctionId, Guid AccountId, string SanctionKind, string? Text, string Status,
    DateTimeOffset CreatedAt, Guid? DecidedBy, DateTimeOffset? DecidedAt);

public sealed record StaffLinkRequest(Guid? MemberAccountId);
public sealed record StaffLinkView(Guid StaffAccountId, Guid MemberAccountId, DateTimeOffset LinkedAt);
/// <summary>D150: an admin lets a member move their home city into another market now, with a reason (audited, never shown publicly).</summary>
public sealed record HomeMoveOverride(string? Reason);

public sealed record EntitlementGrant(string Tier, DateTimeOffset? StartsAt, DateTimeOffset EndsAt, string Reason);
public sealed record EntitlementChange(string? Tier, DateTimeOffset? EndsAt, string? Reason);
public sealed record EntitlementView(Guid Id, string Tier, string Source, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, string Reason,
    Guid? GrantedBy, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt, bool Valid);

/// <summary>One account as staff see it in the admin app (D136).</summary>
public sealed record AccountAdminSummary(Guid Id, string Realm, string Status, string? DisplayName, string? AgeStatus, string? IdStatus,
    bool CanRequestIdVerification, bool Bot, bool Employee, bool Founder, string? HomeMarket, DateTimeOffset? HomeMarketMovedAt, string Plan,
    IReadOnlyList<SanctionView> Sanctions, IReadOnlyList<EntitlementView> Entitlements, int OpenCases, bool DateOfBirthVerified = false,
    DateTimeOffset? HomeReviewFlaggedAt = null, int InconsistentHomeChecks = 0, string DobCheck = "none");

/// <summary>D166: a manual date-of-birth review in the admin queue. Never either date: the admin reads the document's in Didit's console.</summary>
public sealed record DobReviewView(Guid Id, Guid AccountId, string? DisplayName, string Reason, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, string? Outcome);
/// <summary>"verified" (the date on file matches the ID) or "notVerified", with a reason (audited, never shown publicly).</summary>
public sealed record DobReviewDecision(string? Outcome, string? Reason);
/// <summary>D150: an admin clears the home-location review flag after looking, with a reason (audited, never shown publicly).</summary>
public sealed record HomeReviewClear(string? Reason);
