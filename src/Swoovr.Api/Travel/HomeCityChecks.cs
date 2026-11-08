using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Profiles;
using Swoovr.Api.Ticketing;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Travel;

/// <summary>D349, #281: the configurable starting values under `Location:HomeCityCheck`.</summary>
public static class HomeCityCheckSettings
{
    public static HomeCityCheckOptions Read(IConfiguration configuration)
    {
        var d = new HomeCityCheckOptions();
        var section = configuration.GetSection("Location:HomeCityCheck");
        var options = new HomeCityCheckOptions
        {
            VicinityKm = section.GetValue("VicinityKm", d.VicinityKm),
            PeriodDays = section.GetValue("PeriodDays", d.PeriodDays),
            MinimumDays = section.GetValue("MinimumDays", d.MinimumDays),
            AwayShare = section.GetValue("AwayShare", d.AwayShare),
            MinimumAwayParts = section.GetValue("MinimumAwayParts", d.MinimumAwayParts),
            QuietHomeDays = section.GetValue("QuietHomeDays", d.QuietHomeDays),
            CooldownDays = section.GetValue("CooldownDays", d.CooldownDays),
            DayRetentionDays = section.GetValue("DayRetentionDays", d.DayRetentionDays),
            ResultRetentionMonths = section.GetValue("ResultRetentionMonths", d.ResultRetentionMonths),
            AdminBaseUrl = section["AdminBaseUrl"] is { Length: > 0 } url ? url : null,
        };
        if (options.VicinityKm is <= 0 or > 500 || options.PeriodDays is < 28 or > 31 || options.MinimumDays < 1 || options.MinimumDays > options.PeriodDays
            || options.AwayShare is <= 0 or > 1 || options.MinimumAwayParts is < 1 or > HomeCityCheckPolicy.Parts || options.QuietHomeDays < 0
            || options.QuietHomeDays > options.PeriodDays || options.CooldownDays < 0 || options.DayRetentionDays < options.PeriodDays
            || options.ResultRetentionMonths < 1
            || options.AdminBaseUrl is { } admin && !(Uri.TryCreate(admin, UriKind.Absolute, out var parsed) && parsed.Scheme is "https" or "http" && parsed.Query == "" && parsed.UserInfo == ""))
            throw new InvalidOperationException("Invalid Location:HomeCityCheck settings.");
        return options;
    }
}

/// <summary>
/// D349, #281: turns each accepted device reading (Near me's PUT /me/location and the member's own "Check my location
/// matches") into that day's outcome. The reading's distance from the Home City's GeoNames point is used here and
/// dropped: nothing positional is stored, logged or audited. Never IP and never a background reading.
/// </summary>
public sealed class HomeCityDayRecorder(IConfiguration configuration) : IAcceptedReadingObserver
{
    public async Task ObserveAsync(SwoovrDbContext db, MemberProfile member, LocationReading reading, DateTimeOffset now, CancellationToken cancellationToken) =>
        await RecordAsync(db, member, reading, now, HomeCityCheckSettings.Read(configuration), cancellationToken);

    /// <summary>This reading's outcome (the day keeps the higher-precedence one), or null when the member has no Home City.</summary>
    public static async Task<HomeCityDayOutcome?> RecordAsync(SwoovrDbContext db, MemberProfile member, LocationReading reading, DateTimeOffset now,
        HomeCityCheckOptions options, CancellationToken cancellationToken)
    {
        if (member.HomeGeoNameId is not { } home || await db.GeoCities.FindAsync([home], cancellationToken) is not { } city) return null;
        var day = TravelPolicy.LocalToday(city.TimeZone, reading.TakenAt > now ? now : reading.TakenAt);
        var onTrip = (await MarketEndpoints.ActiveTripsAsync(db, [member.AccountId], now, cancellationToken)).ContainsKey(member.AccountId);
        var outcome = HomeCityCheckPolicy.Outcome(PlaceNames.DistanceKm(reading.Latitude, reading.Longitude, city.Latitude, city.Longitude), onTrip, options);
        // One row per day; concurrent readings merge in the database with the same precedence as HomeCityCheckPolicy.Combine.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO home_city_days (account_id, day, outcome, updated_at) VALUES ({member.AccountId}, {day}, {outcome.ToString()}, {now})
            ON CONFLICT (account_id, day) DO UPDATE SET
              outcome = CASE WHEN home_city_days.outcome = 'Excluded' OR (home_city_days.outcome = 'Home' AND EXCLUDED.outcome = 'Away')
                             THEN home_city_days.outcome ELSE EXCLUDED.outcome END,
              updated_at = EXCLUDED.updated_at
            """, cancellationToken);
        if (member.HomeCityCheckFrom is null)
        {
            // The period starts with the first accepted reading (and again after a Home City change or a period without readings).
            member.HomeCityCheckFrom = day;
            await db.SaveChangesAsync(cancellationToken);
        }
        return outcome;
    }

    /// <summary>
    /// A Home City change restarts the period: the old city's days no longer say anything (D349), and a suggestion still
    /// waiting for its Zammad item is withdrawn, because its counts were measured against the old city.
    /// </summary>
    public static async Task RestartAsync(SwoovrDbContext db, MemberProfile member, CancellationToken cancellationToken)
    {
        member.HomeCityCheckFrom = null;
        await db.HomeCityDays.Where(d => d.AccountId == member.AccountId).ExecuteDeleteAsync(cancellationToken);
        await db.HomeCityChecks.Where(c => c.AccountId == member.AccountId && c.TicketState == HomeCityTicketState.Pending)
            .ExecuteUpdateAsync(u => u.SetProperty(c => c.Result, HomeCityCheckResult.Suppressed).SetProperty(c => c.Suppression, HomeCityCheckSuppression.HomeCityChanged)
                .SetProperty(c => c.TicketState, HomeCityTicketState.None).SetProperty(c => c.NextAttemptAt, (DateTimeOffset?)null), cancellationToken);
    }
}

public sealed record HomeCityCheckRun(int DaysPurged, int ResultsPurged, int Checked, int Suggested, int Created, int Suppressed, int Waiting, int Failed);

/// <summary>
/// D349, #281: the scheduled check. Hourly (or `location home-city-check`): purge expired days and results; check every
/// member whose 30-day period has ended in their Home City's time zone; then create the Zammad items suggestions are
/// waiting for. Logs counts only. Never changes the Home City, restricts the account or creates a warning on it.
/// </summary>
public sealed class HomeCityCheckJob(SwoovrDbContext db, TicketingActorFactory actors, IConfiguration configuration, TimeProvider clock, ILogger<HomeCityCheckJob> log)
{
    public async Task<HomeCityCheckRun> RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var options = HomeCityCheckSettings.Read(configuration);
        var utcToday = DateOnly.FromDateTime(now.UtcDateTime);
        int checkedCount = 0, suggested = 0, created = 0, suppressed = 0, waiting = 0, failed = 0;

        // Members whose period may have ended (local dates can lead UTC by up to a day).
        var latestStart = utcToday.AddDays(2 - options.PeriodDays);
        var due = await (from p in db.MemberProfiles join c in db.GeoCities on p.HomeGeoNameId equals (int?)c.GeoNameId
                         where p.HomeCityCheckFrom != null && p.HomeCityCheckFrom <= latestStart select new { p.AccountId, c.TimeZone }).ToListAsync(ct);
        foreach (var member in due)
        {
            var result = await CheckAsync(member.AccountId, member.TimeZone, now, options, ct);
            if (result is null) continue;
            checkedCount++;
            if (result.Result == HomeCityCheckResult.Suggested) suggested++;
            if (result.Result == HomeCityCheckResult.Suppressed) suppressed++;
        }

        // Purged after the evaluation, with the same cutoff CheckAsync uses to refuse incomplete periods.
        var retainedFrom = HomeCityCheckPolicy.RetainedFrom(now, options);
        var daysPurged = await db.HomeCityDays.Where(d => d.Day < retainedFrom).ExecuteDeleteAsync(ct);
        var resultsPurged = await db.HomeCityChecks.Where(c => c.CheckedAt < now.AddMonths(-options.ResultRetentionMonths)).ExecuteDeleteAsync(ct);

        var pending = await db.HomeCityChecks.Where(c => c.TicketState == HomeCityTicketState.Pending && (c.NextAttemptAt == null || c.NextAttemptAt <= now))
            .OrderBy(c => c.CheckedAt).Select(c => c.Id).Take(200).ToListAsync(ct);
        foreach (var id in pending)
        {
            var row = await db.HomeCityChecks.SingleAsync(c => c.Id == id, ct);
            await DeliverAsync(row, now, options, ct);
            switch (row.TicketState)
            {
                case HomeCityTicketState.Created: created++; break;
                case HomeCityTicketState.Pending: waiting++; break;
                case HomeCityTicketState.Failed: failed++; break;
                default: if (row.Result == HomeCityCheckResult.Suppressed) suppressed++; break;
            }
        }
        var run = new HomeCityCheckRun(daysPurged, resultsPurged, checkedCount, suggested, created, suppressed, waiting, failed);
        if (run != new HomeCityCheckRun(0, 0, 0, 0, 0, 0, 0, 0))
            log.LogInformation("Home City check: {Checked} checked, {Suggested} suggested, {Created} items created, {Suppressed} suppressed, {Waiting} waiting, {Failed} failed; {Days} days and {Results} results purged.",
                run.Checked, run.Suggested, run.Created, run.Suppressed, run.Waiting, run.Failed, run.DaysPurged, run.ResultsPurged);
        return run;
    }

    /// <summary>Checks one member's ended period, records the count-only result and moves the period on. Null when nothing was due.</summary>
    private async Task<HomeCityCheck?> CheckAsync(Guid accountId, string timeZone, DateTimeOffset now, HomeCityCheckOptions options, CancellationToken ct)
    {
        var profile = await db.MemberProfiles.SingleAsync(p => p.AccountId == accountId, ct);
        if (profile.HomeCityCheckFrom is not { } start) return null;
        var end = HomeCityCheckPolicy.PeriodEnd(start, options);
        var today = TravelPolicy.LocalToday(timeZone, now);
        if (today <= end) return null;
        if (!HomeCityCheckPolicy.EvidenceComplete(start, now, options))
        {
            // Checked too late (the worker was down): earlier days may be purged and would read as days without readings,
            // which could turn a no-suggestion into a suggestion. No result; the next accepted reading starts a new period.
            profile.HomeCityCheckFrom = null;
            Audit.Record(db, now, null, "system", "home-city-check.expired", "account", accountId.ToString(), start.ToString("yyyy-MM-dd"));
            await db.SaveChangesAsync(ct);
            return null;
        }
        var active = await db.Accounts.AnyAsync(a => a.Id == accountId && a.Realm == AccountRealm.Members && a.Status == AccountStatus.Active, ct);
        var days = await db.HomeCityDays.Where(d => d.AccountId == accountId && d.Day >= start && d.Day <= end).ToListAsync(ct);
        if (!active || days.Count == 0)
        {
            // Not shared in the period (or not an active member): no result; the next accepted reading starts a new period.
            profile.HomeCityCheckFrom = null;
            await db.SaveChangesAsync(ct);
            return null;
        }
        var evaluation = HomeCityCheckPolicy.Evaluate(start, days, options);
        var row = new HomeCityCheck
        {
            Id = Guid.NewGuid(), AccountId = accountId, PeriodStart = start, PeriodEnd = end, LocatedDays = evaluation.LocatedDays, AwayDays = evaluation.AwayDays,
            ExcludedDays = evaluation.ExcludedDays, PartsCovered = evaluation.PartsCovered, AwayParts = evaluation.AwayParts, Result = evaluation.Result, CheckedAt = now,
        };
        if (row.Result == HomeCityCheckResult.Suggested)
        {
            if (HomeCityCheckPolicy.Suppress(today, profile.HomeCityDontSuggestBefore, false, null, now, options) is var why and not HomeCityCheckSuppression.None)
                (row.Result, row.Suppression) = (HomeCityCheckResult.Suppressed, why);
            else (row.TicketState, row.NextAttemptAt) = (HomeCityTicketState.Pending, now);
        }
        // The next period follows on; a period the worker missed entirely is checked on the next run.
        profile.HomeCityCheckFrom = end.AddDays(1);
        if (await db.HomeCityChecks.AnyAsync(c => c.AccountId == accountId && c.PeriodStart == start, ct)) { await db.SaveChangesAsync(ct); return null; }
        db.HomeCityChecks.Add(row);
        Audit.Record(db, now, null, "system", "home-city-check.checked", "account", accountId.ToString(), Wire(row.Result) + (row.Suppression == HomeCityCheckSuppression.None ? "" : ":" + Wire(row.Suppression)));
        await db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>
    /// Creates the suggestion's item unless one is open or closed within the cooldown (checked live in Zammad), as one
    /// internal note in "Home City checks" with the member as customer. The row id is the create operation, so a lost
    /// response is reconciled, never duplicated.
    /// </summary>
    private async Task DeliverAsync(HomeCityCheck row, DateTimeOffset now, HomeCityCheckOptions options, CancellationToken ct)
    {
        var provider = actors.HomeCityCheck();
        if (provider is null || !actors.Configured) { Defer(row, now, "ticketing-unavailable"); await db.SaveChangesAsync(ct); return; }
        try
        {
            var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == row.AccountId && a.Realm == AccountRealm.Members && a.Status == AccountStatus.Active, ct);
            var profile = await db.MemberProfiles.SingleOrDefaultAsync(p => p.AccountId == row.AccountId, ct);
            var city = profile?.HomeGeoNameId is { } home ? await db.GeoCities.FindAsync([home], ct) : null;
            if (account is null || profile is null || city is null)
            {
                (row.TicketState, row.FailureCode, row.NextAttemptAt) = (HomeCityTicketState.Failed, "account-unavailable", null);
                await db.SaveChangesAsync(ct);
                return;
            }
            var groups = await provider.GroupNamesAsync(ct);
            if (groups.Where(g => g.Value == HomeCityCheckPolicy.Group).Select(g => (long?)g.Key).SingleOrDefault() is not { } group)
            { Defer(row, now, "group-missing"); await db.SaveChangesAsync(ct); return; }

            // At most one open item, and a cooldown after closure: earlier results' items plus anything else in the group for this customer.
            var mapped = await db.Set<TicketingIdentity>().Where(m => m.AccountId == row.AccountId && m.State == TicketingOperationState.Applied && m.DeactivatedAt == null)
                .Select(m => m.ProviderUserId).SingleOrDefaultAsync(ct);
            var ids = await db.HomeCityChecks.Where(c => c.AccountId == row.AccountId && c.Id != row.Id && c.TicketId != null).Select(c => c.TicketId!.Value).ToListAsync(ct);
            if (mapped is long customerId)
                ids.AddRange((await provider.SearchAsync($"group_id:{group} AND customer_id:{customerId}", 1, 50, ct)).Select(t => t.Id));
            var closedStates = await provider.ClosedStatesAsync(ct);
            var (open, lastClosed) = (false, (DateTimeOffset?)null);
            foreach (var id in ids.Distinct())
            {
                if (await provider.TicketAsync(id, ct) is not { } ticket || ticket.GroupId != group || mapped is long c && ticket.CustomerId != c) continue;
                if (!closedStates.Contains(ticket.StateId)) open = true;
                else if ((ticket.LastCloseAt ?? ticket.UpdatedAt) is var at && (lastClosed is null || at > lastClosed)) lastClosed = at;
            }
            var why = HomeCityCheckPolicy.Suppress(TravelPolicy.LocalToday(city.TimeZone, now), profile.HomeCityDontSuggestBefore, open, lastClosed, now, options);
            if (why != HomeCityCheckSuppression.None)
            {
                (row.Result, row.Suppression, row.TicketState, row.NextAttemptAt, row.FailureCode) = (HomeCityCheckResult.Suppressed, why, HomeCityTicketState.None, null, null);
                Audit.Record(db, now, null, "system", "home-city-check.suppressed", "account", row.AccountId.ToString(), Wire(why));
                await db.SaveChangesAsync(ct);
                return;
            }

            var operations = new TicketingOperations(db, actors.Identity(), provider, clock);
            var customer = await operations.EnsureCustomerAsync(row.AccountId, account.Email, ct);
            var link = options.AdminBaseUrl is { } admin ? admin.TrimEnd('/') + "/accounts/" + row.AccountId.ToString("D") : "Swoovr Admin /accounts/" + row.AccountId.ToString("D");
            var note = HomeCityCheckPolicy.Note(link, profile.PublicId, PlaceNames.Label(city), row);
            var created = await operations.CreateTicketAsync(row.AccountId, row.Id,
                new SupportTicketDraft(HomeCityCheckPolicy.Title, note, customer, group, row.Id.ToString("D"), TicketingPolicy.SystemSource), ct, sendAttachments: false);
            (row.TicketState, row.TicketId, row.FailureCode, row.NextAttemptAt) = (HomeCityTicketState.Created, created.Id, null, null);
            Audit.Record(db, now, null, "system", "home-city-check.item-created", "account", row.AccountId.ToString(), TicketingProjection.Reference(created.Id));
            await db.SaveChangesAsync(ct);
        }
        catch (TicketingProviderException error) { Defer(row, now, error.Code); await db.SaveChangesAsync(CancellationToken.None); }
        catch (TicketingAccessException error) { Defer(row, now, error.Code); await db.SaveChangesAsync(CancellationToken.None); }
        catch (InvalidOperationException)
        {
            // A rejected operation or an inactive customer mapping: never retried blindly; staff see it as Failed.
            (row.TicketState, row.FailureCode, row.NextAttemptAt) = (HomeCityTicketState.Failed, "operation-rejected", null);
            await db.SaveChangesAsync(CancellationToken.None);
        }
    }

    private static void Defer(HomeCityCheck row, DateTimeOffset now, string code)
    {
        row.Attempts++;
        row.FailureCode = code.Length > 64 ? code[..64] : code;
        if (row.Attempts >= HomeCityCheckPolicy.MaximumAttempts) (row.TicketState, row.NextAttemptAt) = (HomeCityTicketState.Failed, null);
        else row.NextAttemptAt = now + HomeCityCheckPolicy.Backoff(row.Attempts);
    }

    /// <summary>The admin card's view: the current period, the latest result in observed days, the item and "Don't suggest before".</summary>
    public static async Task<HomeCityCheckAdminView?> ViewAsync(SwoovrDbContext db, MemberProfile? profile, CancellationToken ct, HomeCityCheckOptions? configured = null)
    {
        if (profile is null) return null;
        var options = configured ?? new HomeCityCheckOptions();
        var last = await db.HomeCityChecks.Where(c => c.AccountId == profile.AccountId).OrderByDescending(c => c.PeriodStart).FirstOrDefaultAsync(ct);
        return new(profile.HomeCityDontSuggestBefore, profile.HomeCityCheckFrom,
            profile.HomeCityCheckFrom is { } from ? HomeCityCheckPolicy.PeriodEnd(from, options) : null,
            last is null ? null : new HomeCityCheckResultView(last.PeriodStart, last.PeriodEnd, Wire(last.Result),
                last.Suppression == HomeCityCheckSuppression.None ? null : Wire(last.Suppression), last.LocatedDays, last.AwayDays, last.ExcludedDays, last.PartsCovered,
                HomeCityCheckPolicy.Observed(last.AwayDays, last.LocatedDays), Wire(last.TicketState), last.TicketId is { } t ? TicketingProjection.Reference(t) : null, last.TicketId));
    }
}

/// <summary>D349: runs the Home City check hourly. Hosted runs only by default (as the retention worker); `location home-city-check` runs it on demand.</summary>
public sealed class HomeCityCheckWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<HomeCityCheckWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromMinutes(2), clock, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<HomeCityCheckJob>().RunAsync(clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { log.LogError("Home City check failed ({ErrorType}); it retries in an hour.", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}

/// <summary>D349: Support, Trust &amp; Safety and Admin set "Don't suggest before" (audited with the reason).</summary>
public static class HomeCityCheckEndpoints
{
    public const int ReasonMax = 300;

    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization();
        admin.MapPut("/accounts/{id:guid}/home-city-check", async (Guid id, HomeCityCheckChange request, ClaimsPrincipal user, AccountResolver accounts,
            IConfiguration configuration, TimeProvider clock, CancellationToken ct) =>
        {
            var current = await accounts.ResolveAsync(user, ct);
            if (current is null) return Results.Unauthorized();
            if (current.Account is null || accounts.Db is null)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available.");
            if (!new[] { AccountRole.Support, AccountRole.TrustSafety, AccountRole.Admin }.Any(current.Allows)) return Results.Forbid();
            var reason = request.Reason?.Trim();
            if (reason is { Length: > ReasonMax } || reason?.Any(char.IsControl) == true)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Keep the reason under 300 characters.");
            var (db, now) = (accounts.Db, clock.GetUtcNow());
            if (request.DontSuggestBefore is { } date && (date < DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1) || date > DateOnly.FromDateTime(now.UtcDateTime).AddYears(2)))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Choose a date from today up to two years ahead.");
            if (!await db.Accounts.AnyAsync(a => a.Id == id && a.Realm == AccountRealm.Members, ct)) return Results.NotFound();
            if (await StaffAccessEndpoints.ExcludedAsync(db, current.Account.Id, [id], ct)) return Problem(StatusCodes.Status403Forbidden, "conflict-of-interest", "You can't act on this account.");
            var profile = await db.MemberProfiles.FindAsync([id], ct);
            if (profile is null) return Results.NotFound();
            profile.HomeCityDontSuggestBefore = request.DontSuggestBefore;
            Audit.Record(db, now, current.Account.Id, "account", "home-city-check.dont-suggest-before", "account", id.ToString(),
                (request.DontSuggestBefore?.ToString("yyyy-MM-dd") ?? "cleared") + (string.IsNullOrEmpty(reason) ? "" : ": " + reason));
            await db.SaveChangesAsync(ct);
            return Results.Ok(await HomeCityCheckJob.ViewAsync(db, profile, ct, HomeCityCheckSettings.Read(configuration)));
        });
    }
}
