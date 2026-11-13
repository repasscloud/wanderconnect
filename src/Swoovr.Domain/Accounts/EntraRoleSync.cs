namespace Swoovr.Domain;

/// <summary>
/// D363 (owner, 13 November 2026): staff roles assigned through Entra app roles. At each Microsoft sign-in the staff
/// member's Entra app-role values (Keycloak claim <c>swoovr_entra_roles</c>, written by the Entra broker) decide their
/// Entra-managed grants. A grant made in Swoovr (Admin, invitation, CLI) is never added, changed or removed here, and it
/// keeps the role if Entra later removes it. Root admin is never an Entra role (D364).
/// </summary>
public static class EntraRoleSync
{
    /// <summary>Entra app role value → Swoovr role. Values are case-sensitive and anything else is ignored.</summary>
    public static readonly IReadOnlyDictionary<string, AccountRole> Values = new Dictionary<string, AccountRole>(StringComparer.Ordinal)
    {
        ["Support"] = AccountRole.Support,
        ["TrustSafety"] = AccountRole.TrustSafety,
        ["Admin"] = AccountRole.Admin,
        ["Legal"] = AccountRole.Legal,
    };

    public sealed record Plan(IReadOnlyList<AccountRole> Add, IReadOnlyList<RoleGrant> Remove)
    {
        public bool Changes => Add.Count > 0 || Remove.Count > 0;
    }

    public static IReadOnlySet<AccountRole> Parse(IEnumerable<string> claimValues) =>
        claimValues.Select(v => Values.TryGetValue(v, out var role) ? role : AccountRole.Unknown)
            .Where(r => r != AccountRole.Unknown).ToHashSet();

    /// <summary>What to change so the Entra-managed grants equal <paramref name="entraRoles"/>; direct grants are untouched.</summary>
    public static Plan Compute(IReadOnlyCollection<RoleGrant> current, IReadOnlySet<AccountRole> entraRoles)
    {
        var remove = current.Where(g => g.FromEntra && g.VenueId is null && !entraRoles.Contains(g.Role)).ToList();
        var add = entraRoles.Where(role => !current.Any(g => g.Role == role && g.VenueId is null)).OrderBy(r => r).ToList();
        return new(add, remove);
    }
}
