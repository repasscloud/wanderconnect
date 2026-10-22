using Swoovr.Domain;

static class AccountPolicyChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(string name, bool condition)
        {
            if (!condition) throw new Exception($"Account policy: {name}");
            count++;
        }

        // Email normalisation: case and whitespace only, never provider rewriting.
        Check("lower-cases and trims", EmailAddress.Normalise("  Ada.Example@Example.TEST ") == "ada.example@example.test");
        Check("keeps dots and plus tags", EmailAddress.Normalise("a.b+c@example.test") == "a.b+c@example.test");
        foreach (var bad in new[] { null, "", " ", "no-at", "@example.test", "a@", "a@b", "a@@example.test", "a b@example.test", "a\n@example.test" })
            Check($"rejects {bad ?? "null"}", EmailAddress.Normalise(bad) is null);

        // Age entry (owner, 23 October 2026, refining D40).
        var today = new DateOnly(2026, 10, 23);
        Check("birthday today counts", AgePolicy.YearsOn(new DateOnly(2008, 10, 23), today) == 18);
        Check("birthday tomorrow does not", AgePolicy.YearsOn(new DateOnly(2008, 10, 24), today) == 17);
        Check("29 February birthday", AgePolicy.YearsOn(new DateOnly(2008, 2, 29), new DateOnly(2026, 2, 28)) == 17);
        var adult = AgePolicy.Evaluate(AgeStatus.Attested, new DateOnly(1990, 5, 1), null, today);
        Check("adult DOB is self-declared, not verified", adult is { Error: AgeChangeError.None, Status: AgeStatus.SelfDeclaredAdult, LocksAccount: false, Age: 36 });
        Check("DOB wins over an entered age", AgePolicy.Evaluate(AgeStatus.Attested, new DateOnly(1990, 5, 1), 20, today).Age == 36);
        Check("age alone", AgePolicy.Evaluate(AgeStatus.Attested, null, 30, today) is { Status: AgeStatus.SelfDeclaredAdult, Age: 30 });
        Check("both blank keeps attested", AgePolicy.Evaluate(AgeStatus.Attested, null, null, today) is { Status: AgeStatus.Attested, LocksAccount: false, Age: null });
        Check("clearing a self-declared age returns to attested", AgePolicy.Evaluate(AgeStatus.SelfDeclaredAdult, null, null, today).Status == AgeStatus.Attested);
        Check("under-18 DOB locks", AgePolicy.Evaluate(AgeStatus.Attested, new DateOnly(2008, 10, 24), null, today) is { Status: AgeStatus.Underage, LocksAccount: true });
        Check("under-18 age locks", AgePolicy.Evaluate(AgeStatus.Attested, null, 17, today) is { Status: AgeStatus.Underage, LocksAccount: true });
        Check("under-18 entry locks even when verified", AgePolicy.Evaluate(AgeStatus.Verified, null, 16, today).LocksAccount);
        Check("verified stays verified on an adult entry", AgePolicy.Evaluate(AgeStatus.Verified, null, 40, today).Status == AgeStatus.Verified);
        Check("staff override stays", AgePolicy.Evaluate(AgeStatus.AdultOverride, new DateOnly(1980, 1, 1), null, today).Status == AgeStatus.AdultOverride);
        Check("future DOB rejected", AgePolicy.Evaluate(AgeStatus.Attested, today.AddDays(1), null, today).Error == AgeChangeError.DateOfBirthInFuture);
        Check("ancient DOB rejected", AgePolicy.Evaluate(AgeStatus.Attested, new DateOnly(1900, 1, 1), null, today).Error == AgeChangeError.DateOfBirthTooOld);
        Check("negative age rejected", AgePolicy.Evaluate(AgeStatus.Attested, null, -1, today).Error == AgeChangeError.AgeOutOfRange);
        Check("age over 120 rejected", AgePolicy.Evaluate(AgeStatus.Attested, null, 121, today).Error == AgeChangeError.AgeOutOfRange);
        var declared = new MemberProfile { DeclaredAge = 30, DeclaredOn = new DateOnly(2024, 11, 1) };
        Check("declared age grows with time", AgePolicy.CurrentAge(declared, today) == 31);
        Check("no details, no age", AgePolicy.CurrentAge(new MemberProfile(), today) is null);

        // Badges (D55, D57, D58): one fact each; admin verified never shows or implies ID verified.
        Check("no profile, no badges", BadgePolicy.For(null).Count == 0);
        Check("admin verified has no badge", BadgePolicy.For(new MemberProfile { AdminVerified = true }).Count == 0);
        foreach (var status in Enum.GetValues<VerificationStatus>().Where(s => s != VerificationStatus.Verified))
            Check($"ID {status} shows nothing", BadgePolicy.For(new MemberProfile { IdStatus = status, PhotoStatus = status }).Count == 0);
        var all = BadgePolicy.For(new MemberProfile
        {
            FoundingMember = true, PhotoStatus = VerificationStatus.Verified, IdStatus = VerificationStatus.Verified,
            IdCheckedAt = DateTimeOffset.UnixEpoch, Employee = true, Bot = true, AdminVerified = true,
        });
        Check("every applicable badge, in order", all.Select(b => b.Badge).SequenceEqual([Badge.Founder, Badge.PhotoVerified, Badge.IdVerified, Badge.Staff, Badge.Bot]));
        Check("checked date carried", all.Single(b => b.Badge == Badge.IdVerified).CheckedAt == DateTimeOffset.UnixEpoch);

        // Invitations: matched once, in the invited realm, by that realm's own sign-in method.
        var now = DateTimeOffset.Parse("2026-10-23T00:00:00Z");
        var venue = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var staffInvite = new Invitation { Realm = AccountRealm.Staff, Role = AccountRole.Support, Email = "staff@example.test", ExpiresAt = now.AddDays(7) };
        var venueInvite = new Invitation { Realm = AccountRealm.Venues, Role = AccountRole.VenuePartner, VenueId = venue, Email = "venue@example.test", ExpiresAt = now.AddDays(7) };
        Check("staff via Entra", InvitationPolicy.CanAccept(staffInvite, AccountRealm.Staff, SignInMethod.CorporateSso, true, "Staff@Example.test", now));
        Check("venue via password", InvitationPolicy.CanAccept(venueInvite, AccountRealm.Venues, SignInMethod.EmailPassword, true, "venue@example.test", now));
        Check("venue via email link", InvitationPolicy.CanAccept(venueInvite, AccountRealm.Venues, SignInMethod.EmailLink, true, "venue@example.test", now));
        foreach (var method in new[] { SignInMethod.Apple, SignInMethod.Google, SignInMethod.Passkey, SignInMethod.EmailPassword, SignInMethod.EmailLink, SignInMethod.Unknown })
            Check($"staff invite rejects {method}", !InvitationPolicy.CanAccept(staffInvite, AccountRealm.Staff, method, true, "staff@example.test", now));
        foreach (var method in new[] { SignInMethod.Apple, SignInMethod.Google, SignInMethod.Passkey, SignInMethod.CorporateSso, SignInMethod.Unknown })
            Check($"venue invite rejects {method}", !InvitationPolicy.CanAccept(venueInvite, AccountRealm.Venues, method, true, "venue@example.test", now));
        // A member whose email matches a staff or venue invitation gains nothing (no merge by email).
        Check("member realm never accepts a staff invite", !InvitationPolicy.CanAccept(staffInvite, AccountRealm.Members, SignInMethod.EmailPassword, true, "staff@example.test", now));
        Check("member realm never accepts a venue invite", !InvitationPolicy.CanAccept(venueInvite, AccountRealm.Members, SignInMethod.EmailPassword, true, "venue@example.test", now));
        Check("unverified email rejected", !InvitationPolicy.CanAccept(staffInvite, AccountRealm.Staff, SignInMethod.CorporateSso, false, "staff@example.test", now));
        Check("other email rejected", !InvitationPolicy.CanAccept(staffInvite, AccountRealm.Staff, SignInMethod.CorporateSso, true, "staff2@example.test", now));
        Check("expired rejected", !InvitationPolicy.CanAccept(staffInvite, AccountRealm.Staff, SignInMethod.CorporateSso, true, "staff@example.test", now.AddDays(7)));
        Check("revoked rejected", !InvitationPolicy.CanAccept(new Invitation { Realm = AccountRealm.Staff, Role = AccountRole.Support, Email = "staff@example.test", ExpiresAt = now.AddDays(7), RevokedAt = now }, AccountRealm.Staff, SignInMethod.CorporateSso, true, "staff@example.test", now));
        Check("accepted once only", !InvitationPolicy.CanAccept(new Invitation { Realm = AccountRealm.Staff, Role = AccountRole.Support, Email = "staff@example.test", ExpiresAt = now.AddDays(7), AcceptedAt = now }, AccountRealm.Staff, SignInMethod.CorporateSso, true, "staff@example.test", now));
        Check("role and realm must agree", !InvitationPolicy.CanAccept(new Invitation { Realm = AccountRealm.Venues, Role = AccountRole.Admin, Email = "staff@example.test", ExpiresAt = now.AddDays(7) }, AccountRealm.Venues, SignInMethod.EmailPassword, true, "staff@example.test", now));
        Check("null invitation", !InvitationPolicy.CanAccept(null, AccountRealm.Staff, SignInMethod.CorporateSso, true, "staff@example.test", now));
        Check("member role is never invited", !InvitationPolicy.IsValidRequest(AccountRole.Member, "a@example.test", null));
        Check("partner needs a venue", !InvitationPolicy.IsValidRequest(AccountRole.VenuePartner, "a@example.test", null));
        Check("partner empty venue", !InvitationPolicy.IsValidRequest(AccountRole.VenuePartner, "a@example.test", Guid.Empty));
        Check("staff takes no venue", !InvitationPolicy.IsValidRequest(AccountRole.Admin, "a@example.test", venue));
        Check("bad email", !InvitationPolicy.IsValidRequest(AccountRole.Admin, "not-an-email", null));
        Check("valid partner", InvitationPolicy.IsValidRequest(AccountRole.VenuePartner, "a@example.test", venue));

        // Private beta (D62): members only, verified email or a bound enrolment, revocation ends it.
        var account = Guid.NewGuid();
        var open = new BetaEnrolment { Email = "beta@example.test" };
        BetaAccess Beta(bool enabled, bool persistence, AccountRealm realm, bool verified, string? email, params BetaEnrolment[] list) =>
            BetaAccessPolicy.Evaluate(enabled, persistence, realm, account, verified, email, list);
        Check("verified email matches", Beta(true, true, AccountRealm.Members, true, "Beta@Example.test", open) == BetaAccess.Enrolled);
        Check("unverified email never matches", Beta(true, true, AccountRealm.Members, false, "beta@example.test", open) == BetaAccess.NotEnrolled);
        Check("other email", Beta(true, true, AccountRealm.Members, true, "other@example.test", open) == BetaAccess.NotEnrolled);
        Check("no enrolments", Beta(true, true, AccountRealm.Members, true, "beta@example.test") == BetaAccess.NotEnrolled);
        Check("revoked", Beta(true, true, AccountRealm.Members, true, "beta@example.test", new BetaEnrolment { Email = "beta@example.test", RevokedAt = now }) == BetaAccess.NotEnrolled);
        Check("bound to this account survives an email change", Beta(true, true, AccountRealm.Members, true, "new@example.test", new BetaEnrolment { Email = "beta@example.test", AccountId = account }) == BetaAccess.Enrolled);
        Check("bound to another account does not transfer", Beta(true, true, AccountRealm.Members, true, "beta@example.test", new BetaEnrolment { Email = "beta@example.test", AccountId = Guid.NewGuid() }) == BetaAccess.NotEnrolled);
        Check("revoked binding ends access", Beta(true, true, AccountRealm.Members, true, "beta@example.test", new BetaEnrolment { Email = "beta@example.test", AccountId = account, RevokedAt = now }) == BetaAccess.NotEnrolled);
        Check("no database fails closed", Beta(true, false, AccountRealm.Members, true, "beta@example.test", open) == BetaAccess.Unavailable);
        Check("beta off", Beta(false, false, AccountRealm.Members, true, null) == BetaAccess.NotRequired);
        foreach (var realm in new[] { AccountRealm.Venues, AccountRealm.Staff })
            Check($"{realm} not gated", Beta(true, true, realm, true, null) == BetaAccess.NotRequired);
        Check("allows enrolled and not required only", BetaAccessPolicy.Allows(BetaAccess.Enrolled) && BetaAccessPolicy.Allows(BetaAccess.NotRequired)
            && !BetaAccessPolicy.Allows(BetaAccess.NotEnrolled) && !BetaAccessPolicy.Allows(BetaAccess.Unavailable));

        Console.WriteLine($"PASS: {count} account policy cases (email normalisation, age entry and lock, badges, invitations, beta gate).");
    }
}
