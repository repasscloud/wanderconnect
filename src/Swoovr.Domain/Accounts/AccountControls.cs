namespace Swoovr.Domain;

/// <summary>
/// A sign-in lock on a member account (#190 §10, D323): while active, the API refuses every protected member operation
/// with the controlled "account-locked" state and its public case reference, whatever session or token the member holds.
/// Only the account summary, notices and support paths stay open. Staff place and remove locks with set reasons; the
/// lock is never deleted, so history survives.
/// </summary>
public sealed class AccountLock
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>A set reason code from the lock reason list (security incident, compromise, legal, investigation…).</summary>
    public string ReasonCode { get; set; } = "";
    /// <summary>The public reference shown to the member (SW-XXXXXX or an existing case reference). Designed for later Zammad linkage.</summary>
    public string CaseReference { get; set; } = "";
    /// <summary>Optional internal note: never shown to the member, never in the audit log.</summary>
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid CreatedBy { get; set; }
    public AccountRole CreatedByRole { get; set; }
    /// <summary>Null = until removed.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedBy { get; set; }
    public string? RemovedReason { get; set; }
}

public static class AccountLockPolicy
{
    public const int NoteMax = 1000, ReferenceMax = 32;
    public static bool Active(AccountLock l, DateTimeOffset now) => l.RemovedAt is null && (l.ExpiresAt is null || l.ExpiresAt > now);
    /// <summary>Legal restrictions are placed and removed by Legal or Admin only; any other lock by Trust & Safety, Admin or Legal (D322).</summary>
    public static bool MayLock(AccountRole role, string reasonCode) => reasonCode == "legal-restriction"
        ? role is AccountRole.Legal or AccountRole.Admin
        : role is AccountRole.TrustSafety or AccountRole.Admin or AccountRole.Legal;
    /// <summary>Nobody removes a lock placed by a higher role; Legal locks only Legal or Admin can remove.</summary>
    public static bool MayUnlock(AccountRole role, AccountLock l) => MayLock(role, l.ReasonCode)
        && (l.CreatedByRole != AccountRole.Admin || role == AccountRole.Admin)
        && (l.CreatedByRole != AccountRole.Legal || role is AccountRole.Legal or AccountRole.Admin);
}

public enum VerificationBadge { PhotoVerified, IdVerified }

/// <summary>
/// One staff override of Photo Verified or ID Verified (#190 §3, D323): append-only history with the set reason. Applying
/// never creates a provider result; revoking keeps every verification attempt and identity record.
/// </summary>
public sealed class VerificationOverride
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public VerificationBadge Badge { get; set; }
    public bool Applied { get; set; }
    public string ReasonCode { get; set; } = "";
    public string? Note { get; set; }
    public DateTimeOffset At { get; set; }
    public Guid By { get; set; }
    public AccountRole ByRole { get; set; }
}
