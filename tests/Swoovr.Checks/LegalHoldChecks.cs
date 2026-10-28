using Swoovr.Domain;

/// <summary>
/// Legal holds and evidence (policy §3, §13; owner Q76, D189): who places and manages holds, what they cover, review
/// dates, release, sealed evidence, the package hash and the hash-chained custody log, disclosures and one-time exports.
/// </summary>
static class LegalHoldChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Legal hold check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);
        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var caseId = Guid.NewGuid();

        // Who places what.
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, "Police request", 1, null, null, null, null, now) == HoldRefusal.None, "legal places a hold without a case");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Admin, "Serious report", 1, null, null, null, null, now) == HoldRefusal.CaseRequired, "an admin needs a case");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Admin, "Serious report", 2, caseId, null, null, null, now) == HoldRefusal.None, "an admin may open one from a case");
        foreach (var role in new[] { AccountRole.Support, AccountRole.Member, AccountRole.VenuePartner, AccountRole.Unknown })
            Expect(LegalHoldPolicy.CheckPlace(role, "x", 1, caseId, null, null, null, now) == HoldRefusal.NotPermitted, $"{role} can't place a hold");
        Expect(LegalHoldPolicy.StatusFor(AccountRole.Legal) == LegalHoldStatus.Active && LegalHoldPolicy.StatusFor(AccountRole.Admin) == LegalHoldStatus.Provisional,
            "legal's holds are active, an admin's provisional");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, " ", 1, null, null, null, null, now) == HoldRefusal.ReasonRequired, "a reason is required");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, new string('x', 1001), 1, null, null, null, null, now) == HoldRefusal.ReasonRequired, "reasons are bounded");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, "x", 0, null, null, null, null, now) == HoldRefusal.ScopeRequired, "at least one account");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, "x", 21, null, null, null, null, now) == HoldRefusal.ScopeRequired, "narrowly scoped: at most 20 accounts");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, "x", 1, null, now, now.AddDays(-1), null, now) == HoldRefusal.BadDates, "the period runs forwards");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, "x", 1, null, null, null, now.AddDays(366), now) == HoldRefusal.ReviewTooFar, "a review within a year");
        Expect(LegalHoldPolicy.CheckPlace(AccountRole.Legal, "x", 1, null, null, null, now.AddDays(-1), now) == HoldRefusal.ReviewTooFar, "a review in the future");

        // Review dates.
        Expect(LegalHoldPolicy.ReviewFor(LegalHoldStatus.Active, null, now) == now.AddDays(90), "active holds default to a 90-day review");
        Expect(LegalHoldPolicy.ReviewFor(LegalHoldStatus.Active, now.AddDays(200), now) == now.AddDays(200), "legal may choose the review date");
        Expect(LegalHoldPolicy.ReviewFor(LegalHoldStatus.Provisional, now.AddDays(200), now) == now.AddDays(30), "a provisional hold is reviewed within 30 days");
        var hold = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Provisional, Reason = "r", PlacedAt = now, ReviewDue = now.AddDays(30) };
        Expect(!LegalHoldPolicy.Overdue(hold, now) && LegalHoldPolicy.Overdue(hold, now.AddDays(31)), "overdue once the review date passes");
        Expect(LegalHoldPolicy.Live(hold), "a provisional hold protects like an active one");

        // Managing is legal's alone.
        Expect(LegalHoldPolicy.CheckManage(AccountRole.Legal, hold) == HoldRefusal.None, "legal manages");
        Expect(LegalHoldPolicy.CheckManage(AccountRole.Admin, hold) == HoldRefusal.NotPermitted, "an admin can't confirm, capture, release or export");
        Expect(LegalHoldPolicy.CheckManage(AccountRole.Support, hold) == HoldRefusal.NotPermitted, "support can't either");
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Legal, hold, "", c, 0, false) == HoldRefusal.ReasonRequired, "releasing needs a reason");
        Expect(LegalHoldPolicy.CheckReview(AccountRole.Legal, hold, now.AddDays(400), now) == HoldRefusal.ReviewTooFar, "an extension stays within a year");
        var released = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Released };
        Expect(!LegalHoldPolicy.Live(released) && LegalHoldPolicy.CheckManage(AccountRole.Legal, released) == HoldRefusal.Released, "a released hold is closed");

        // What a hold covers.
        var scoped = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active, From = now.AddDays(-10), Until = now.AddDays(-1) };
        var conversation = Guid.NewGuid();
        var scopes = new List<LegalHoldScope>
        {
            new() { HoldId = scoped.Id, Kind = HoldScopeKind.Account, TargetId = a },
            new() { HoldId = scoped.Id, Kind = HoldScopeKind.Conversation, TargetId = conversation },
            new() { HoldId = Guid.NewGuid(), Kind = HoldScopeKind.Account, TargetId = b },
        };
        Expect(LegalHoldPolicy.Covers(scoped, scopes, a, null, now.AddDays(-5)), "a named account's records in the period");
        Expect(!LegalHoldPolicy.Covers(scoped, scopes, a, null, now.AddDays(-20)), "not before the period");
        Expect(!LegalHoldPolicy.Covers(scoped, scopes, a, null, now), "not after the period");
        Expect(!LegalHoldPolicy.Covers(scoped, scopes, b, null, now.AddDays(-5)), "another hold's account isn't this hold's");
        Expect(!LegalHoldPolicy.Covers(scoped, scopes, c, null, now.AddDays(-5)), "an unnamed account isn't covered");
        Expect(LegalHoldPolicy.Covers(scoped, scopes, a, conversation, now.AddDays(-5)), "the named conversation is covered");
        Expect(!LegalHoldPolicy.Covers(scoped, scopes, a, Guid.NewGuid(), now.AddDays(-5)), "other conversations aren't, when conversations are named");
        Expect(!LegalHoldPolicy.Covers(released, [new() { HoldId = released.Id, Kind = HoldScopeKind.Account, TargetId = a }], a, null, now), "a released hold covers nothing");
        var open = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active };
        Expect(LegalHoldPolicy.Covers(open, [new() { HoldId = open.Id, Kind = HoldScopeKind.Account, TargetId = a }], a, Guid.NewGuid(), now.AddYears(-3)),
            "with no period and no conversations, every conversation and date");

        // Sealed evidence: encrypted, hashed, tamper-evident.
        var key = new byte[32]; key[3] = 7;
        var json = """{"id":"m1","body":"fictional threat text"}""";
        var sealedItem = EvidenceSeal.Seal(1, key, json);
        Expect(!System.Text.Encoding.UTF8.GetString(sealedItem).Contains("fictional threat"), "evidence is never stored in clear");
        Expect(EvidenceSeal.Open(new Dictionary<byte, byte[]> { [1] = key }, sealedItem) == json, "opens with the right key");
        var altered = (byte[])sealedItem.Clone(); altered[^1] ^= 1;
        var tampered = false;
        try { EvidenceSeal.Open(new Dictionary<byte, byte[]> { [1] = key }, altered); } catch (System.Security.Cryptography.CryptographicException) { tampered = true; }
        Expect(tampered, "a changed item fails authentication");
        Expect(Convert.ToHexStringLower(EvidenceSeal.Hash(json)) == Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json))),
            "the item hash is SHA-256 of the preserved JSON");
        var (h1, h2) = (EvidenceSeal.Hash("one"), EvidenceSeal.Hash("two"));
        Expect(!EvidenceSeal.PackageHash([h1, h2]).SequenceEqual(EvidenceSeal.PackageHash([h2, h1])), "the package hash depends on item order");
        Expect(!EvidenceSeal.PackageHash([h1, h2]).SequenceEqual(EvidenceSeal.PackageHash([h1, EvidenceSeal.Hash("Two")])), "one changed item changes the package hash");

        // The custody chain.
        var chain = new List<CustodyEvent>();
        byte[] previous = [];
        foreach (var (action, at) in new[] { ("placed", now), ("captured", now.AddMinutes(1)), ("exported", now.AddMinutes(2)) })
        {
            var hash = EvidenceSeal.Chain(previous, hold.Id, at, a, action, null);
            chain.Add(new CustodyEvent { Sequence = chain.Count + 1, HoldId = hold.Id, At = at, ActorAccountId = a, Action = action, PreviousHash = previous, Hash = hash });
            previous = hash;
        }
        Expect(EvidenceSeal.ChainIntact(chain), "an untouched custody log is intact");
        Expect(!EvidenceSeal.ChainIntact([chain[0], chain[2]]), "a removed entry breaks the chain");
        var edited = chain.Select(e => new CustodyEvent { Sequence = e.Sequence, HoldId = e.HoldId, At = e.At, ActorAccountId = e.ActorAccountId,
            Action = e.Action == "exported" ? "viewed" : e.Action, PreviousHash = e.PreviousHash, Hash = e.Hash }).ToList();
        Expect(!EvidenceSeal.ChainIntact(edited), "an edited entry breaks the chain");

        // Disclosures and exports.
        Expect(LegalHoldPolicy.ValidDisclosure("SA Police", "Fictional Officer", "REF-1", "warrant", "Called the station switchboard"), "a complete disclosure");
        Expect(!LegalHoldPolicy.ValidDisclosure("SA Police", "Fictional Officer", "REF-1", "because", "Called"), "only listed legal bases");
        Expect(!LegalHoldPolicy.ValidDisclosure("SA Police", "", "REF-1", "warrant", "Called"), "every field is needed");
        var active = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active };
        var export = new EvidenceExport { Id = Guid.NewGuid(), HoldId = active.Id, CreatedAt = now, ExpiresAt = now + LegalHoldPolicy.ExportLifetime };
        Expect(LegalHoldPolicy.ExportUsable(export, active, now.AddMinutes(14)), "usable within 15 minutes");
        Expect(!LegalHoldPolicy.ExportUsable(export, active, now.AddMinutes(16)), "expired after 15 minutes");
        Expect(!LegalHoldPolicy.ExportUsable(export, released, now), "not once the hold is released");
        var retained = new LegalHold { Id = active.Id, Status = LegalHoldStatus.Released };
        Expect(LegalHoldPolicy.ExportUsable(export, retained, now, retainedAccess: true), "server-confirmed retained evidence remains accessible to authorised legal export");
        Expect(!LegalHoldPolicy.ExportUsable(export, retained, now.AddMinutes(16), retainedAccess: true), "retention never extends a download ticket");
        Expect(!LegalHoldPolicy.ExportUsable(export, released, now, retainedAccess: true), "retention never permits a mismatched hold");
        export.DownloadedAt = now;
        Expect(!LegalHoldPolicy.ExportUsable(export, active, now), "used once");
        Expect(!LegalHoldPolicy.ExportUsable(export, retained, now, retainedAccess: true), "retained evidence never revives a used ticket");
        Expect(InvitationPolicy.RealmFor(AccountRole.Legal) == AccountRealm.Staff, "legal is a staff-realm role");

        Console.WriteLine($"PASS: legal holds and evidence ({checks}).");
    }
}
