using System.Globalization;

namespace Swoovr.Domain;

/// <summary>
/// D349: a located day's derived outcome, in the Home City's time zone. The order is the precedence when several readings
/// fall on one day (D354 DEFAULT): a day on an active checked-in trip is excluded whatever else happened, and any reading
/// in the Home City's vicinity makes it a home day, so a day spent partly at home never counts as away.
/// </summary>
public enum HomeCityDayOutcome { Away = 0, Home = 1, Excluded = 2 }

/// <summary>
/// D349, #281: one member's outcome for one local day. Never a coordinate, place, distance or trip: only the day and the
/// outcome. Deleted 35 days after the day (HomeCityCheckOptions.DayRetentionDays).
/// </summary>
public sealed class HomeCityDay
{
    public Guid AccountId { get; set; }
    public DateOnly Day { get; set; }
    public HomeCityDayOutcome Outcome { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>What one scheduled check concluded (D349). Suggested results create (or wait to create) the Zammad item.</summary>
public enum HomeCityCheckResult { Skipped, NoSuggestion, Suggested, Suppressed }

/// <summary>
/// Why a suggestion was not raised: "Don't suggest before", an item already open, the 90-day cooldown after closure, or
/// the Home City changed before the item was created (its counts were measured against the old city).
/// </summary>
public enum HomeCityCheckSuppression { None, DontSuggestBefore, OpenItem, Cooldown, HomeCityChanged }

/// <summary>Delivery of a suggestion's Zammad item. Pending items are retried by the worker with the same operation id.</summary>
public enum HomeCityTicketState { None, Pending, Created, Failed }

/// <summary>
/// D349, #281: one scheduled check's count-only result, kept 12 months. The id is also the Zammad create operation id, so
/// a lost response is reconciled rather than duplicated. No coordinates, places, trip destinations or per-day list.
/// </summary>
public sealed class HomeCityCheck
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateOnly PeriodStart { get; set; }
    public DateOnly PeriodEnd { get; set; }
    /// <summary>Located, non-excluded days (the evidence count).</summary>
    public int LocatedDays { get; set; }
    public int AwayDays { get; set; }
    public int ExcludedDays { get; set; }
    /// <summary>How many of the period's four parts have at least one located, non-excluded day.</summary>
    public int PartsCovered { get; set; }
    public int AwayParts { get; set; }
    public HomeCityCheckResult Result { get; set; }
    public HomeCityCheckSuppression Suppression { get; set; }
    public HomeCityTicketState TicketState { get; set; }
    public long? TicketId { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? FailureCode { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}

/// <summary>D349's starting values, all configurable under `Location:HomeCityCheck` ("All thresholds are configurable starting values").</summary>
public sealed record HomeCityCheckOptions
{
    /// <summary>Vicinity option B (CONFIRMED): the radius around the Home City's GeoNames point.</summary>
    public double VicinityKm { get; init; } = 25;
    public int PeriodDays { get; init; } = 30;
    public int MinimumDays { get; init; } = 12;
    public double AwayShare { get; init; } = 0.75;
    public int MinimumAwayParts { get; init; } = 3;
    /// <summary>No home day in this many final days of the period.</summary>
    public int QuietHomeDays { get; init; } = 7;
    public int CooldownDays { get; init; } = 90;
    public int DayRetentionDays { get; init; } = 35;
    public int ResultRetentionMonths { get; init; } = 12;
    /// <summary>Swoovr Admin's public origin, for the account link in the Zammad note. Without it the note gives the path only.</summary>
    public string? AdminBaseUrl { get; init; }
}

/// <summary>The counts one period produced, before any suppression.</summary>
public sealed record HomeCityEvaluation(int LocatedDays, int AwayDays, int HomeDays, int ExcludedDays, int PartsCovered, int AwayParts, HomeCityCheckResult Result);

/// <summary>
/// D349, #281: the scheduled, non-punitive Home City check. Pure rules: no I/O, no positions kept. It never changes the
/// Home City, restricts the account or warns the member; the most it does is suggest that Support might get in touch.
/// </summary>
public static class HomeCityCheckPolicy
{
    public const int Parts = 4;
    /// <summary>The Zammad group (Support, Trust &amp; Safety and Admin).</summary>
    public const string Group = "Home City checks";
    /// <summary>The ticket title, which the member sees only once Support has replied.</summary>
    public const string Title = "Your Home City";
    public const string MacroName = "Suggest updating your Home City";

    /// <summary>The outcome one accepted reading gives its day. Distance is used here and discarded.</summary>
    public static HomeCityDayOutcome Outcome(double distanceKm, bool onCheckedInTrip, HomeCityCheckOptions options) =>
        onCheckedInTrip ? HomeCityDayOutcome.Excluded : distanceKm <= options.VicinityKm ? HomeCityDayOutcome.Home : HomeCityDayOutcome.Away;

    /// <summary>Several readings on one day: the higher-precedence outcome wins (excluded, then home, then away).</summary>
    public static HomeCityDayOutcome Combine(HomeCityDayOutcome? existing, HomeCityDayOutcome next) =>
        existing is { } current && current > next ? current : next;

    public static DateOnly PeriodEnd(DateOnly start, HomeCityCheckOptions options) => start.AddDays(options.PeriodDays - 1);

    /// <summary>The oldest day still kept: daily outcomes before it have been (or may have been) purged.</summary>
    public static DateOnly RetainedFrom(DateTimeOffset now, HomeCityCheckOptions options) =>
        DateOnly.FromDateTime(now.UtcDateTime).AddDays(-options.DayRetentionDays);

    /// <summary>
    /// Whether every day of the period is still within the retained evidence. A period checked late (the worker was down)
    /// may have lost its first days to the purge; those would read as days without readings and could change the result,
    /// so such a period is never evaluated.
    /// </summary>
    public static bool EvidenceComplete(DateOnly start, DateTimeOffset now, HomeCityCheckOptions options) => start >= RetainedFrom(now, options);

    /// <summary>Which of the four parts a day falls in: days 1–7, 8–14, 15–21 and 22 to the end.</summary>
    public static int Part(DateOnly start, DateOnly day) => Math.Min(Parts - 1, (day.DayNumber - start.DayNumber) / 7);

    /// <summary>
    /// The period's counts and result. Days without readings are unknown and simply absent. Excluded days count for neither
    /// the calculation nor the evidence. Skipped unless there are enough located days in every part; a suggestion needs a
    /// large enough away share, away days in enough parts and no home day near the end of the period.
    /// </summary>
    public static HomeCityEvaluation Evaluate(DateOnly start, IEnumerable<HomeCityDay> days, HomeCityCheckOptions options)
    {
        var end = PeriodEnd(start, options);
        var inPeriod = days.Where(d => d.Day >= start && d.Day <= end).GroupBy(d => d.Day).Select(g => (Day: g.Key, Outcome: g.Max(d => d.Outcome))).ToList();
        var located = inPeriod.Where(d => d.Outcome != HomeCityDayOutcome.Excluded).ToList();
        var away = located.Where(d => d.Outcome == HomeCityDayOutcome.Away).ToList();
        var parts = located.Select(d => Part(start, d.Day)).Distinct().Count();
        var awayParts = away.Select(d => Part(start, d.Day)).Distinct().Count();
        var excluded = inPeriod.Count - located.Count;
        HomeCityCheckResult result;
        if (located.Count < options.MinimumDays || parts < Parts) result = HomeCityCheckResult.Skipped;
        else
        {
            var quietFrom = end.AddDays(1 - options.QuietHomeDays);
            var recentHome = located.Any(d => d.Outcome == HomeCityDayOutcome.Home && d.Day >= quietFrom);
            result = away.Count >= options.AwayShare * located.Count && awayParts >= options.MinimumAwayParts && !recentHome
                ? HomeCityCheckResult.Suggested : HomeCityCheckResult.NoSuggestion;
        }
        return new(located.Count, away.Count, located.Count - away.Count, excluded, parts, awayParts, result);
    }

    /// <summary>
    /// Why a suggestion must not create an item now: "Don't suggest before" (a date in the Home City's time zone, still to
    /// come), an item already open, or one closed less than the cooldown ago.
    /// </summary>
    public static HomeCityCheckSuppression Suppress(DateOnly today, DateOnly? dontSuggestBefore, bool openItem, DateTimeOffset? lastClosedAt,
        DateTimeOffset now, HomeCityCheckOptions options)
    {
        if (dontSuggestBefore is { } until && today < until) return HomeCityCheckSuppression.DontSuggestBefore;
        if (openItem) return HomeCityCheckSuppression.OpenItem;
        if (lastClosedAt is { } closed && now - closed < TimeSpan.FromDays(options.CooldownDays)) return HomeCityCheckSuppression.Cooldown;
        return HomeCityCheckSuppression.None;
    }

    /// <summary>Retry spacing for creating the item: 15 minutes doubling to a day; ten attempts, then Failed for staff to see.</summary>
    public const int MaximumAttempts = 10;
    public static TimeSpan Backoff(int attempts) => TimeSpan.FromMinutes(Math.Min(24 * 60, 15 * Math.Pow(2, Math.Max(0, attempts - 1))));

    /// <summary>"away on 15 of 17 days with readings": observed days only, never a claim about where the member lived.</summary>
    public static string Observed(int awayDays, int locatedDays) =>
        string.Create(CultureInfo.InvariantCulture, $"away on {awayDays} of {locatedDays} day{(locatedDays == 1 ? "" : "s")} with readings");

    /// <summary>
    /// The internal note that opens the item. Only the admin account link, public id, Home City label, period dates and
    /// counts; never coordinates, places, trip destinations or a per-day list.
    /// </summary>
    public static string Note(string accountLink, Guid publicId, string homeCity, HomeCityCheck check) => string.Join("\n", new[]
    {
        "Scheduled Home City check (D349). The member may benefit from updating their Home City.",
        "",
        "Account: " + accountLink,
        "Public id: " + publicId.ToString("D"),
        "Home City: " + homeCity,
        "Period: " + check.PeriodStart.ToString("d MMM yyyy", CultureInfo.InvariantCulture) + " – " + check.PeriodEnd.ToString("d MMM yyyy", CultureInfo.InvariantCulture),
        "Result: " + Observed(check.AwayDays, check.LocatedDays) + string.Create(CultureInfo.InvariantCulture,
            $"; days with readings in {check.PartsCovered} of {Parts} parts of the period, away days in {check.AwayParts}; {check.ExcludedDays} day{(check.ExcludedDays == 1 ? "" : "s")} on a checked-in trip not counted."),
        "",
        "These are observed days only and don't establish where the member lived. Nothing on the account has changed, and the member can't see this item until you reply.",
        "If you contact them, use the “" + MacroName + "” reply.",
    });

    /// <summary>
    /// The friendly reply Support may send (Zammad macro and Swoovr Admin text module). It suggests, never requires, and
    /// says nothing about readings or places.
    /// </summary>
    public const string Reply =
        "Hi, we're checking in because it looks like you may have moved recently. If your Home City has changed, you can update it any time in Me → Profile, so people near you can find you. If nothing has changed, there's nothing you need to do. Thanks for being part of Swoovr.";

    /// <summary>
    /// D349: a system-created item stays out of the member's Support list (and detail) until Support has written something
    /// the member may see. Other items are unaffected.
    /// </summary>
    public static bool MemberVisible(string? source, IEnumerable<SupportArticle> articles) =>
        source != TicketingPolicy.SystemSource || articles.Any(a => a.CustomerVisible);
}
