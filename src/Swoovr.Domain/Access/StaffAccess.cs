using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>
/// Time-limited elevated access (internal policy §11, §15; procedure §5; #88): a staff member asks for one capability with
/// a recorded purpose and gets it for 60 minutes, renewable; it ends on its own. Opening a verified identity, exporting
/// evidence and terminating an account need one. Grants belong to the account, never to a session.
/// </summary>
public sealed class AccessGrant
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>identity-open, evidence-export or termination.</summary>
    public string Capability { get; set; } = "";
    public string Purpose { get; set; } = "";
    public DateTimeOffset GrantedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public int Renewals { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    /// <summary>expired-not-used is never stored; "revoked", "staff-revoked" or "superseded".</summary>
    public string? RevokedReason { get; set; }
}

/// <summary>
/// One entry in the append-only sensitive-access log (§11, §15, procedure §14): every view, query, export, change and
/// refused attempt on restricted staff routes, hash-chained like the custody log and protected by a database trigger
/// against update and delete. Ids and route kinds only: never query values, tokens or content.
/// </summary>
public sealed class SensitiveAccess
{
    public long Sequence { get; set; }
    public DateTimeOffset At { get; set; }
    public Guid? ActorAccountId { get; set; }
    /// <summary>view, query, export, change or refused.</summary>
    public string Action { get; set; } = "";
    /// <summary>The kind of thing: holds, evidence-export, disclosures, le-case, identity, contact-history, access, staff.</summary>
    public string Resource { get; set; } = "";
    public string? ResourceId { get; set; }
    public int Status { get; set; }
    public byte[] PreviousHash { get; set; } = [];
    public byte[] Hash { get; set; } = [];
}

/// <summary>A declared conflict of interest (procedure §5): the staff member is kept out of anything about that member.</summary>
public sealed class StaffExclusion
{
    public Guid StaffAccountId { get; set; }
    public Guid SubjectAccountId { get; set; }
    public string Reason { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A bulk evidence export waiting for, or holding, a second officer's approval (§11 "elevated authorisation").</summary>
public sealed class ExportApproval
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public Guid RequestedBy { get; set; }
    public string Purpose { get; set; } = "";
    public DateTimeOffset RequestedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTimeOffset? ApprovedAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

public enum AccessRefusal { None, NotPermitted, PurposeRequired, Unknown, Expired, RenewalLimit, NotOwner, NotSeparated, Used }

public static class StaffAccessPolicy
{
    public const string IdentityOpen = "identity-open";
    public const string EvidenceExport = "evidence-export";
    public const string Termination = "termination";
    public const string IdentifierRelease = "identifier-release";
    public const string AccountReinstatement = "account-reinstatement";
    public static readonly IReadOnlyList<string> Capabilities = [IdentityOpen, EvidenceExport, Termination, IdentifierRelease, AccountReinstatement];
    /// <summary>DEFAULT (owner, 27 October 2026): 60 minutes, renewable.</summary>
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(60);
    /// <summary>At most eight renewals: a working day, then ask again.</summary>
    public const int MaxRenewals = 8;
    public const int PurposeMax = 500;
    /// <summary>A bulk export: more than five accounts or a thousand items in the hold.</summary>
    public const int BulkAccounts = 5;
    public const int BulkItems = 1000;
    public static readonly TimeSpan ApprovalLifetime = TimeSpan.FromHours(24);

    /// <summary>Which role may ask for a capability: `legal` opens identities and exports evidence; admins terminate.</summary>
    public static AccountRole RoleFor(string capability) => capability switch
    {
        IdentityOpen or EvidenceExport => AccountRole.Legal,
        Termination or IdentifierRelease or AccountReinstatement => AccountRole.Admin,
        _ => AccountRole.Unknown,
    };

    public static AccessRefusal CheckRequest(string? capability, string? purpose, Func<AccountRole, bool> allows) =>
        capability is null || !Capabilities.Contains(capability) ? AccessRefusal.Unknown
        : !allows(RoleFor(capability)) ? AccessRefusal.NotPermitted
        : string.IsNullOrWhiteSpace(purpose) || purpose.Length > PurposeMax ? AccessRefusal.PurposeRequired : AccessRefusal.None;

    public static bool Active(AccessGrant grant, DateTimeOffset now) => grant.RevokedAt is null && grant.ExpiresAt > now;

    public static AccessGrant Grant(Guid account, string capability, string purpose, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), AccountId = account, Capability = capability, Purpose = purpose.Trim(), GrantedAt = now, ExpiresAt = now + Duration,
    };

    /// <summary>Renewing extends an active grant by another 60 minutes from now, up to eight times.</summary>
    public static AccessRefusal CheckRenew(AccessGrant grant, Guid caller, DateTimeOffset now) =>
        grant.AccountId != caller ? AccessRefusal.NotOwner
        : !Active(grant, now) ? AccessRefusal.Expired
        : grant.Renewals >= MaxRenewals ? AccessRefusal.RenewalLimit : AccessRefusal.None;

    public static bool Bulk(int accounts, int items) => accounts > BulkAccounts || items > BulkItems;

    /// <summary>A second officer approves; never the requester, once, within 24 hours.</summary>
    public static AccessRefusal CheckApprove(ExportApproval approval, Guid approver, DateTimeOffset now) =>
        approval.RequestedBy == approver ? AccessRefusal.NotSeparated
        : approval.ApprovedAt is not null || now - approval.RequestedAt > ApprovalLifetime ? AccessRefusal.Used : AccessRefusal.None;

    public static bool ApprovalUsable(ExportApproval? approval, Guid requester, Guid hold, DateTimeOffset now) =>
        approval is { ApprovedAt: { } approved, UsedAt: null } && approval.RequestedBy == requester && approval.HoldId == hold && now - approved <= ApprovalLifetime;

    /// <summary>
    /// Conflict of interest (procedure §5): a declared exclusion, or the staff member's own linked member account (D134),
    /// keeps them away from any of these subjects.
    /// </summary>
    public static bool Excluded(Guid staff, IEnumerable<Guid> subjects, IEnumerable<StaffExclusion> exclusions, IEnumerable<StaffMemberLink> links)
    {
        var set = subjects.ToHashSet();
        return set.Contains(staff) || exclusions.Any(e => e.StaffAccountId == staff && set.Contains(e.SubjectAccountId))
            || links.Any(l => l.StaffAccountId == staff && set.Contains(l.MemberAccountId));
    }

    /// <summary>A revoked staff member's sessions end: a token authenticated at or before the revocation is refused.</summary>
    public static bool SessionRevoked(DateTimeOffset? revokedAt, DateTimeOffset authenticatedAt) => revokedAt is { } r && authenticatedAt <= r;

    public static byte[] Chain(byte[] previous, DateTimeOffset at, Guid? actor, string action, string resource, string? resourceId, int status) =>
        SHA256.HashData([.. previous, .. Encoding.UTF8.GetBytes($"{at.ToUnixTimeMilliseconds()}|{actor}|{action}|{resource}|{resourceId}|{status}")]);

    public static bool ChainIntact(IReadOnlyList<SensitiveAccess> entries, byte[]? before = null)
    {
        var previous = before ?? entries.FirstOrDefault()?.PreviousHash ?? [];
        foreach (var e in entries)
        {
            if (!e.PreviousHash.SequenceEqual(previous) || !e.Hash.SequenceEqual(Chain(previous, e.At, e.ActorAccountId, e.Action, e.Resource, e.ResourceId, e.Status))) return false;
            previous = e.Hash;
        }
        return true;
    }

    /// <summary>
    /// Classifies a restricted admin route for the log (never its query string): the resource kind, an id from the path
    /// when there is one (never an export token), and whether it reads one record, searches, exports or changes.
    /// </summary>
    public static (string Resource, string? Id, string Action)? Classify(string method, string path, int status)
    {
        path = path.ToLowerInvariant();
        var parts = path.TrimEnd('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3 || parts[0] != "api" || parts[1] != "admin") return null;
        string? Id(int i) => parts.Length > i && Guid.TryParse(parts[i], out var g) ? g.ToString() : null;
        (string, string?, bool)? kind = parts[2] switch
        {
            "holds" => ("holds", Id(3), parts.Length > 4 && parts[4] == "exports"),
            "evidence-exports" => ("evidence-export", null, true),
            "disclosures" => ("disclosures", null, false),
            "law-enforcement" => ("le-case", Id(4), parts.Length > 3 && parts[3] == "audit-export"),
            "banned-identifiers" => ("banned-identifiers", Id(3), false),
            "cases" => ("moderation-case", Id(3), false),
            "appeals" => ("moderation-appeal", Id(3), false),
            "accounts" when parts.Length > 4 && parts[4] is "reinstate" or "reinstatement" => ("account-reinstatement", Id(3), false),
            "contact-history" => ("contact-history", null, false),
            "access" => ("access", Id(4), false),
            "staff" => ("staff", Id(3), false),
            "accounts" when parts.Length > 4 && parts[4] == "identity" => ("identity", Id(3), false),
            "media" => ("media", Id(3), false),
            _ => null,
        };
        if (kind is not var (resource, id, export)) return null;
        var action = status is 401 or 403 ? "refused"
            : resource == "banned-identifiers" && parts.Length > 3 && parts[3] == "lookup" ? "query"
            : export ? "export"
            : resource == "identity" || resource == "media" && path.EndsWith("/view") ? "view"
            : method is "GET" or "HEAD" ? (id is null ? "query" : "view")
            : "change";
        return (resource, id, action);
    }
}
