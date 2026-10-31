using Swoovr.Domain;

/// <summary>
/// Staff access controls (internal policy §11, §15; procedure §5; #88): who may ask for which capability, 60-minute grants
/// and renewals, bulk export approval by a second officer, conflict-of-interest exclusions, revoked sessions, the
/// sensitive-access log's classification (never a query value or export token) and its hash chain.
/// </summary>
static class StaffAccessChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Staff access check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var (me, other, member, linked) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Func<AccountRole, bool> legal = r => r == AccountRole.Legal, adminRole = r => r == AccountRole.Admin, support = r => r == AccountRole.Support;

        // Requests.
        Expect(StaffAccessPolicy.CheckRequest(StaffAccessPolicy.IdentifierRelease,"Fictional reviewed reassignment",adminRole)==AccessRefusal.None,"admins may request separate identifier-release permission");
        Expect(StaffAccessPolicy.CheckRequest(StaffAccessPolicy.AccountReinstatement,"Fictional reviewed reinstatement",adminRole)==AccessRefusal.None,"admins may request separate reinstatement permission");
        Expect(StaffAccessPolicy.CheckRequest(StaffAccessPolicy.IdentifierRelease,"Fictional",support)==AccessRefusal.NotPermitted,"support cannot release identifiers");
        Expect(StaffAccessPolicy.CheckRequest(StaffAccessPolicy.AccountReinstatement,"Fictional",legal)==AccessRefusal.NotPermitted,"legal evidence role cannot reinstate accounts");
        Expect(StaffAccessPolicy.CheckRequest("identity-open", "Fictional police request REF-1", legal) == AccessRefusal.None, "legal opens identities");
        Expect(StaffAccessPolicy.CheckRequest("evidence-export", "Fictional disclosure", legal) == AccessRefusal.None, "legal exports evidence");
        Expect(StaffAccessPolicy.CheckRequest("termination", "Fictional serious breach", adminRole) == AccessRefusal.None, "admins terminate");
        Expect(StaffAccessPolicy.CheckRequest("identity-open", "x", adminRole) == AccessRefusal.NotPermitted, "admins don't open identities");
        Expect(StaffAccessPolicy.CheckRequest("termination", "x", legal) == AccessRefusal.NotPermitted, "legal doesn't terminate");
        Expect(StaffAccessPolicy.CheckRequest("evidence-export", "x", support) == AccessRefusal.NotPermitted, "support gets nothing");
        Expect(StaffAccessPolicy.CheckRequest("everything", "x", legal) == AccessRefusal.Unknown, "only listed capabilities");
        Expect(StaffAccessPolicy.CheckRequest("identity-open", " ", legal) == AccessRefusal.PurposeRequired, "a purpose is recorded");
        Expect(StaffAccessPolicy.CheckRequest("identity-open", new string('x', 501), legal) == AccessRefusal.PurposeRequired, "purposes are bounded");

        // Grants expire on their own and renew up to eight times.
        var grant = StaffAccessPolicy.Grant(me, "identity-open", " Fictional purpose ", now);
        Expect(grant.ExpiresAt == now.AddMinutes(60) && grant.Purpose == "Fictional purpose", "60 minutes with a trimmed purpose");
        Expect(StaffAccessPolicy.Active(grant, now.AddMinutes(59)) && !StaffAccessPolicy.Active(grant, now.AddMinutes(60)), "automatic expiry");
        Expect(StaffAccessPolicy.CheckRenew(grant, other, now) == AccessRefusal.NotOwner, "only the holder renews");
        Expect(StaffAccessPolicy.CheckRenew(grant, me, now.AddMinutes(61)) == AccessRefusal.Expired, "an expired grant isn't renewed");
        Expect(StaffAccessPolicy.CheckRenew(grant, me, now.AddMinutes(30)) == AccessRefusal.None, "a live grant renews");
        grant.Renewals = StaffAccessPolicy.MaxRenewals;
        Expect(StaffAccessPolicy.CheckRenew(grant, me, now.AddMinutes(30)) == AccessRefusal.RenewalLimit, "eight renewals at most");
        grant.RevokedAt = now;
        Expect(!StaffAccessPolicy.Active(grant, now.AddMinutes(1)), "a revoked grant ends at once");

        // Bulk exports need a second officer.
        Expect(!StaffAccessPolicy.Bulk(5, 1000) && StaffAccessPolicy.Bulk(6, 10) && StaffAccessPolicy.Bulk(1, 1001), "bulk: over five accounts or a thousand items");
        var hold = Guid.NewGuid();
        var approval = new ExportApproval { Id = Guid.NewGuid(), HoldId = hold, RequestedBy = me, RequestedAt = now };
        Expect(StaffAccessPolicy.CheckApprove(approval, me, now) == AccessRefusal.NotSeparated, "never self-approved");
        Expect(StaffAccessPolicy.CheckApprove(approval, other, now.AddHours(25)) == AccessRefusal.Used, "approved within a day");
        Expect(StaffAccessPolicy.CheckApprove(approval, other, now) == AccessRefusal.None, "another officer approves");
        Expect(!StaffAccessPolicy.ApprovalUsable(approval, me, hold, now), "unusable until approved");
        (approval.ApprovedBy, approval.ApprovedAt) = (other, now);
        Expect(StaffAccessPolicy.CheckApprove(approval, other, now) == AccessRefusal.Used, "approved once");
        Expect(StaffAccessPolicy.ApprovalUsable(approval, me, hold, now.AddHours(1)), "usable by the requester for that hold");
        Expect(!StaffAccessPolicy.ApprovalUsable(approval, other, hold, now) && !StaffAccessPolicy.ApprovalUsable(approval, me, Guid.NewGuid(), now), "nobody else, no other hold");
        approval.UsedAt = now;
        Expect(!StaffAccessPolicy.ApprovalUsable(approval, me, hold, now), "used once");

        // Conflicts of interest.
        var exclusions = new[] { new StaffExclusion { StaffAccountId = me, SubjectAccountId = member } };
        var links = new[] { new StaffMemberLink { StaffAccountId = me, MemberAccountId = linked } };
        Expect(StaffAccessPolicy.Excluded(me, [member], exclusions, []), "a declared exclusion");
        Expect(StaffAccessPolicy.Excluded(me, [Guid.NewGuid(), linked], [], links), "one's own linked member account");
        Expect(!StaffAccessPolicy.Excluded(other, [member, linked], exclusions, links), "others aren't excluded");
        Expect(!StaffAccessPolicy.Excluded(me, [Guid.NewGuid()], exclusions, links), "unrelated members aren't");

        // Revoked staff sessions.
        Expect(StaffAccessPolicy.SessionRevoked(now, now.AddMinutes(-5)) && StaffAccessPolicy.SessionRevoked(now, now), "earlier sign-ins are refused");
        Expect(!StaffAccessPolicy.SessionRevoked(now, now.AddMinutes(1)) && !StaffAccessPolicy.SessionRevoked(null, now), "a later sign-in or no revocation is fine");

        // Classification for the sensitive-access log.
        var id = Guid.NewGuid();
        Expect(StaffAccessPolicy.Classify("GET", $"/api/admin/holds/{id}", 200) == ("holds", id.ToString(), "view"), "a hold view");
        Expect(StaffAccessPolicy.Classify("GET", "/api/admin/holds", 200) == ("holds", null, "query"), "a hold list is a query");
        Expect(StaffAccessPolicy.Classify("POST", $"/api/admin/holds/{id}/exports", 201) == ("holds", id.ToString(), "export"), "creating an export");
        Expect(StaffAccessPolicy.Classify("GET", "/api/admin/evidence-exports/SECRET-TOKEN", 200) == ("evidence-export", null, "export"), "the export token is never logged");
        Expect(StaffAccessPolicy.Classify("GET", "/api/admin/contact-history", 200) == ("contact-history", null, "query"), "contact-history searches, never the value");
        Expect(StaffAccessPolicy.Classify("POST", $"/api/admin/accounts/{id}/identity", 200) == ("identity", id.ToString(), "view"), "opening an identity");
        Expect(StaffAccessPolicy.Classify("POST", $"/api/admin/accounts/{id}/identity", 403) == ("identity", id.ToString(), "refused"), "refused attempts");
        Expect(StaffAccessPolicy.Classify("GET", "/api/admin/law-enforcement/audit-export", 200)?.Action == "export", "the audit export");
        Expect(StaffAccessPolicy.Classify("POST", $"/api/admin/law-enforcement/cases/{id}/verify", 200) == ("le-case", id.ToString(), "change"), "case changes");
        Expect(StaffAccessPolicy.Classify("GET", $"/api/admin/accounts/{id}/summary", 200) is null, "ordinary admin routes aren't sensitive");
        Expect(StaffAccessPolicy.Classify("GET", "/api/me", 200) is null, "member routes aren't logged here");
        Expect(StaffAccessPolicy.Classify("GET",$"/api/admin/Cases/{id}",403)==("moderation-case",id.ToString(),"refused"),"case evidence read refusals are restricted metadata");
        Expect(StaffAccessPolicy.Classify("GET","/api/admin/appeals",200)==("moderation-appeal",null,"query"),"appeal-list audit omits its query values/content");
        Expect(StaffAccessPolicy.Classify("POST","/api/admin/banned-identifiers/lookup",200)==("banned-identifiers",null,"query"),"registry lookup is a restricted query without the value");
        Expect(StaffAccessPolicy.Classify("GET",$"/API/ADMIN/BANNED-IDENTIFIERS/{id}",403)==("banned-identifiers",id.ToString(),"refused"),"route casing cannot bypass sensitive logging");
        Expect(StaffAccessPolicy.Classify("POST",$"/api/admin/accounts/{id}/reinstate",200)==("account-reinstatement",id.ToString(),"change"),"account reinstatement is audited separately");
        Expect(StaffAccessPolicy.Classify("GET",$"/api/admin/accounts/{id}/reinstatement",200)==("account-reinstatement",id.ToString(),"view"),"restricted reinstatement preview is an audited view");


        // The chain.
        var entries = new List<SensitiveAccess>();
        byte[] previous = [];
        foreach (var (action, status) in new[] { ("view", 200), ("refused", 403), ("export", 200) })
        {
            var at = now.AddSeconds(entries.Count);
            var hash = StaffAccessPolicy.Chain(previous, at, me, action, "holds", null, status);
            entries.Add(new SensitiveAccess { Sequence = entries.Count + 1, At = at, ActorAccountId = me, Action = action, Resource = "holds", Status = status, PreviousHash = previous, Hash = hash });
            previous = hash;
        }
        Expect(StaffAccessPolicy.ChainIntact(entries), "an untouched log is intact");
        Expect(StaffAccessPolicy.ChainIntact(entries.Skip(1).ToList()), "a later window checks from its first entry");
        Expect(!StaffAccessPolicy.ChainIntact([entries[0], entries[2]]), "a removed entry shows");
        entries[1].Action = "view";
        Expect(!StaffAccessPolicy.ChainIntact(entries), "an edited entry shows");

        Console.WriteLine($"PASS: staff access controls ({checks}).");
    }
}
