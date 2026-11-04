namespace Swoovr.Contracts;

/// <summary>
/// Set reasons for staff account actions (#190, D323): stable wire codes with their admin labels. The API accepts only
/// these codes; the label is what staff pick from. Free text is an optional internal note, never the primary reason.
/// </summary>
public static class AccountReasons
{
    public sealed record Reason(string Code, string Label);

    public static readonly IReadOnlyList<Reason> Warn =
    [
        new("explicit-content", "Unsolicited explicit content"), new("harassment", "Harassment or abuse"), new("spam-scam", "Spam or scam behaviour"),
        new("misleading-profile", "Misleading profile"), new("hate-speech", "Hateful conduct"), new("other-breach", "Other Community Guidelines breach"),
    ];
    public static readonly IReadOnlyList<Reason> Restrict =
    [
        new("repeated-reports", "Repeated reports"), new("spam-scam", "Spam or scam behaviour"), new("harassment", "Harassment or abuse"),
        new("pending-investigation", "Pending investigation"), new("explicit-content", "Unsolicited explicit content"),
    ];
    public static readonly IReadOnlyList<Reason> Suspend =
    [
        new("serious-breach", "Serious policy breach"), new("pending-investigation", "Pending investigation"), new("suspected-compromise", "Suspected account compromise"),
        new("safety-risk", "Risk to member safety"),
    ];
    public static readonly IReadOnlyList<Reason> Terminate =
    [
        new("severe-breach", "Severe or repeated breach"), new("underage", "Underage account"), new("fraud-impersonation", "Fraud or impersonation"),
        new("legal-requirement", "Legal requirement"),
    ];
    public static readonly IReadOnlyList<Reason> Lift =
    [
        new("appeal-upheld", "Appeal upheld"), new("applied-in-error", "Applied in error"), new("investigation-closed", "Investigation closed"), new("review-complete", "Review complete"),
    ];
    public static readonly IReadOnlyList<Reason> Lock =
    [
        new("suspected-compromise", "Suspected account compromise"), new("security-incident", "Security incident"),
        new("trust-safety-investigation", "Trust & Safety investigation"), new("legal-restriction", "Legal restriction"),
        new("administrative", "Administrative restriction"),
    ];
    public static readonly IReadOnlyList<Reason> Unlock =
    [
        new("identity-confirmed", "Member identity confirmed"), new("investigation-closed", "Investigation closed"), new("applied-in-error", "Applied in error"),
        new("legal-cleared", "Legal restriction cleared"),
    ];
    public static readonly IReadOnlyList<Reason> VerifyApply =
    [
        new("staff-document-check", "Identity confirmed by staff from an approved document"), new("provider-outage", "Provider result confirmed during an outage"),
        new("staff-selfie-check", "Selfie confirmed by staff"),
    ];
    public static readonly IReadOnlyList<Reason> VerifyRevoke =
    [
        new("evidence-invalid", "Evidence no longer valid"), new("applied-in-error", "Applied in error"), new("fraud-impersonation", "Fraud or impersonation"),
        new("member-request", "Member request"),
    ];
    public static readonly IReadOnlyList<Reason> GrantComp =
    [
        new("beta-tester", "Beta tester"), new("founder-programme", "Founder programme"), new("service-recovery", "Service recovery"),
        new("partner-press", "Partner or press"), new("staff-contractor", "Staff or contractor"),
    ];
    public static readonly IReadOnlyList<Reason> RevokeComp =
    [
        new("granted-in-error", "Granted in error"), new("programme-ended", "Programme ended"), new("policy-breach", "Policy breach"), new("member-request", "Member request"),
    ];
    public static readonly IReadOnlyList<Reason> StaffOn = [new("joined-staff", "Joined RePass Cloud staff"), new("contractor", "Contractor engagement")];
    public static readonly IReadOnlyList<Reason> StaffOff = [new("left-company", "Left the company"), new("applied-in-error", "Applied in error"), new("role-change", "Role change")];

    public static IReadOnlyList<Reason> ForAction(string action) => action switch
    {
        "warn" => Warn, "restrict" => Restrict, "suspend" => Suspend, "terminate" => Terminate, _ => [],
    };
    public static bool Valid(IReadOnlyList<Reason> list, string? code) => code is not null && list.Any(r => r.Code == code);
    public static string Label(string? code) =>
        new[] { Warn, Restrict, Suspend, Terminate, Lift, Lock, Unlock, VerifyApply, VerifyRevoke, GrantComp, RevokeComp, StaffOn, StaffOff }
            .SelectMany(l => l).FirstOrDefault(r => r.Code == code)?.Label ?? code ?? "";
}

/// <summary>
/// What the signed-in staff member may do on this account (#190, D322). The admin app only shows or disables controls with
/// it; every action is still authorised by its own endpoint.
/// </summary>
public sealed record AccountPermissionsView(string Role, bool Warn, bool Restrict, int MaxRestrictionDays, bool Suspend, bool Terminate,
    bool Verify, bool Membership, bool StaffBadge, bool Lock, bool LegalLock, bool InspectMedia, bool Conversations, bool EditLocation);

public sealed record AccountLockRequest(string ReasonCode, string? CaseReference, int? Days, string? Note);
public sealed record AccountUnlockRequest(string ReasonCode, string? Note);
public sealed record AccountLockView(Guid Id, string ReasonCode, string CaseReference, DateTimeOffset CreatedAt, string CreatedByRole,
    DateTimeOffset? ExpiresAt, DateTimeOffset? RemovedAt, string? RemovedReason, bool Active);

public sealed record VerificationOverrideRequest(string Badge, bool Apply, string ReasonCode, string? Note);
public sealed record VerificationOverrideView(Guid Id, string Badge, bool Applied, string ReasonCode, DateTimeOffset At, string ByRole);
public sealed record VerificationStateView(string PhotoStatus, bool PhotoManual, DateTimeOffset? PhotoCheckedAt, string IdStatus, bool IdManual,
    DateTimeOffset? IdCheckedAt, IReadOnlyList<VerificationOverrideView> History);

/// <summary>Contact and sign-in facts for staff (#190 §9): email and mobile are masked by the API, never sent whole.</summary>
public sealed record AccountContactView(string? EmailMasked, string? MobileMasked, DateTimeOffset? MobileVerifiedAt, DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);

public sealed record StaffBadgeRequest(bool On, string ReasonCode);
public sealed record EntitlementRevoke(string ReasonCode);
