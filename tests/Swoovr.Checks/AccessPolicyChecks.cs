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
        var none = new HashSet<Guid>();
        var member = new AccessSession(AccountRole.Member, AccountRealm.Members, SignInMethod.Apple, false, true, true, true, false, none);
        Check("anonymous denied", false, null, AccountRole.Member);
        foreach (var method in Enum.GetValues<SignInMethod>())
            Check($"member {method} allowed in members realm", true, member with { Method = method }, AccountRole.Member);
        Check("member allowed without fresh session", true, member with { IsFresh = false }, AccountRole.Member);
        Check("member requires verified email", false, member with { EmailVerified = false }, AccountRole.Member);
        Check("inactive member denied", false, member with { IsActive = false }, AccountRole.Member);
        foreach (var realm in new[] { AccountRealm.Venues, AccountRealm.Staff, AccountRealm.Unknown, (AccountRealm)999 })
            Check($"member area rejects {realm} realm", false, member with { Realm = realm }, AccountRole.Member);
        Check("venue scope cannot be ignored by member area", false, member, AccountRole.Member, venueA);

        var partner = new AccessSession(AccountRole.VenuePartner, AccountRealm.Venues, SignInMethod.EmailPassword, false, true, true, true, false, new HashSet<Guid> { venueA });
        Check("partner password without mandatory MFA", true, partner, AccountRole.VenuePartner, venueA);
        Check("partner email link", true, partner with { Method = SignInMethod.EmailLink }, AccountRole.VenuePartner, venueA);
        Check("partner with optional MFA", true, partner with { MultiFactor = true }, AccountRole.VenuePartner, venueA);
        foreach (var method in new[] { SignInMethod.Apple, SignInMethod.Google, SignInMethod.Passkey, SignInMethod.CorporateSso, SignInMethod.Unknown, (SignInMethod)999 })
            Check($"partner rejects {method}", false, partner with { Method = method }, AccountRole.VenuePartner, venueA);
        foreach (var realm in new[] { AccountRealm.Members, AccountRealm.Staff, AccountRealm.Unknown })
            Check($"partner rejects {realm} realm", false, partner with { Realm = realm }, AccountRole.VenuePartner, venueA);
        Check("partner stale session denied", false, partner with { IsFresh = false }, AccountRole.VenuePartner, venueA);
        Check("partner unverified email denied", false, partner with { EmailVerified = false }, AccountRole.VenuePartner, venueA);
        Check("partner revoked", false, partner with { IsActive = false }, AccountRole.VenuePartner, venueA);
        Check("other venue denied", false, partner, AccountRole.VenuePartner, venueB);
        Check("missing venue denied", false, partner, AccountRole.VenuePartner);
        Check("empty venue denied", false, partner with { VenueIds = new HashSet<Guid> { Guid.Empty } }, AccountRole.VenuePartner, Guid.Empty);
        Check("revoked venue membership denied", false, partner with { VenueIds = none }, AccountRole.VenuePartner, venueA);
        Check("member account cannot enter venue area", false, member with { Realm = AccountRealm.Venues, Method = SignInMethod.EmailPassword }, AccountRole.VenuePartner, venueA);

        foreach (var role in new[] { AccountRole.Admin, AccountRole.Support, AccountRole.Legal })
        {
            var staff = new AccessSession(role, AccountRealm.Staff, SignInMethod.CorporateSso, false, true, true, true, false, none);
            Check($"{role} Entra SSO", true, staff, role);
            foreach (var method in new[] { SignInMethod.Apple, SignInMethod.Google, SignInMethod.EmailPassword, SignInMethod.EmailLink, SignInMethod.Passkey, SignInMethod.Unknown, (SignInMethod)999 })
                Check($"{role} rejects {method}", false, staff with { Method = method }, role);
            Check($"{role} rejects password even with OTP unless root admin", false, staff with { Method = SignInMethod.EmailPassword, MultiFactor = true }, role);
            foreach (var realm in new[] { AccountRealm.Members, AccountRealm.Venues, AccountRealm.Unknown })
                Check($"{role} rejects {realm} realm", false, staff with { Realm = realm }, role);
            Check($"{role} stale session denied", false, staff with { IsFresh = false }, role);
            Check($"{role} unverified email denied", false, staff with { EmailVerified = false }, role);
            Check($"{role} revoked", false, staff with { IsActive = false }, role);
            Check($"{role} venue scope rejected", false, staff, role, venueA);
            Check($"member cannot enter {role}", false, member, role);
            Check($"{role} cannot enter member area", false, staff, AccountRole.Member);
            Check($"{role} cannot enter venue area", false, staff, AccountRole.VenuePartner, venueA);
            var other = role == AccountRole.Admin ? AccountRole.Support : AccountRole.Admin;
            Check($"{role} cannot inherit {other}", false, staff, other);
        }

        var root = new AccessSession(AccountRole.Admin, AccountRealm.Staff, SignInMethod.EmailPassword, true, true, true, true, true, none);
        Check("root admin password + OTP", true, root, AccountRole.Admin);
        Check("root admin without OTP denied (MFA enforced, D36)", false, root with { MultiFactor = false }, AccountRole.Admin);
        Check("root admin via SSO denied (password only, D36)", false, root with { Method = SignInMethod.CorporateSso }, AccountRole.Admin);
        Check("root admin email link denied", false, root with { Method = SignInMethod.EmailLink }, AccountRole.Admin);
        Check("root admin stale session denied", false, root with { IsFresh = false }, AccountRole.Admin);
        Check("root admin outside staff realm denied", false, root with { Realm = AccountRealm.Members }, AccountRole.Admin);
        Check("root admin flag does not grant support", false, root with { Role = AccountRole.Support }, AccountRole.Support);
        // D189: the root admin may hold a granted `legal` role, with the same password + OTP evidence.
        Check("root admin with a legal grant", true, root with { Role = AccountRole.Legal }, AccountRole.Legal);
        Check("root admin legal without OTP denied", false, root with { Role = AccountRole.Legal, MultiFactor = false }, AccountRole.Legal);
        Check("root admin without a legal grant denied", false, root, AccountRole.Legal);
        Check("legal is staff-realm only", false, root with { Role = AccountRole.Legal, Realm = AccountRealm.Members }, AccountRole.Legal);
        Check("unknown role denied", false, member with { Role = (AccountRole)999 }, (AccountRole)999);
        Console.WriteLine($"PASS: {count} account access policy cases (application facts only; no provider authentication).");

        SessionEvidenceChecks.Run();
    }
}

static class SessionEvidenceChecks
{
    public static void Run()
    {
        var now = DateTimeOffset.Parse("2026-10-22T06:00:00Z");
        var policy = new SessionEvidencePolicy(new SessionEvidenceOptions("apple", "google", "repasscloud-entra"));
        var count = 0;
        void Check(string name, SessionEvidence expected, ValidatedSessionFacts? facts)
        {
            var actual = policy.Evaluate(facts, now);
            if (actual != expected) throw new Exception($"Session evidence: {name} gave {actual}");
            count++;
        }
        SessionEvidence Fresh(SignInMethod method, bool mfa = false) => new(method, mfa, true);
        var unknown = new SessionEvidence(SignInMethod.Unknown, false, false);
        ValidatedSessionFacts Facts(AccountRealm realm, string[] amr, string? broker = null, TimeSpan? age = null) =>
            new(realm, "subject-1", true, amr, broker, now - (age ?? TimeSpan.FromMinutes(1)), now.AddDays(-1));

        Check("null facts", unknown, null);
        Check("blank subject", unknown, Facts(AccountRealm.Members, ["pwd"]) with { Subject = " " });
        Check("unknown realm", unknown, Facts(AccountRealm.Unknown, ["pwd"]));
        Check("member password", Fresh(SignInMethod.EmailPassword), Facts(AccountRealm.Members, ["pwd"]));
        Check("member password + OTP", Fresh(SignInMethod.EmailPassword, true), Facts(AccountRealm.Members, ["pwd", "otp"]));
        Check("member passkey", Fresh(SignInMethod.Passkey), Facts(AccountRealm.Members, ["hwk"]));
        Check("member email link", Fresh(SignInMethod.EmailLink), Facts(AccountRealm.Members, ["swoovr-email-link"]));
        Check("member Apple", Fresh(SignInMethod.Apple), Facts(AccountRealm.Members, [], "apple"));
        Check("member Google", Fresh(SignInMethod.Google), Facts(AccountRealm.Members, [], "google"));
        Check("broker never also counts as password", Fresh(SignInMethod.Google), Facts(AccountRealm.Members, ["pwd"], "google"));
        Check("email-first registration session has no method", unknown, Facts(AccountRealm.Members, []));
        Check("OTP alone is not a method", unknown, Facts(AccountRealm.Members, ["otp"]));
        Check("unconfigured broker alias", unknown, Facts(AccountRealm.Members, [], "facebook"));
        Check("broker alias is case-sensitive", unknown, Facts(AccountRealm.Members, [], "Google"));
        Check("Apple alias in venues realm rejected", unknown, Facts(AccountRealm.Venues, [], "apple"));
        Check("staff SSO alias in members realm rejected", unknown, Facts(AccountRealm.Members, [], "repasscloud-entra"));
        Check("Google alias in staff realm rejected", unknown, Facts(AccountRealm.Staff, [], "google"));
        Check("staff Entra SSO", Fresh(SignInMethod.CorporateSso), Facts(AccountRealm.Staff, [], "repasscloud-entra"));
        Check("staff root admin password + OTP", Fresh(SignInMethod.EmailPassword, true), Facts(AccountRealm.Staff, ["pwd", "otp"]));
        Check("venue password", Fresh(SignInMethod.EmailPassword), Facts(AccountRealm.Venues, ["pwd"]));
        Check("venue passkey not recognised", unknown, Facts(AccountRealm.Venues, ["hwk"]));
        Check("password wins over link when both claimed", Fresh(SignInMethod.EmailPassword), Facts(AccountRealm.Venues, ["pwd", "swoovr-email-link"]));
        Check("12 hours is still fresh", Fresh(SignInMethod.EmailPassword), Facts(AccountRealm.Venues, ["pwd"], age: TimeSpan.FromHours(12)));
        Check("older than 12 hours is stale", new(SignInMethod.EmailPassword, false, false), Facts(AccountRealm.Venues, ["pwd"], age: TimeSpan.FromHours(12).Add(TimeSpan.FromSeconds(1))));
        Check("future auth time beyond skew is stale", new(SignInMethod.EmailPassword, false, false), Facts(AccountRealm.Venues, ["pwd"], age: TimeSpan.FromSeconds(-6)));
        var unconfigured = new SessionEvidencePolicy(new SessionEvidenceOptions());
        foreach (var broker in new[] { "apple", "google", "repasscloud-entra" })
            if (unconfigured.Evaluate(Facts(broker == "repasscloud-entra" ? AccountRealm.Staff : AccountRealm.Members, [], broker), now) != unknown)
                throw new Exception($"Session evidence: unconfigured {broker} must be Unknown");
            else count++;
        Console.WriteLine($"PASS: {count} session evidence cases (validated-token facts only; ambiguous evidence maps to Unknown).");
    }
}
