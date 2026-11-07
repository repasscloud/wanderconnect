using Swoovr.Contracts;

namespace Swoovr.Admin;

/// <summary>Readable staff labels for account wire values (#156); data attributes keep the raw values.</summary>
public static class AccountLabels
{
    public static string Status(string? status) => status switch
    {
        "active" => "Active", "suspended" => "Suspended", "restricted" => "Restricted", "terminated" => "Terminated",
        "pendingDeletion" => "Deletion pending", "deleted" => "Deleted", "disconnected" => "Disconnected", "locked" => "Locked", null => "Unknown", _ => Title(status),
    };
    public static string Age(string? status) => status switch
    {
        "unconfirmed" => "Not confirmed", "attested" => "18+ confirmed at sign-up", "selfDeclaredAdult" => "Self-declared adult",
        "underage" => "Under 18 (locked)", "needsVerification" => "Age check required", "verified" => "Verified by ID", "adultOverride" => "Adult override by staff",
        null => "Not recorded", _ => Title(status),
    };
    public static string Id(string? status) => status switch
    {
        "notStarted" => "ID not verified", "pending" => "ID check pending", "inReview" => "ID check in review", "verified" => "ID verified",
        "declined" => "ID check declined", "rejected" => "ID check rejected", "revoked" => "ID verified revoked", "expired" => "ID check expired",
        null => "ID not verified", _ => "ID " + Title(status).ToLowerInvariant(),
    };
    public static string Plan(string? plan) => plan switch { "free" => "Free", "plus" => "Plus", "premium" => "Premium", null => "Free", _ => Title(plan) };
    public static string Realm(string realm) => realm switch { "members" => "Member", "venues" => "Venue partner", "staff" => "Staff", _ => Title(realm) };
    public static string Source(string source) => source switch { "adminGrant" => "Staff grant", "founder" => "Founder", "subscription" => "Paid subscription", _ => Title(source) };
    public static string Attempt(string state) => state switch
    {
        "created" => "Created", "inProgress" => "In progress", "inReview" => "In review", "approved" => "Approved", "declined" => "Declined",
        "abandoned" => "Abandoned", "expired" => "Expired", _ => Title(state),
    };
    public static string Sanction(string kind) => kind switch { "warning" => "Warning", "restriction" => "Restricted", "suspension" => "Suspended", "termination" => "Terminated", _ => Title(kind) };
    public static string Reason(string? code) => AccountReasons.Label(code);
    public static string Role(string? role) => role switch { "business" => "Business", "admin" => "Admin", "trustSafety" => "Trust & Safety", "support" => "Support", "legal" => "Legal", null => "", _ => Title(role) };
    private static string Title(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var spaced = string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
        return char.ToUpperInvariant(spaced[0]) + spaced[1..];
    }
}
