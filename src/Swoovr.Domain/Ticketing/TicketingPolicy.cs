namespace Swoovr.Domain;

/// <summary>
/// Queue access by Swoovr staff role (owner, 5 November 2026, Q133/#204). Swoovr is the only source of staff permissions;
/// Zammad grants are derived from this policy and synchronised (<see cref="StaffGrants"/>). Unknown names and roles never
/// receive an implicit grant.
/// </summary>
public static class TicketingPolicy
{
    public const string Restricted = "Trust & Safety — Restricted";
    public const string Legal = "Legal & Law Enforcement";

    public static readonly IReadOnlyList<string> Groups = Array.AsReadOnly(new[] {
        "General Support", "Billing & Subscriptions", "Account Recovery", "Appeals", "Trust & Safety",
        Restricted, Legal, "Privacy & Data Protection",
        "Public Relations & Media", "Partnerships & Business Development", "Marketing & Community",
        "Internal Operations", "Security" });

    /// <summary>Queues that also need the matching elevated capability and a sign-in within 15 minutes.</summary>
    public static readonly IReadOnlySet<string> Sensitive = new HashSet<string>(StringComparer.Ordinal) { Restricted, Legal, "Privacy & Data Protection", "Security" };

    public static string MemberLogin(Guid memberId) => $"swoovr-{memberId:D}";
    /// <summary>The token-only identity the Swoovr API acts as for one staff member. It has no password or email, so it can't sign in.</summary>
    public static string StaffLogin(Guid staffId) => $"swoovr-staff-{staffId:D}";
    /// <summary>The same staff member's Zammad web sign-in (central staff sign-in only). Never holds a sensitive queue.</summary>
    public static string StaffWebLogin(Guid staffId) => $"swoovr-staff-web-{staffId:D}";

    public static bool CanAccessGroup(AccountRole role, string group) => group switch
    {
        Legal => role == AccountRole.Legal,
        Restricted => role == AccountRole.TrustSafety,
        "Trust & Safety" or "Appeals" => role is AccountRole.TrustSafety or AccountRole.Admin,
        "Privacy & Data Protection" => role is AccountRole.Legal or AccountRole.Admin,
        "Internal Operations" or "Security" => role == AccountRole.Admin,
        "General Support" or "Billing & Subscriptions" or "Account Recovery" or
        "Public Relations & Media" or "Partnerships & Business Development" or "Marketing & Community"
            => role is AccountRole.Support or AccountRole.Admin or AccountRole.TrustSafety,
        _ => false,
    };

    /// <summary>Hand-off only: move a request into the queue, after which it is no longer visible to this role.</summary>
    public static bool CanHandOff(AccountRole role, string group) => group switch
    {
        Restricted => role is AccountRole.Support or AccountRole.Admin,
        Legal => role is AccountRole.Support or AccountRole.TrustSafety or AccountRole.Admin,
        _ => false,
    };

    public static bool CanTransfer(AccountRole role, string group) => CanAccessGroup(role, group) || CanHandOff(role, group);

    /// <summary>Moving a request into these queues never needs the receiving queue's elevation.</summary>
    public static bool IsHandOffTarget(string group) => group is Restricted or Legal;

    /// <summary>Roles as the API applies them: root admin acts as Admin, and Trust &amp; Safety also acts as Support.</summary>
    public static IReadOnlySet<AccountRole> EffectiveRoles(IEnumerable<AccountRole> granted, bool rootAdmin)
    {
        var roles = granted.Where(r => r is AccountRole.Support or AccountRole.TrustSafety or AccountRole.Legal or AccountRole.Admin).ToHashSet();
        if (rootAdmin) roles.Add(AccountRole.Admin);
        if (roles.Contains(AccountRole.TrustSafety)) roles.Add(AccountRole.Support);
        return roles;
    }

    /// <summary>
    /// The Zammad group grants for one staff identity: "full" where a role can work the queue, "create" where it can only
    /// hand off. The web sign-in identity (<paramref name="web"/>) never receives "full" on a sensitive queue, because Zammad
    /// can't enforce the elevated capability or the 15-minute sign-in; those queues are worked through Swoovr Admin only.
    /// </summary>
    public static IReadOnlyDictionary<string, string> StaffGrants(IReadOnlySet<AccountRole> roles, bool web)
    {
        var grants = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var group in Groups)
        {
            var full = roles.Any(r => CanAccessGroup(r, group));
            var transfer = roles.Any(r => CanTransfer(r, group));
            if (full && !(web && Sensitive.Contains(group))) grants[group] = "full";
            else if (transfer) grants[group] = "create";
        }
        return grants;
    }
}
