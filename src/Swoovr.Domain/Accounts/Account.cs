namespace Swoovr.Domain;

/// <summary>PendingDeletion and Deleted (D144, D170): out of discovery and every member route but the deletion screen.</summary>
public enum AccountStatus { Active, Locked, Suspended, PendingDeletion, Deleted }

/// <summary>
/// An application account keyed by the validated token's (issuer, subject) (D55). Keycloak
/// answers who someone is; this record and those linked to it answer what they may do.
/// Email is the last accepted verified address from validated token evidence, kept for
/// beta enrolment, invitations and founder tokens; it is never used to merge accounts (D38).
/// </summary>
public sealed class Account
{
    public Guid Id { get; set; }
    public string Issuer { get; set; } = "";
    public string Subject { get; set; } = "";
    public AccountRealm Realm { get; set; }
    /// <summary>Normalised, verified only; null until a token carries a verified address.</summary>
    public string? Email { get; set; }
    /// <summary>Current verified email's acceptance watermark (including tolerated issuer skew/legacy uncertainty); older claims cannot reverse it.</summary>
    public DateTimeOffset? EmailAcceptedAt { get; set; }
    /// <summary>D249: reviewed binding retired; suppress claim adoption until independent original-account recovery.</summary>
    public DateTimeOffset? EmailRetiredAt { get; set; }
    public AccountStatus Status { get; set; }
    /// <summary>A short machine reason such as "underage-dob"; never free text from a client.</summary>
    public string? StatusReason { get; set; }
    /// <summary>The single staff-realm break-glass account (D36). Granted only from the CLI.</summary>
    public bool IsRootAdmin { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    /// <summary>Deletion (D170): when it was asked for, when final deletion runs, and the status a cancel restores.</summary>
    public DateTimeOffset? DeletionRequestedAt { get; set; }
    public DateTimeOffset? DeletionDueAt { get; set; }
    /// <summary>Final purge has crossed its irreversible boundary; provider uncertainty retries and cannot resurrect the account.</summary>
    public DateTimeOffset? DeletionFinalizingAt { get; set; }
    public AccountStatus? DeletionRestoreStatus { get; set; }
    /// <summary>The member also asked for their founder recovery token to go at final deletion (D173, #22).</summary>
    public bool DeleteFounderToken { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    /// <summary>#88 (policy §11): when a staff member's access was revoked; sessions authenticated before it are refused.</summary>
    public DateTimeOffset? SessionsRevokedAt { get; set; }
}

/// <summary>An application role (Admin, Support, VenuePartner). Members hold Member implicitly.</summary>
public sealed class RoleGrant
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public AccountRole Role { get; set; }
    /// <summary>Required for VenuePartner, null otherwise. Venue records arrive with the venues slice.</summary>
    public Guid? VenueId { get; set; }
    public DateTimeOffset GrantedAt { get; set; }
    /// <summary>The granting account, or null for an invitation acceptance or the CLI.</summary>
    public Guid? GrantedBy { get; set; }
}

/// <summary>
/// Append-only record of a privileged or security-relevant action. Detail carries ids and
/// machine values only: no email address, token, date of birth or message text.
/// </summary>
public sealed class AuditEntry
{
    public long Id { get; set; }
    public DateTimeOffset At { get; set; }
    /// <summary>The acting account; null for the CLI or background logic (see Actor).</summary>
    public Guid? ActorAccountId { get; set; }
    /// <summary>"account", "cli" or "system".</summary>
    public string Actor { get; set; } = "";
    public string Action { get; set; } = "";
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? Detail { get; set; }
}

public static class EmailAddress
{
    /// <summary>
    /// DEFAULT normalisation: trim and lower-case the whole address (invariant culture). No
    /// provider-specific rewriting (dots, plus tags), so two spellings stay two addresses.
    /// Returns null for anything that is not a single plausible address.
    /// </summary>
    public static string? Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var email = value.Trim().ToLowerInvariant();
        var at = email.IndexOf('@');
        if (email.Length > 254 || at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1
            || email.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)) || !email[(at + 1)..].Contains('.'))
            return null;
        return email;
    }
}
