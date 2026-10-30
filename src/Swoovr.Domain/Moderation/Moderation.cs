namespace Swoovr.Domain;

public enum ReportPriority { Normal, High, Urgent }

/// <summary>
/// A report category (D131): centrally managed data with a priority; threats and suspected underage are urgent.
/// Labels, order, availability and priority are configuration, not code.
/// </summary>
public sealed class ReportCategory
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public ReportPriority Priority { get; set; }
    public int SortOrder { get; set; }
    public bool Available { get; set; } = true;
    /// <summary>
    /// A Serious Safety Incident category (policy §9, owner Q77, D190): offered only in the separate serious-incident
    /// pathway, always urgent, always the admin queue, escalated and preserved under a provisional legal hold.
    /// </summary>
    public bool Serious { get; set; }
}

/// <summary>
/// What a report is about (D131). Stored by name, so types that arrive later (conversations, messages, photos,
/// albums) need no redesign; <see cref="ReportPolicy.Supported"/> says which can be reported today.
/// </summary>
public enum ReportSubject { Profile, Request, Conversation, Message, Photo, Album, Bot }

/// <summary>
/// One member's report. The subject account is the person (or bot) reported; SubjectRef is the reported object's id
/// (a profile's public id, a request id). Text is the reporter's optional note (up to 2,000 characters); it is case
/// evidence, never written to the audit log.
/// </summary>
public sealed class Report
{
    public Guid Id { get; set; }
    public Guid ReporterAccountId { get; set; }
    /// <summary>Reporter-owned optional meeting context, never shared with the reported person.</summary>
    public Guid? MeetingSafetySessionId { get; set; }
    public Guid SubjectAccountId { get; set; }
    public ReportSubject SubjectType { get; set; }
    public string SubjectRef { get; set; } = "";
    public string CategoryId { get; set; } = "";
    public ReportPriority Priority { get; set; }
    public string? Text { get; set; }
    public Guid CaseId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    /// <summary>Serious incidents (D190): whether the reporter has contacted or wants to contact police; never acted on by Swoovr.</summary>
    public string? Police { get; set; }
    /// <summary>The police reference the reporter gave, sealed with the evidence key (D190); never in clear, never in the audit log.</summary>
    public byte[]? PoliceReference { get; set; }
    /// <summary>#50: the member-facing reference (SW-XXXXXX) support looks up; given to older reports when first shown.</summary>
    public string? SupportReference { get; set; }
}

public enum CaseStatus { Open, UnderReview, Reviewed, Closed }
public enum CaseQueue { Support, Admin }

/// <summary>
/// A moderation case (D131, D133): every open report about the same account joins one case (duplicates consolidated).
/// Reports about staff-affiliated accounts go to the admin queue (D134). An urgent case is escalated at once and stays
/// open until someone acknowledges it (D135).
/// </summary>
public sealed class ModerationCase
{
    public Guid Id { get; set; }
    public Guid SubjectAccountId { get; set; }
    public CaseStatus Status { get; set; }
    public CaseQueue Queue { get; set; }
    public ReportPriority Priority { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EscalatedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedBy { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    /// <summary>D216: qualifying review content was disposed after its schedule; minimal compliance records remain separately.</summary>
    public DateTimeOffset? EvidencePurgedAt { get; set; }
    /// <summary>A case Swoovr opened itself, without a report: "returning-terminated" (D171). Null for reported cases.</summary>
    public string? SystemFlag { get; set; }
}

/// <summary>
/// What the reported thing looked like when it was reported (D131: evidence preserved), as a small JSON snapshot. Read
/// only by staff working the case; never in the audit log, never shown to the reporter.
/// </summary>
public sealed class CaseEvidence
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid ReportId { get; set; }
    public string Kind { get; set; } = "";
    public string Snapshot { get; set; } = "";
    /// <summary>Objects referenced by this independent review snapshot. Null denotes legacy provenance; disposal checks its snapshot conservatively.</summary>
    public Guid[]? MediaIds { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
}

public enum SanctionKind { Warning, Restriction, Suspension, Termination }

/// <summary>
/// A moderation action on an account (D133). Restrictions last up to 30 days and are targeted (out of discovery, no new
/// requests, no messages); suspensions up to 90 days (no normal member functions); termination is permanent; a warning
/// is a notice only. Every action has a reason, actor, time and audit entry; expired sanctions lift by themselves.
/// </summary>
public sealed class Sanction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid? CaseId { get; set; }
    public SanctionKind Kind { get; set; }
    public bool HideFromDiscovery { get; set; }
    public bool NoNewRequests { get; set; }
    public bool NoMessages { get; set; }
    /// <summary>A staff-written reason, shown in the admin app and in the member's notice; never in the audit log.</summary>
    public string Reason { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public Guid CreatedBy { get; set; }
    /// <summary>The role the actor used; support can't lift what an admin applied.</summary>
    public AccountRole CreatedByRole { get; set; }
    public DateTimeOffset? LiftedAt { get; set; }
    public Guid? LiftedBy { get; set; }
}

public enum AppealStatus { Pending, Upheld, Overturned }

/// <summary>One appeal per action within 30 days (D135), decided by someone other than the original decider.</summary>
public sealed class Appeal
{
    public Guid Id { get; set; }
    public Guid SanctionId { get; set; }
    public Guid AccountId { get; set; }
    public string? Text { get; set; }
    public AppealStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
}

public enum NoticeKind { Warning, Restriction, Suspension, Termination, AppealUpheld, AppealOverturned, SanctionLifted }

/// <summary>An in-app notice to a member about an action on their account (D135); optionally also emailed.</summary>
public sealed class MemberNotice
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public NoticeKind Kind { get; set; }
    public Guid? SanctionId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}

/// <summary>
/// The admin-set link from a staff-realm account to the one member account its owner uses personally (D134, D146), so no
/// admin decides a case about their own member account.
/// </summary>
public sealed class StaffMemberLink
{
    public Guid StaffAccountId { get; set; }
    public Guid MemberAccountId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public Guid LinkedBy { get; set; }
}

public enum ModerationAction { Dismiss, Warn, Restrict, Suspend, Terminate, Lift, RevokeStaffAffiliation, Acknowledge, DecideAppeal }

public enum ModerationRefusal { None, NotPermitted, StaffAffiliatedCase, OwnAccount, AdminSanction, TooLong, ReasonRequired }

/// <summary>
/// Who may do what in moderation (D133–D135). Support reviews, dismisses, warns and applies or lifts temporary
/// restrictions; only admins suspend, terminate, revoke staff affiliation and work staff-affiliated cases; nobody
/// decides about their own member account; support can't lift what an admin applied. Facts come from application
/// records only.
/// </summary>
public static class ModerationPolicy
{
    public static readonly TimeSpan MaxRestriction = TimeSpan.FromDays(30);
    public static readonly TimeSpan MaxSuspension = TimeSpan.FromDays(90);
    public const int ReasonMax = 1000;

    public static ModerationRefusal Check(ModerationAction action, AccountRole actor, bool staffAffiliatedSubject, bool ownAccount,
        TimeSpan? duration = null, string? reason = null, Sanction? lifting = null)
    {
        if (actor is not (AccountRole.Admin or AccountRole.Support)) return ModerationRefusal.NotPermitted;
        if (ownAccount) return ModerationRefusal.OwnAccount;
        if (staffAffiliatedSubject && actor != AccountRole.Admin) return ModerationRefusal.StaffAffiliatedCase;
        var adminOnly = action is ModerationAction.Suspend or ModerationAction.Terminate or ModerationAction.RevokeStaffAffiliation;
        if (adminOnly && actor != AccountRole.Admin) return ModerationRefusal.NotPermitted;
        if (action == ModerationAction.Lift && lifting is not null)
        {
            if (lifting.CreatedByRole == AccountRole.Admin && actor != AccountRole.Admin) return ModerationRefusal.AdminSanction;
            if (lifting.Kind is SanctionKind.Suspension or SanctionKind.Termination && actor != AccountRole.Admin) return ModerationRefusal.NotPermitted;
        }
        if (action is ModerationAction.Warn or ModerationAction.Restrict or ModerationAction.Suspend or ModerationAction.Terminate or ModerationAction.Lift
            && string.IsNullOrWhiteSpace(reason)) return ModerationRefusal.ReasonRequired;
        if (reason is { Length: > ReasonMax }) return ModerationRefusal.ReasonRequired;
        if (action == ModerationAction.Restrict && (duration is null || duration <= TimeSpan.Zero || duration > MaxRestriction)) return ModerationRefusal.TooLong;
        if (action == ModerationAction.Suspend && (duration is null || duration <= TimeSpan.Zero || duration > MaxSuspension)) return ModerationRefusal.TooLong;
        return ModerationRefusal.None;
    }

    /// <summary>
    /// Appeals (D135): one per action (warnings included), within 30 days of it, by the sanctioned member; decided by
    /// someone other than the original decider, and only by admins for staff-affiliated accounts.
    /// </summary>
    public static readonly TimeSpan AppealWindow = TimeSpan.FromDays(30);

    public static bool MayAppeal(Sanction sanction, Guid member, bool alreadyAppealed, DateTimeOffset now) =>
        sanction.AccountId == member && !alreadyAppealed && sanction.LiftedAt is null && now - sanction.StartsAt <= AppealWindow;

    public static ModerationRefusal MayDecideAppeal(Sanction sanction, Guid decider, AccountRole role, bool staffAffiliatedSubject, bool ownAccount)
    {
        if (role is not (AccountRole.Admin or AccountRole.Support)) return ModerationRefusal.NotPermitted;
        if (ownAccount) return ModerationRefusal.OwnAccount;
        if (sanction.CreatedBy == decider) return ModerationRefusal.NotPermitted; // someone other than the original decider
        if (staffAffiliatedSubject && role != AccountRole.Admin) return ModerationRefusal.StaffAffiliatedCase;
        if (sanction.CreatedByRole == AccountRole.Admin && role != AccountRole.Admin) return ModerationRefusal.AdminSanction;
        return ModerationRefusal.None;
    }
}

/// <summary>What the account's live sanctions mean right now (D133); expired or lifted ones count for nothing.</summary>
/// <remarks>
/// Owner answer to Q55 (D160): a restricted member can't do anything in the app either; restrictions are a safety
/// concern. Restricted, suspended and terminated accounts are all barred (only their account, notices and appeals stay
/// open, and the app shows the restricted screen); the targeted options on a restriction are kept as a record only.
/// </remarks>
public sealed record Enforcement(bool Suspended, bool Terminated, bool HiddenFromDiscovery, bool NoNewRequests, bool NoMessages, bool Restricted = false)
{
    public static readonly Enforcement None = new(false, false, false, false, false);
    /// <summary>Restricted, suspended or terminated: no normal member functions.</summary>
    public bool Barred => Suspended || Terminated || Restricted;
    /// <summary>The account state members and the web host see: active, restricted, suspended or terminated.</summary>
    public string State => Terminated ? "terminated" : Suspended ? "suspended" : Restricted ? "restricted" : "active";

    public static bool Live(Sanction s, DateTimeOffset now) => s.LiftedAt is null && s.StartsAt <= now && (s.EndsAt is null || s.EndsAt > now);

    public static Enforcement For(IEnumerable<Sanction> sanctions, DateTimeOffset now)
    {
        var live = sanctions.Where(s => Live(s, now)).ToList();
        var terminated = live.Any(s => s.Kind == SanctionKind.Termination);
        var suspended = live.Any(s => s.Kind == SanctionKind.Suspension);
        var restricted = live.Any(s => s.Kind == SanctionKind.Restriction);
        var barred = terminated || suspended || restricted;
        return new(suspended, terminated, barred, barred, barred, restricted);
    }
}

/// <summary>Reporting rules (D131).</summary>
public static class ReportPolicy
{
    public const int TextMax = 2000;
    /// <summary>DEFAULT abuse limit (no fixed daily cap, D131): non-urgent reports per reporter per 10 minutes; urgent always accepted.</summary>
    public const int BurstLimit = 10;
    public static readonly TimeSpan BurstWindow = TimeSpan.FromMinutes(10);

    /// <summary>Subjects that exist in the product today; the rest arrive with messaging (U) and photos (D132).</summary>
    public static bool Supported(ReportSubject subject) => subject is ReportSubject.Profile or ReportSubject.Request or ReportSubject.Bot or ReportSubject.Conversation or ReportSubject.Message;

    /// <summary>
    /// What the reporter sees (D131): Submitted, Under review, Reviewed or Closed. Since D190 a closed report also shows a
    /// general outcome (<see cref="Outcome"/>), never the specific action.
    /// </summary>
    public static string ReporterStatus(CaseStatus status) => status switch
    {
        CaseStatus.Open => "submitted",
        CaseStatus.UnderReview => "underReview",
        CaseStatus.Reviewed => "reviewed",
        _ => "closed",
    };

    /// <summary>
    /// D190 (policy §9, supersedes D131's "never the outcome"): once the case is closed, "actionTaken" when any action was
    /// taken on the case after the report, otherwise "noBreachFound"; nothing while it is open. Never which action.
    /// </summary>
    public static string? Outcome(CaseStatus status, bool actionAfterReport) =>
        status != CaseStatus.Closed ? null : actionAfterReport ? "actionTaken" : "noBreachFound";

    /// <summary>D190: what the reporter says about police; null when they didn't say.</summary>
    public static readonly IReadOnlyList<string> PoliceAnswers = ["contacted", "will-contact", "not-now"];
    public const int PoliceReferenceMax = 100;
    /// <summary>D190: messages a reporter may pick from the conversation as evidence.</summary>
    public const int ChosenMessagesMax = 20;

    /// <summary>The queue for a case (D134, D190): staff-affiliated subjects and serious incidents go to admins.</summary>
    public static CaseQueue QueueFor(bool staffAffiliatedSubject, bool serious) => staffAffiliatedSubject || serious ? CaseQueue.Admin : CaseQueue.Support;

    public static bool RateLimited(ReportPriority priority, int recentNonUrgent) => priority != ReportPriority.Urgent && recentNonUrgent >= BurstLimit;

    /// <summary>The queue for a case (D134): staff-affiliated subjects go to admins.</summary>
    public static CaseQueue QueueFor(bool staffAffiliatedSubject) => staffAffiliatedSubject ? CaseQueue.Admin : CaseQueue.Support;

    public static ReportPriority Max(ReportPriority a, ReportPriority b) => a > b ? a : b;
}

public enum PlanTier { Free, Plus, Premium }
public enum EntitlementSource { AdminGrant, Promotion, Founding, Paid }

/// <summary>
/// A tier a member holds for a period (D151): admin grants for the beta, later promotions and paid plans. Complimentary
/// grants are never charged; a lapsed or revoked one falls back to Free. Founding membership stays separate (D63) and is
/// read from the profile.
/// </summary>
public sealed class Entitlement
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public PlanTier Tier { get; set; }
    public EntitlementSource Source { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? EndsAt { get; set; }
    public string Reason { get; set; } = "";
    public Guid? GrantedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
}

/// <summary>
/// The one place that decides a member's effective tier (D137, D151): the highest valid entitlement among grants,
/// promotions, founding membership and paid plans. Features ask this, never Stripe. DEFAULT: founding membership gives
/// Plus (D63: founders keep Plus free).
/// </summary>
public static class EntitlementPolicy
{
    public static bool Valid(Entitlement e, DateTimeOffset now) => e.RevokedAt is null && e.StartsAt <= now && (e.EndsAt is null || e.EndsAt > now);

    public static PlanTier Effective(IEnumerable<Entitlement> entitlements, bool founder, DateTimeOffset? founderFrom, DateTimeOffset now)
    {
        var tier = entitlements.Where(e => Valid(e, now)).Select(e => e.Tier).DefaultIfEmpty(PlanTier.Free).Max();
        if (founder && (founderFrom is null || founderFrom <= now) && tier < PlanTier.Plus) tier = PlanTier.Plus;
        return tier;
    }

    public const int ReasonMax = 500;
    /// <summary>DEFAULT: an admin grant runs at most a year at a time (extend to continue).</summary>
    public static readonly TimeSpan MaxGrant = TimeSpan.FromDays(366);

    public static bool ValidGrant(PlanTier tier, DateTimeOffset starts, DateTimeOffset? ends, string? reason) =>
        tier != PlanTier.Free && ends is { } end && end > starts && end - starts <= MaxGrant
        && !string.IsNullOrWhiteSpace(reason) && reason.Length <= ReasonMax;
}
