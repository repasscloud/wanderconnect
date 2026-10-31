using Swoovr.Domain;

static class ReportOutcomeChecks
{
    public static void Run(Action<string, bool> check)
    {
        var now = DateTimeOffset.Parse("2026-10-30T00:00:00Z");
        var initial = ReportOutcomePolicy.Observe(CaseStatus.Open, null, []);
        var review = ReportOutcomePolicy.Observe(CaseStatus.UnderReview, null, []);
        check("report internal progress alone does not manufacture an outcome", initial.Fingerprint == review.Fingerprint);
        check("legacy absent fingerprint silently baselines without historical spam", !ReportOutcomePolicy.Advance(null, 0, initial).Notify);
        check("unchanged source decision idempotent", !ReportOutcomePolicy.Advance(initial.Fingerprint, 0, initial).Notify);
        var closed = ReportOutcomePolicy.Observe(CaseStatus.Closed, now, []);
        check("closed no action keeps existing noBreachFound policy", closed.GeneralOutcome == "noBreachFound");
        var next = ReportOutcomePolicy.Advance(initial.Fingerprint, 0, closed);
        check("new closure queues one revision", next.Notify && next.Revision == 1);
        var sanction = new ReportOutcomeDecision(Guid.NewGuid(), "sanction", now, State: "Warning");
        var action = ReportOutcomePolicy.Observe(CaseStatus.Closed, now, [sanction]);
        check("sanction retains general actionTaken only", action.GeneralOutcome == "actionTaken");
        var appeal = new ReportOutcomeDecision(Guid.NewGuid(), "appeal", now.AddHours(1), State: "Upheld");
        var appealed = ReportOutcomePolicy.Observe(CaseStatus.Closed, now, [sanction, appeal]);
        check("decided appeal notifies even with same general outcome", action.GeneralOutcome == appealed.GeneralOutcome && action.Fingerprint != appealed.Fingerprint);
        var removal = new ReportOutcomeDecision(Guid.NewGuid(), "removal", now.AddHours(2));
        check("content removal counts as action using existing policy", ReportOutcomePolicy.Observe(CaseStatus.Closed, now, [removal]).GeneralOutcome == "actionTaken");
        check("decision ordering is deterministic", ReportOutcomePolicy.Observe(CaseStatus.Closed, now, [sanction, appeal]).Fingerprint == ReportOutcomePolicy.Observe(CaseStatus.Closed, now, [appeal, sanction]).Fingerprint);
        check("decision clock offsets canonicalised", ReportOutcomePolicy.Observe(CaseStatus.Closed, now.ToOffset(TimeSpan.FromHours(10)), [sanction with { At = now.ToOffset(TimeSpan.FromHours(10)) }]).Fingerprint == action.Fingerprint);
        var back = ReportOutcomePolicy.Advance(closed.Fingerprint, 1, initial);
        check("return to earlier fingerprint gets fresh revision/dedupe", back.Notify && back.Revision == 2 && ReportOutcomePolicy.Dedupe(Guid.Empty, 1) != ReportOutcomePolicy.Dedupe(Guid.Empty, 2));
        check("fingerprint contains no public outcome/private reason text", action.Fingerprint.Length == 64 && !action.Fingerprint.Contains("Warning", StringComparison.Ordinal));
        try { ReportOutcomePolicy.Observe(CaseStatus.Closed, null, []); check("unknown closure provenance refused", false); }
        catch (InvalidOperationException) { check("unknown closure provenance refused", true); }
    }
}
