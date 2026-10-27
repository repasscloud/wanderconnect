using Swoovr.Domain;

/// <summary>
/// Law-enforcement case management (police-contact procedure §2–§14, D195): deadline timers in business days, independent
/// verification, separated approvals and break-glass review, notification decisions, temporary police holds, independent
/// release with overlapping holds, the closure checklist, seven-year retention, suppressed transparency counts, quarters
/// and redacted disclosure copies.
/// </summary>
static class LawEnforcementChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Law-enforcement check failed: " + what); checks++; }
        // Monday 26 October 2026, 10:00 in Adelaide (UTC+10:30 in daylight saving).
        var monday = new DateTimeOffset(2026, 10, 25, 23, 30, 0, TimeSpan.Zero);
        var friday = new DateTimeOffset(2026, 10, 30, 6, 0, 0, TimeSpan.Zero);
        var (owner, other, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Timers (§2).
        Expect(LawEnforcementPolicy.ResponseDue(LeUrgency.Critical, monday, null) == monday.AddHours(1), "critical: one hour");
        Expect(LawEnforcementPolicy.ResponseDue(LeUrgency.Urgent, monday, null) == monday.AddHours(4), "urgent: four hours");
        Expect(LawEnforcementPolicy.ResponseDue(LeUrgency.Standard, monday, null) == monday.AddDays(2), "standard: two business days midweek");
        Expect(LawEnforcementPolicy.ResponseDue(LeUrgency.Standard, friday, null) == friday.AddDays(4), "standard: the weekend doesn't count");
        Expect(LawEnforcementPolicy.ResponseDue(LeUrgency.Standard, monday, monday.AddHours(6)) == monday.AddHours(6), "an earlier statutory deadline overrides");
        Expect(LawEnforcementPolicy.ResponseDue(LeUrgency.Critical, monday, monday.AddDays(5)) == monday.AddHours(1), "a later statutory deadline doesn't relax the target");
        Expect(LawEnforcementPolicy.AddBusinessDays(friday, 5) == friday.AddDays(7), "five business days from a Friday is the next Friday");
        var item = new LeCase { Id = Guid.NewGuid(), OwnerId = owner, Urgency = LeUrgency.Urgent, ReceivedAt = monday };
        Expect(LawEnforcementPolicy.Timer(item, monday.AddHours(1)) == (monday.AddHours(4), "response-due"), "response due");
        Expect(LawEnforcementPolicy.Timer(item, monday.AddHours(5)).State == "response-overdue", "response overdue");
        item.FirstResponseAt = monday.AddHours(2);
        Expect(LawEnforcementPolicy.Timer(item, monday.AddHours(3)) == (monday.AddHours(2).AddDays(7), "update-due"), "then an update every five business days");
        item.LastUpdateAt = monday.AddDays(8);
        Expect(LawEnforcementPolicy.Timer(item, monday.AddDays(8)).Due == LawEnforcementPolicy.AddBusinessDays(monday.AddDays(8), 5), "from the latest update");
        Expect(LawEnforcementPolicy.Timer(new LeCase { Status = LeCaseStatus.Closed }, monday).State == "closed", "no timer once closed");
        Expect(LawEnforcementPolicy.Reference(monday, Guid.Parse("abcdef12-0000-0000-0000-000000000000")) == "LE-2026-ABCDEF", "references");

        // Verification (§3.2): an independent route, never the email alone.
        Expect(LawEnforcementPolicy.CheckVerify("agency-switchboard", "Called the published switchboard; officer confirmed") == LeRefusal.None, "switchboard callback");
        Expect(LawEnforcementPolicy.CheckVerify("email", "It came from a .gov.au address") == LeRefusal.Invalid, "a government email alone is insufficient");
        Expect(LawEnforcementPolicy.CheckVerify("official-directory", " ") == LeRefusal.Invalid, "evidence of the check is required");

        // Deciding requests (§3, §5).
        var request = new LeRequest { Id = Guid.NewGuid(), Kind = "disclosure", Status = "open" };
        Expect(LawEnforcementPolicy.CheckDecide(item, request, other, "disclosed", "Warrant checked", false) == LeRefusal.NotVerified, "disclosure needs verification");
        item.VerifiedAt = monday;
        Expect(LawEnforcementPolicy.CheckDecide(item, request, other, "disclosed", "Warrant checked", false) == LeRefusal.NotAssessed, "and a legal assessment");
        (item.AssessedAt, item.LegalInstrument) = (monday, "app-6.2e");
        Expect(LawEnforcementPolicy.CheckDecide(item, request, other, "disclosed", "Basis noted", false) == LeRefusal.App65Required, "APP 6.2(e) needs the APP 6.5 note");
        Expect(LawEnforcementPolicy.CheckDecide(item, request, other, "refused", "Overbroad", false) == LeRefusal.None, "refusing needs no note");
        item.App65Note = "Reasonable belief: fictional facts; extent: messages 1–3; recipient: fictional officer; date.";
        Expect(LawEnforcementPolicy.CheckDecide(item, request, other, "disclosed", "Basis noted", false) == LeRefusal.None, "a separate approver discloses");
        Expect(LawEnforcementPolicy.CheckDecide(item, request, owner, "disclosed", "Basis noted", false) == LeRefusal.NotSeparated, "the collector doesn't approve their own case");
        Expect(LawEnforcementPolicy.CheckDecide(item, request, owner, "disclosed", "On call", true) == LeRefusal.Invalid, "break-glass only for emergencies");
        var emergency = new LeRequest { Id = Guid.NewGuid(), Kind = "emergency-disclosure", Status = "open" };
        Expect(LawEnforcementPolicy.CheckDecide(item, emergency, owner, "disclosed", "Imminent danger", true) == LeRefusal.None, "on-call break-glass in an emergency");
        Expect(LawEnforcementPolicy.CheckDecide(item, request, other, "shared", "x", false) == LeRefusal.Invalid, "only listed outcomes");
        Expect(LawEnforcementPolicy.CheckDecide(item, new LeRequest { Status = "refused" }, other, "disclosed", "x", false) == LeRefusal.AlreadyDecided, "decided once");
        (emergency.Status, emergency.DecidedBy, emergency.BreakGlass) = ("disclosed", owner, true);
        Expect(LawEnforcementPolicy.CheckRetrospective(emergency, owner, "Reviewed") == LeRefusal.NotSeparated, "the approver can't review themselves");
        Expect(LawEnforcementPolicy.CheckRetrospective(emergency, other, "Reviewed: proportionate") == LeRefusal.None, "another officer reviews");
        Expect(LawEnforcementPolicy.CheckRetrospective(request, other, "x") == LeRefusal.Invalid, "only break-glass approvals are reviewed this way");

        // Notification (§10).
        Expect(LawEnforcementPolicy.CheckNotification("notified", "Told by email", null, monday) == LeRefusal.None, "notified");
        Expect(LawEnforcementPolicy.CheckNotification("withhold", "Order prohibits notice", null, monday) == LeRefusal.ReviewTooFar, "withholding needs a reconsideration date");
        Expect(LawEnforcementPolicy.CheckNotification("delay", "Investigation", monday.AddDays(30), monday) == LeRefusal.None, "a delay with a date");
        Expect(LawEnforcementPolicy.CheckNotification("delay", "Investigation", monday.AddDays(200), monday) == LeRefusal.ReviewTooFar, "reconsidered within six months");
        Expect(LawEnforcementPolicy.CheckNotification("ignore", "x", monday.AddDays(3), monday) == LeRefusal.Invalid, "only listed decisions");

        // Exclusion (§5).
        var people = new List<LeCasePerson> { new() { Kind = "subject", AccountId = third }, new() { Kind = "implicated-staff", AccountId = other } };
        Expect(LawEnforcementPolicy.Excluded(people, other) && !LawEnforcementPolicy.Excluded(people, owner), "implicated staff are excluded");
        Expect(!LawEnforcementPolicy.Excluded(people, third), "a subject isn't staff");

        // Temporary police preservation holds and independent release (§7).
        var unverified = new LeCase { Status = LeCaseStatus.Open };
        Expect(LawEnforcementPolicy.CheckTemporaryHold(unverified, null, monday) == LeRefusal.NotVerified, "only for a verified request");
        Expect(LawEnforcementPolicy.CheckTemporaryHold(item, monday.AddDays(31), monday) == LeRefusal.ReviewTooFar, "up to 30 days");
        Expect(LawEnforcementPolicy.CheckTemporaryHold(item, monday.AddDays(30), monday) == LeRefusal.None, "30 days");
        var temporary = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active, Kind = LawEnforcementPolicy.PolicePreservation, PlacedBy = owner, ConfirmedBy = owner, ReviewDue = monday.AddDays(30) };
        Expect(LegalHoldPolicy.CheckReview(AccountRole.Legal, temporary, monday.AddDays(31), monday) == HoldRefusal.ReviewTooFar, "an extension is another 30 days at most");
        Expect(LegalHoldPolicy.Live(temporary) && !LegalHoldPolicy.Overdue(temporary, monday.AddDays(29)), "review isn't release");
        Expect(LegalHoldPolicy.Live(temporary) && LegalHoldPolicy.Overdue(temporary, monday.AddDays(31)), "an overdue temporary hold still protects");
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Legal, temporary, "Done", owner, 0, false) == HoldRefusal.NotIndependent, "the authoriser can't release");
        var confirmedByOther = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active, PlacedBy = null, ConfirmedBy = other };
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Legal, confirmedByOther, "Done", other, 0, false) == HoldRefusal.NotIndependent, "nor the confirmer");
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Legal, temporary, "Done", other, 0, false) == HoldRefusal.None, "an independent officer releases");
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Legal, temporary, "Done", other, 1, false) == HoldRefusal.OverlapsUnchecked, "after checking overlapping holds");
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Legal, temporary, "Done", other, 1, true) == HoldRefusal.None, "checked");
        Expect(LegalHoldPolicy.CheckRelease(AccountRole.Admin, temporary, "Done", third, 0, false) == HoldRefusal.NotPermitted, "admins never release");
        var second = new LegalHold { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active };
        var scopes = new List<LegalHoldScope>
        {
            new() { HoldId = temporary.Id, Kind = HoldScopeKind.Account, TargetId = third },
            new() { HoldId = second.Id, Kind = HoldScopeKind.Account, TargetId = third },
        };
        Expect(LegalHoldPolicy.Overlaps(temporary, [temporary, second], scopes) == 1, "overlaps counted");
        Expect(LegalHoldPolicy.Overlaps(temporary, [temporary, new LegalHold { Id = second.Id, Status = LegalHoldStatus.Released }], scopes) == 0, "released holds don't overlap");

        // Closure (§13).
        var decided = new LeRequest { Status = "disclosed" };
        var notice = new LeNotification { AccountId = third, Decision = "delay" };
        var transfer = new LeTransfer { ReceiptAt = monday };
        bool Closes(IReadOnlyList<LawEnforcementPolicy.ChecklistItem> list) => list.All(i => i.Done);
        Expect(Closes(LawEnforcementPolicy.Closure(item, [decided], people, [notice], [transfer], [temporary], monday)), "everything done");
        Expect(!Closes(LawEnforcementPolicy.Closure(item, [new LeRequest { Status = "open" }], people, [notice], [transfer], [], monday)), "an open request blocks");
        Expect(!Closes(LawEnforcementPolicy.Closure(item, [decided], people, [], [transfer], [], monday)), "a subject without a notification decision blocks");
        Expect(!Closes(LawEnforcementPolicy.Closure(item, [decided], people, [notice], [new LeTransfer()], [], monday)), "a transfer without a receipt blocks");
        Expect(!Closes(LawEnforcementPolicy.Closure(item, [emergency], people, [notice], [], [], monday)), "an unreviewed break-glass approval blocks");
        emergency.RetrospectiveAt = monday;
        Expect(Closes(LawEnforcementPolicy.Closure(item, [emergency], people, [notice], [], [], monday)), "once reviewed it doesn't");
        Expect(!Closes(LawEnforcementPolicy.Closure(item, [decided], people, [notice], [transfer], [temporary], monday.AddDays(40))), "a held hold needs a future review");
        Expect(Closes(LawEnforcementPolicy.Closure(item, [decided], people, [notice], [transfer], [new LegalHold { Status = LegalHoldStatus.Released }], monday)), "released holds don't block");
        Expect(LawEnforcementPolicy.RetainUntil(monday) == monday.AddYears(7), "kept seven years from closure");

        // Transparency and the audit quarter.
        Expect(LawEnforcementPolicy.Suppress(0) == 0 && LawEnforcementPolicy.Suppress(4) is null && LawEnforcementPolicy.Suppress(5) == 5, "counts under five suppressed");
        Expect(LawEnforcementPolicy.Quarter("2026-Q4") == (new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero)), "quarters");
        Expect(LawEnforcementPolicy.Quarter("2026-Q5") is null && LawEnforcementPolicy.Quarter("Q4-2026") is null, "bad quarters refused");

        // Redacted disclosure copies (§6).
        var original = """{"message":{"body":"fictional text","senderAccountId":"x"},"hiddenBy":[]}""";
        var (copy, missing) = LawEnforcementPolicy.Redact(original, ["message.body", "message.nothing"]);
        Expect(copy.Contains("[REDACTED]") && !copy.Contains("fictional text") && copy.Contains("senderAccountId"), "only the named field is redacted");
        Expect(missing.SequenceEqual(["message.nothing"]), "a missing path is reported, never claimed");
        Expect(original.Contains("fictional text"), "the original is untouched");

        Console.WriteLine($"PASS: law-enforcement cases ({checks}).");
    }
}
