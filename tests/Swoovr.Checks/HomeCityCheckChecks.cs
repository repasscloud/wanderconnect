using Microsoft.Extensions.Configuration;
using Swoovr.Api.Travel;
using Swoovr.Domain;

/// <summary>
/// D349, #281: the scheduled Home City check's rules. Daily outcomes (25 km vicinity, trip days excluded, precedence),
/// the four parts, skip/suggest thresholds, the last-7-days home rule, suppression (Don't suggest before, open item,
/// 90-day cooldown), member visibility of system items, the note's content, queue access and retention.
/// </summary>
static class HomeCityCheckChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Home City check failed: " + what); checks++; }
        var o = new HomeCityCheckOptions();
        var start = new DateOnly(2026, 10, 1);
        var end = HomeCityCheckPolicy.PeriodEnd(start, o);
        HomeCityDay D(int dayOfPeriod, HomeCityDayOutcome outcome) => new() { Day = start.AddDays(dayOfPeriod - 1), Outcome = outcome };
        var away = HomeCityDayOutcome.Away; var home = HomeCityDayOutcome.Home; var trip = HomeCityDayOutcome.Excluded;

        // Day outcomes: the 25 km vicinity (option B), trip days excluded, and precedence on one day.
        Expect(o.VicinityKm == 25 && o.PeriodDays == 30 && o.MinimumDays == 12 && o.AwayShare == 0.75 && o.MinimumAwayParts == 3 && o.QuietHomeDays == 7 && o.CooldownDays == 90
            && o.DayRetentionDays == 35 && o.ResultRetentionMonths == 12, "D349's starting values");
        Expect(HomeCityCheckPolicy.Outcome(24.9, false, o) == home && HomeCityCheckPolicy.Outcome(25, false, o) == home && HomeCityCheckPolicy.Outcome(25.1, false, o) == away, "25 km vicinity");
        Expect(HomeCityCheckPolicy.Outcome(3, true, o) == trip && HomeCityCheckPolicy.Outcome(900, true, o) == trip, "a checked-in trip day is excluded wherever the reading is");
        Expect(HomeCityCheckPolicy.Combine(null, away) == away && HomeCityCheckPolicy.Combine(away, home) == home && HomeCityCheckPolicy.Combine(home, away) == home, "any home reading makes it a home day");
        Expect(HomeCityCheckPolicy.Combine(trip, home) == trip && HomeCityCheckPolicy.Combine(home, trip) == trip, "a trip day stays excluded");
        Expect(end == new DateOnly(2026, 10, 30), "a 30-day period");
        Expect(HomeCityCheckPolicy.Part(start, start) == 0 && HomeCityCheckPolicy.Part(start, start.AddDays(6)) == 0 && HomeCityCheckPolicy.Part(start, start.AddDays(7)) == 1
            && HomeCityCheckPolicy.Part(start, start.AddDays(20)) == 2 && HomeCityCheckPolicy.Part(start, start.AddDays(21)) == 3 && HomeCityCheckPolicy.Part(start, end) == 3, "parts: days 1–7, 8–14, 15–21, 22–30");

        // A suggestion: 15 of 17 days away, in every part, the two home days early.
        var spread = new[] { 1, 3, 5, 8, 10, 12, 15, 17, 19, 22, 24, 25, 26, 27, 28, 29, 30 };
        List<HomeCityDay> Days(Func<int, HomeCityDayOutcome> outcome) => spread.Select(d => D(d, outcome(d))).ToList();
        var suggested = HomeCityCheckPolicy.Evaluate(start, Days(d => d is 1 or 8 ? home : away), o);
        Expect(suggested is { Result: HomeCityCheckResult.Suggested, LocatedDays: 17, AwayDays: 15, HomeDays: 2, PartsCovered: 4, AwayParts: 4, ExcludedDays: 0 }, "15 of 17 away suggests");
        Expect(HomeCityCheckPolicy.Observed(15, 17) == "away on 15 of 17 days with readings" && HomeCityCheckPolicy.Observed(1, 1) == "away on 1 of 1 day with readings", "observed-days wording");

        // Sparse readings skip.
        Expect(HomeCityCheckPolicy.Evaluate(start, spread.Take(11).Select(d => D(d, away)), o).Result == HomeCityCheckResult.Skipped, "11 located days skip");
        Expect(HomeCityCheckPolicy.Evaluate(start, Enumerable.Range(1, 21).Select(d => D(d, away)), o) is { Result: HomeCityCheckResult.Skipped, PartsCovered: 3 }, "21 days but nothing in days 22–30 skip");
        Expect(HomeCityCheckPolicy.Evaluate(start, [], o).Result == HomeCityCheckResult.Skipped, "no readings skip");
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(_ => away).Select(d => new HomeCityDay { Day = d.Day.AddDays(40), Outcome = away }), o).Result == HomeCityCheckResult.Skipped, "days outside the period don't count");

        // A home day in the last 7 days prevents a suggestion.
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(d => d == 24 ? home : away), o).Result == HomeCityCheckResult.NoSuggestion, "home on day 24 (inside the last 7)");
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(d => d == 22 ? home : away), o).Result == HomeCityCheckResult.Suggested, "home on day 22 (before the last 7) still suggests");

        // Trip days are excluded from the calculation and the evidence count.
        var withTrip = HomeCityCheckPolicy.Evaluate(start, Days(d => d is 24 or 25 or 26 ? trip : d is 1 or 8 ? home : away), o);
        Expect(withTrip is { LocatedDays: 14, AwayDays: 12, ExcludedDays: 3, Result: HomeCityCheckResult.Suggested }, "three trip days counted for neither side");
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(d => d >= 15 ? trip : away), o) is { Result: HomeCityCheckResult.Skipped, LocatedDays: 6, ExcludedDays: 11 }, "trip days can't make up the evidence");
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(d => d >= 22 ? trip : home), o).Result == HomeCityCheckResult.Skipped, "a trip covering the last part skips (no located days there)");

        // Share and spread.
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(d => d is 1 or 3 or 5 or 8 or 10 ? home : away), o) is { AwayDays: 12, Result: HomeCityCheckResult.NoSuggestion }, "12 of 17 (71%) isn't enough");
        Expect(HomeCityCheckPolicy.Evaluate(start, Enumerable.Range(1, 30).Select(d => D(d, d <= 14 ? home : away)), o).Result == HomeCityCheckResult.NoSuggestion, "half and half");
        var twoParts = new[] { D(1, home), D(8, home) }.Concat(Enumerable.Range(15, 16).Select(d => D(d, away))).ToList();
        Expect(HomeCityCheckPolicy.Evaluate(start, twoParts, o) is { AwayParts: 2, Result: HomeCityCheckResult.NoSuggestion }, "away days in only 2 of 4 parts");
        Expect(HomeCityCheckPolicy.Evaluate(start, Days(_ => away), o with { MinimumDays = 18 }).Result == HomeCityCheckResult.Skipped, "thresholds are configurable");

        // Suppression: Don't suggest before, one open item, 90 days after closure.
        var now = new DateTimeOffset(2026, 11, 2, 0, 0, 0, TimeSpan.Zero); var today = new DateOnly(2026, 11, 2);
        Expect(HomeCityCheckPolicy.Suppress(today, null, false, null, now, o) == HomeCityCheckSuppression.None, "nothing in the way");
        Expect(HomeCityCheckPolicy.Suppress(today, today.AddDays(1), false, null, now, o) == HomeCityCheckSuppression.DontSuggestBefore, "Don't suggest before tomorrow");
        Expect(HomeCityCheckPolicy.Suppress(today, today, false, null, now, o) == HomeCityCheckSuppression.None, "Don't suggest before today allows today");
        Expect(HomeCityCheckPolicy.Suppress(today, null, true, null, now, o) == HomeCityCheckSuppression.OpenItem, "an open item");
        Expect(HomeCityCheckPolicy.Suppress(today, null, false, now.AddDays(-89), now, o) == HomeCityCheckSuppression.Cooldown, "closed 89 days ago");
        Expect(HomeCityCheckPolicy.Suppress(today, null, false, now.AddDays(-91), now, o) == HomeCityCheckSuppression.None, "closed 91 days ago");

        // Late checks: a period is evaluated only while all its days are still retained (35 days).
        var evaluatedAt = new DateTimeOffset(start.AddDays(35).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        Expect(HomeCityCheckPolicy.RetainedFrom(evaluatedAt, o) == start, "the retained window starts 35 days back");
        Expect(HomeCityCheckPolicy.EvidenceComplete(start, evaluatedAt, o), "checked within the window");
        Expect(!HomeCityCheckPolicy.EvidenceComplete(start, evaluatedAt.AddDays(1), o), "a day later its first day may be purged");
        Expect(HomeCityCheckPolicy.EvidenceComplete(start, new DateTimeOffset(end.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), o), "the normal check (the day after the period) is well inside");
        // Codex's example: home days 1–7 and away days 8–22 (15 of 22) must not become 15 of 17 because days 1–5 were purged.
        var full = Enumerable.Range(1, 22).Select(d => D(d, d <= 7 ? home : away)).Concat(Enumerable.Range(23, 8).Select(d => D(d, away))).ToList();
        Expect(HomeCityCheckPolicy.Evaluate(start, full.Where(d => d.Day.Day <= 22), o).Result == HomeCityCheckResult.NoSuggestion, "15 of 22 is no suggestion");

        // The member doesn't see a system item until Support has written to them.
        SupportArticle A(bool @internal, string sender, string type = "note") => new(1, 7, "x", "text/plain", @internal, sender, now, []) { Type = type };
        Expect(!HomeCityCheckPolicy.MemberVisible(TicketingPolicy.SystemSource, [A(true, "Agent")]), "only the internal note: hidden");
        Expect(!HomeCityCheckPolicy.MemberVisible(TicketingPolicy.SystemSource, [A(true, "Agent"), A(true, "Agent")]), "more internal notes: still hidden");
        Expect(!HomeCityCheckPolicy.MemberVisible(TicketingPolicy.SystemSource, [A(true, "Agent"), A(false, "Customer", "email")]), "an emailed customer message doesn't open it");
        Expect(HomeCityCheckPolicy.MemberVisible(TicketingPolicy.SystemSource, [A(true, "Agent"), A(false, "Agent")]), "Support's reply opens it");
        Expect(HomeCityCheckPolicy.MemberVisible("member", []) && HomeCityCheckPolicy.MemberVisible(null, []), "other items are unaffected");

        // The note: account link, public id, Home City, period and counts; nothing positional, no per-day list.
        var check = new HomeCityCheck { PeriodStart = start, PeriodEnd = end, LocatedDays = 17, AwayDays = 15, PartsCovered = 4, AwayParts = 4, ExcludedDays = 3 };
        var publicId = Guid.Parse("6f1c1f4e-2b55-4f0e-9a44-0d5a7d7c0f11");
        var note = HomeCityCheckPolicy.Note("https://admin.example.test/accounts/11111111-1111-1111-1111-111111111111", publicId, "Adelaide, South Australia, Australia", check);
        Expect(note.Contains("away on 15 of 17 days with readings") && note.Contains(publicId.ToString("D")) && note.Contains("Adelaide, South Australia, Australia")
            && note.Contains("1 Oct 2026") && note.Contains("30 Oct 2026") && note.Contains("/accounts/11111111-1111-1111-1111-111111111111"), "the note carries only the agreed facts");
        Expect(!System.Text.RegularExpressions.Regex.IsMatch(note, @"-?\d+\.\d{2,}") && !note.Contains("km") && !note.Contains("latitude") && !note.Contains("longitude"), "no coordinates or distances in the note");
        Expect(!System.Text.RegularExpressions.Regex.IsMatch(note, @"\b(2|3|4|5|6|7|8|9|1[0-9]|2[0-9]) Oct\b"), "no per-day list");
        Expect(!HomeCityCheckPolicy.Reply.Contains("location") && HomeCityCheckPolicy.Reply.Contains("Me → Profile"), "the reply suggests, and links to Me → Profile");

        // Queue access: Support, Trust & Safety and Admin; worked through Zammad's web sign-in too (not a sensitive queue).
        Expect(TicketingPolicy.Groups.Contains(HomeCityCheckPolicy.Group), "the group is provisioned");
        Expect(TicketingPolicy.CanAccessGroup(AccountRole.Support, HomeCityCheckPolicy.Group) && TicketingPolicy.CanAccessGroup(AccountRole.TrustSafety, HomeCityCheckPolicy.Group)
            && TicketingPolicy.CanAccessGroup(AccountRole.Admin, HomeCityCheckPolicy.Group) && !TicketingPolicy.CanAccessGroup(AccountRole.Legal, HomeCityCheckPolicy.Group), "Support, T&S and Admin only");
        Expect(TicketingPolicy.StaffGrants(new HashSet<AccountRole> { AccountRole.Support }, web: true)[HomeCityCheckPolicy.Group] == "full", "Support works it from Zammad too");
        Expect(!TicketingPolicy.Sensitive.Contains(HomeCityCheckPolicy.Group), "not a sensitive queue");

        // Retention: closed + 6 months (D337's retention step).
        Expect(HelpdeskRetentionPolicy.CategoryForGroup(HomeCityCheckPolicy.Group) == HelpdeskRetentionCategory.HomeCityCheck, "the group's category");
        var closed = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        Expect(HelpdeskRetentionPolicy.RetainUntil(closed, HelpdeskRetentionCategory.HomeCityCheck) == closed.AddMonths(6), "kept 6 months after closure");
        Expect(HelpdeskRetentionPolicy.RetainUntil(null, HelpdeskRetentionCategory.HomeCityCheck) is null, "open items are kept");

        // Provisioning: the group, the identity and the same friendly reply in Zammad (macro and text module).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "containers/zammad/provision.rb"))) dir = dir.Parent;
        if (dir is not null)
        {
            var provision = File.ReadAllText(Path.Combine(dir.FullName, "containers/zammad/provision.rb"));
            Expect(provision.Contains("'" + HomeCityCheckPolicy.Group + "'") && provision.Contains("'" + HomeCityCheckPolicy.MacroName + "'") && provision.Contains("swoovr-home-city-check")
                && provision.Contains("\"" + HomeCityCheckPolicy.Reply + "\""), "provision.rb carries the group, macro, identity and reply");
        }

        // Settings: configurable, with D349's defaults, and refused when nonsensical.
        IConfiguration Config(params (string, string)[] values) => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Item1, v.Item2))).Build();
        Expect(HomeCityCheckSettings.Read(Config()) == new HomeCityCheckOptions(), "defaults without configuration");
        Expect(HomeCityCheckSettings.Read(Config(("Location:HomeCityCheck:VicinityKm", "40"))).VicinityKm == 40, "vicinity configurable");
        foreach (var (key, value) in new[] { ("VicinityKm", "0"), ("AwayShare", "1.5"), ("MinimumAwayParts", "5"), ("DayRetentionDays", "20"), ("AdminBaseUrl", "javascript:alert(1)") })
        {
            var refused = false;
            try { HomeCityCheckSettings.Read(Config(("Location:HomeCityCheck:" + key, value))); } catch (InvalidOperationException) { refused = true; }
            Expect(refused, "refuses " + key + "=" + value);
        }

        Console.WriteLine($"PASS: scheduled Home City check rules ({checks}).");
    }
}
