using Swoovr.Domain;

static class AccessPolicyChecks
{
    public static void Run()
    {
        var policy = new AccountAccessPolicy();
        var count = 0;
        void Check(string name, bool expected, AccessSession? session, AccountRole area,
            Guid? venue = null)
        {
            if (policy.Allows(session, area, venue) != expected)
                throw new Exception($"Access policy: {name}");
            count++;
        }

        var venueA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var venueB = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var member = new AccessSession(AccountRole.Member, SignInMethod.Apple, false, true, new HashSet<Guid>());
        Check("anonymous denied", false, null, AccountRole.Member);
        foreach (var method in new[] { SignInMethod.Apple, SignInMethod.Google, SignInMethod.EmailPassword })
            Check($"member {method}", true, member with { Method = method }, AccountRole.Member);
        Check("member links remain unapproved", false, member with { Method = SignInMethod.EmailLink }, AccountRole.Member);
        foreach (var role in new[] { AccountRole.VenuePartner, AccountRole.Admin, AccountRole.Support })
        {
            // A hypothetical approved server verdict, not a configured Keycloak flow.
            var staff = new AccessSession(role, SignInMethod.EmailPassword, true, true, new HashSet<Guid> { venueA });
            var scope = role == AccountRole.VenuePartner ? venueA : (Guid?)null;
            Check($"{role} dedicated password without mandatory MFA", true, staff, role, scope);
            Check($"{role} dedicated link", true, staff with { Method = SignInMethod.EmailLink }, role, scope);
            foreach (var method in new[] { SignInMethod.Apple, SignInMethod.Google, SignInMethod.Unknown, (SignInMethod)999 })
                Check($"{role} rejects {method}", false, staff with { Method = method }, role, scope);
            Check($"{role} requires approved privileged email evidence", false, staff with { HasApprovedPrivilegedEmailEvidence = false }, role, scope);
            Check($"{role} revoked", false, staff with { IsActive = false }, role, scope);
            Check($"member cannot enter {role}", false, member, role, scope);
            Check($"{role} cannot enter member area", false, staff, AccountRole.Member);
            foreach (var other in new[] { AccountRole.VenuePartner, AccountRole.Admin, AccountRole.Support }.Where(x => x != role))
                Check($"{role} cannot inherit {other}", false, staff, other, other == AccountRole.VenuePartner ? venueA : null);
        }
        var partner = new AccessSession(AccountRole.VenuePartner, SignInMethod.EmailPassword, true, true, new HashSet<Guid> { venueA });
        Check("other venue denied", false, partner, AccountRole.VenuePartner, venueB);
        Check("missing venue denied", false, partner, AccountRole.VenuePartner);
        Check("empty venue denied", false, partner with { VenueIds = new HashSet<Guid> { Guid.Empty } }, AccountRole.VenuePartner, Guid.Empty);
        Check("revoked venue membership denied", false, partner with { VenueIds = new HashSet<Guid>() }, AccountRole.VenuePartner, venueA);
        Check("unknown member method denied", false, member with { Method = SignInMethod.Unknown }, AccountRole.Member);
        Check("invalid member method denied", false, member with { Method = (SignInMethod)999 }, AccountRole.Member);
        Check("inactive member denied", false, member with { IsActive = false }, AccountRole.Member);
        Check("unknown role denied", false, member with { Role = (AccountRole)999 }, (AccountRole)999);
        Check("venue scope cannot be ignored by member area", false, member, AccountRole.Member, venueA);
        Console.WriteLine($"PASS: {count} account access policy cases (application facts only; no provider authentication).");
    }
}
