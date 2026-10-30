namespace Swoovr.Contracts;

/// <summary>A report category (D131) as members see it: id, label and nothing about priority handling.</summary>
public sealed record ReportCategoryView(string Id, string Label);

/// <summary>
/// A member's report (D131). SubjectType is "profile", "request" or "bot" today ("conversation", "message", "photo" and
/// "album" arrive with those features); SubjectId is the profile's public id or the request id. Text is optional, up to
/// 2,000 characters. Block also blocks the person in the same step.
/// </summary>
/// Serious incidents (D190) add Police ("contacted", "will-contact" or "not-now"), an optional PoliceReference (up to 100
/// characters, kept encrypted) and MessageIds: up to 20 messages from the reported conversation chosen as evidence.
public sealed record NewReport(string SubjectType, string SubjectId, string Category, string? Text, bool Block, string? Police = null,
    string? PoliceReference = null, List<Guid>? MessageIds = null, Guid? MeetingSafetySessionId = null);

/// <summary>
/// What the reporter sees (D131): submitted, underReview, reviewed or closed. Outcome (D190) appears once closed:
/// "actionTaken" or "noBreachFound", never which action. Serious marks a Serious Safety Incident report.
/// </summary>
public sealed record MyReportView(Guid Id, string SubjectType, string Category, string Status, DateTimeOffset CreatedAt, string? Outcome = null, bool Serious = false);

/// <summary>
/// Someone the member has dealt with (D190: reporting stays available after a block, disconnect or deletion): a
/// conversation or request to report through, the name they knew (or "Deleted member"), and the state now:
/// connected, pending, blocked, disconnected, ended, declined or deleted.
/// </summary>
public sealed record SafetyPerson(string SubjectType, Guid SubjectId, string Name, string State, DateTimeOffset Since);

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

/// <param name="SystemFlag">Set on a case Swoovr opened itself: "returning-terminated" (D171).</param>
public sealed record CaseSummaryView(Guid Id, Guid SubjectAccountId, string? SubjectName, string Status, string Queue, string Priority,
    int Reports, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool AwaitingAcknowledgement, string? SystemFlag = null);
/// <summary>D187: an account that used a contact value, and when (never the value itself).</summary>
public sealed record ContactHistoryHit(Guid AccountId, DateTimeOffset? ValidFrom, DateTimeOffset ValidUntil, string Reason);

/// <summary>D187: the account holding the value now (if any) and the accounts that held it before.</summary>
public sealed record ContactHistoryResult(Guid? CurrentAccountId, IReadOnlyList<ContactHistoryHit> Previous);

public sealed record CaseReportView(Guid Id, Guid ReporterAccountId, string SubjectType, string SubjectRef, string Category, string Priority,
    string? Text, DateTimeOffset CreatedAt, string? Police = null);
public sealed record CaseEvidenceView(Guid ReportId, string Kind, string Snapshot, DateTimeOffset CapturedAt);
public sealed record SanctionView(Guid Id, Guid AccountId, Guid? CaseId, string Kind, string Reason, DateTimeOffset StartsAt, DateTimeOffset? EndsAt,
    bool HideFromDiscovery, bool NoNewRequests, bool NoMessages, Guid CreatedBy, string CreatedByRole, DateTimeOffset? LiftedAt, bool Live);
public sealed record CaseDetailView(CaseSummaryView Case, bool StaffAffiliatedSubject, IReadOnlyList<CaseReportView> Reports,
    IReadOnlyList<CaseEvidenceView> Evidence, IReadOnlyList<SanctionView> Sanctions, DateTimeOffset? EvidencePurgedAt = null);

/// <summary>
/// A staff action (D133): "dismiss", "warn", "restrict", "suspend" or "terminate", with a reason (required except to
/// dismiss), days for a restriction (up to 30) or suspension (up to 90), and what a restriction covers.
/// </summary>
public sealed record ModerationActionRequest(string Action, string? Reason, int? Days, bool HideFromDiscovery, bool NoNewRequests, bool NoMessages, ModerationReportingChoice? Reporting=null);
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
    DateTimeOffset? HomeReviewFlaggedAt = null, int InconsistentHomeChecks = 0, string DobCheck = "none",
    DateTimeOffset? IdentityOnFileSince = null, string? IdentityIssuingCountry = null);

/// <summary>D191: opening a verified identity record needs the `legal` role and a recorded purpose (up to 500 characters).</summary>
public sealed record IdentityAccessRequest(string? Purpose);

/// <summary>D191: the record as kept: legal names, the document's date of birth, type, issuing country (ISO alpha-3) and expiry.</summary>
public sealed record VerifiedIdentityView(Guid AccountId, string? FirstName, string? LastName, DateOnly? DateOfBirth, string? DocumentType, string? IssuingCountry,
    DateOnly? ExpiryDate, string? Method, DateTimeOffset VerifiedAt, string SessionId, DateTimeOffset? RetainUntil);

/// <summary>D166: a manual date-of-birth review in the admin queue. Never either date: the admin reads the document's in Didit's console.</summary>
public sealed record DobReviewView(Guid Id, Guid AccountId, string? DisplayName, string Reason, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, string? Outcome);
/// <summary>"verified" (the date on file matches the ID) or "notVerified", with a reason (audited, never shown publicly).</summary>
public sealed record DobReviewDecision(string? Outcome, string? Reason);
/// <summary>D150: an admin clears the home-location review flag after looking, with a reason (audited, never shown publicly).</summary>
public sealed record HomeReviewClear(string? Reason);
