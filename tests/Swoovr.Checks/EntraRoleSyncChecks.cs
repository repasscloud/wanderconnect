using Swoovr.Domain;

/// <summary>D363: Entra app roles manage only Entra-sourced grants; Swoovr grants are never touched (D364: root is never an Entra role).</summary>
static class EntraRoleSyncChecks
{
    public static void Run()
    {
        var checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception("entra role sync: " + name); checks++; }
        RoleGrant Grant(AccountRole role, string? source = null) => new() { Id = Guid.NewGuid(), Role = role, Source = source };

        var parsed = EntraRoleSync.Parse(["Admin", "TrustSafety", "Support", "Legal"]);
        Check(parsed.SetEquals([AccountRole.Admin, AccountRole.TrustSafety, AccountRole.Support, AccountRole.Legal]), "the four app role values map");
        Check(EntraRoleSync.Parse(["admin", "Root", "RootAdmin", "Member", "VenuePartner", "Business", " Admin", ""]).Count == 0,
            "values are exact and case-sensitive; root, member, venue and business are never Entra roles");

        var added = EntraRoleSync.Compute([], EntraRoleSync.Parse(["Admin"]));
        Check(added.Add.SequenceEqual([AccountRole.Admin]) && added.Remove.Count == 0, "a new Entra role is added");

        var entraAdmin = Grant(AccountRole.Admin, RoleGrant.EntraSource);
        var removed = EntraRoleSync.Compute([entraAdmin], EntraRoleSync.Parse([]));
        Check(removed.Remove.SequenceEqual([entraAdmin]) && removed.Add.Count == 0, "leaving the Entra group removes the Entra role");

        var direct = Grant(AccountRole.Support);
        var kept = EntraRoleSync.Compute([direct], EntraRoleSync.Parse([]));
        Check(!kept.Changes, "a Swoovr grant survives an empty Entra role list");

        var both = EntraRoleSync.Compute([direct], EntraRoleSync.Parse(["Support"]));
        Check(!both.Changes, "a Swoovr grant for the same role is not duplicated or taken over by Entra");

        var mixed = EntraRoleSync.Compute([direct, entraAdmin, Grant(AccountRole.Legal, RoleGrant.EntraSource)], EntraRoleSync.Parse(["Admin", "TrustSafety"]));
        Check(mixed.Add.SequenceEqual([AccountRole.TrustSafety]) && mixed.Remove.Select(g => g.Role).SequenceEqual([AccountRole.Legal]),
            "only Entra-sourced grants follow Entra");

        var venue = new RoleGrant { Id = Guid.NewGuid(), Role = AccountRole.VenuePartner, VenueId = Guid.NewGuid(), Source = RoleGrant.EntraSource };
        Check(!EntraRoleSync.Compute([venue], EntraRoleSync.Parse([])).Changes, "venue grants are never Entra-managed");

        // Access: an Entra staff session needs the Keycloak one-time code (amr otp) after Microsoft, like every staff session.
        var policy = new SessionEvidencePolicy(new SessionEvidenceOptions(StaffSsoAlias: "entra"));
        var now = DateTimeOffset.UtcNow;
        ValidatedSessionFacts Facts(string[] amr, string? idp) => new(AccountRealm.Staff, "subject", true, amr, idp, now, null);
        var withCode = policy.Evaluate(Facts(["otp"], "entra"), now);
        Check(withCode is { Method: SignInMethod.CorporateSso, MultiFactor: true }, "Microsoft sign-in plus Keycloak code is Entra MFA");
        Check(policy.Evaluate(Facts([], "entra"), now) is { Method: SignInMethod.CorporateSso, MultiFactor: false }, "Microsoft alone is not staff MFA");
        var access = new AccountAccessPolicy();
        Check(access.Allows(new AccessSession(AccountRole.Admin, AccountRealm.Staff, SignInMethod.CorporateSso, true, true, true, true, false, new HashSet<Guid>()), AccountRole.Admin),
            "an Entra Admin with MFA is allowed");
        Check(!access.Allows(new AccessSession(AccountRole.Admin, AccountRealm.Staff, SignInMethod.EmailPassword, true, true, true, true, false, new HashSet<Guid>()), AccountRole.Admin),
            "a non-root password session is refused outside Development");
        Check(access.Allows(new AccessSession(AccountRole.Admin, AccountRealm.Staff, SignInMethod.EmailPassword, true, true, true, true, true, new HashSet<Guid>()), AccountRole.Admin),
            "the break-glass root admin signs in with password plus code (D36, D364)");
        Check(!access.Allows(new AccessSession(AccountRole.Admin, AccountRealm.Staff, SignInMethod.CorporateSso, true, true, true, true, true, new HashSet<Guid>()), AccountRole.Admin),
            "root through Microsoft is refused: the break-glass account never uses Entra");
        Console.WriteLine($"PASS: Entra role sync and break-glass root access ({checks} checks).");
    }
}
