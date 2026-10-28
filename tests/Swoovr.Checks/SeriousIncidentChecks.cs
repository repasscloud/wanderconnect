using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;

/// <summary>
/// The Serious Safety Incident pathway (policy §9, owner Q77, D190): admin queue, general outcomes only once closed,
/// police answers and limits, the shared safety information, and the reporter's report line.
/// </summary>
static class SeriousIncidentChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Serious incident check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        Expect(ReportPolicy.QueueFor(false, serious: true) == CaseQueue.Admin, "serious incidents go to the admin queue");
        Expect(ReportPolicy.QueueFor(true, serious: false) == CaseQueue.Admin, "staff-affiliated subjects still go to admins");
        Expect(ReportPolicy.QueueFor(false, serious: false) == CaseQueue.Support, "routine reports go to support");
        foreach (var status in new[] { CaseStatus.Open, CaseStatus.UnderReview, CaseStatus.Reviewed })
            Expect(ReportPolicy.Outcome(status, true) is null && ReportPolicy.Outcome(status, false) is null, $"no outcome while {status}");
        Expect(ReportPolicy.Outcome(CaseStatus.Closed, true) == "actionTaken", "closed with an action: action taken");
        Expect(ReportPolicy.Outcome(CaseStatus.Closed, false) == "noBreachFound", "closed without one: no breach found");
        Expect(ReportPolicy.PoliceAnswers.SequenceEqual(["contacted", "will-contact", "not-now"]), "three police answers");
        Expect(ReportPolicy.PoliceReferenceMax == 100 && ReportPolicy.ChosenMessagesMax == 20, "police reference and chosen-message limits");
        Expect(!ReportPolicy.RateLimited(ReportPriority.Urgent, 1000), "serious (urgent) reports are never rate limited");

        // Shared wording (web and app).
        // #103 (D204): the Australian contacts (Q77, D190) are now the seeded AU pack, edited as data.
        Expect(!MemberText.ImmediateDanger.Any(char.IsDigit), "the danger line names no number (it depends on the country)");
        var au = Swoovr.Infrastructure.Persistence.SafetySeed.Resources.Where(r => r.CountryCode == "AU").OrderBy(r => r.DisplayOrder).ToList();
        Expect(au[0].Phone == "000" && au[0].Category == SafetyResourceCategory.Emergency, "emergency first");
        Expect(au.Any(r => r.Phone == "1800737732") && au.Any(r => r.Phone == "131444") && au.Any(r => r.Phone == "131114"), "1800RESPECT, the Police Assistance Line and Lifeline");
        Expect(au.Any(r => r.WebsiteUrl == "https://www.esafety.gov.au/report") && au.Any(r => r.WebsiteUrl == "https://www.accce.gov.au/report"), "eSafety and the ACCCE");
        Expect(au.All(r => r.WebsiteUrl is null || r.WebsiteUrl.StartsWith("https://")), "links are https");
        Expect(MemberText.SeriousPromise.Contains("won't tell the other person") && MemberText.SeriousPromise.Contains("don't contact police for you"), "what Swoovr does and doesn't do");
        var report = new MyReportView(Guid.NewGuid(), "conversation", "stalking", "closed", now, "actionTaken", true);
        Expect(MemberText.ReportLine(report).Contains("serious incident") && MemberText.ReportLine(report).Contains("We took action"), "the report line shows a general outcome");
        Expect(!MemberText.ReportLine(report).Contains("suspen") && !MemberText.ReportLine(report).Contains("terminat"), "never which action");
        Expect(MemberText.ReportLine(report with { Status = "underReview", Outcome = null }).EndsWith("Under review"), "no outcome while under review");
        Expect(MemberText.ReportLine(report with { Outcome = "noBreachFound" }).Contains("didn't find a breach"), "no breach found");
        Expect(MemberText.SafetyState("deleted") == "Account deleted" && MemberText.SafetyState("blocked") == "Blocked", "people states");

        Console.WriteLine($"PASS: serious safety incidents ({checks}).");
    }
}
