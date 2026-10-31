using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Profiles;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Travel;

/// <summary>
/// Slice T (D142, D149, D150, D153, D164): confirm-arrival with a fresh device location, Traveller Search through one of
/// the member's own trips, home-location consistency checks, and the staff routes that confirm an arrival after a review
/// or clear a home-location review. Readings are checked and dropped: no coordinates, IP addresses or derived places
/// are stored, returned or audited; the audit holds trip ids, market ids and outcomes only.
/// </summary>
public static class ArrivalEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // D149: the explicit confirm-arrival operation. A client "arrived" flag is never read; only the reading is.
        api.MapPost("/me/trips/{id:guid}/arrival", async (Guid id, LocationCheck? reading, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            var row = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId where t.Id == id && t.AccountId == me.AccountId select new { t, c })
                .SingleOrDefaultAsync(cancellationToken);
            if (row is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That trip isn't there.");
            if (await LimitedAsync(db, me.AccountId, now, cancellationToken)) return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyAttempts, "That's a lot of location checks. Try again later.");
            var zones = await MarketEndpoints.ZonesAsync(db, cancellationToken);
            var market = PlaceNames.MarketOf(row.c, zones);
            var state = TravelPolicy.State(new TripFacts(row.t, row.c.TimeZone), [], now);
            var refusal = ArrivalPolicy.Confirm(state, market, TravelPolicy.LocalToday(row.c.TimeZone, now), row.t.Arrival, Reading(reading), zones, now);
            row.t.ArrivalCheckedAt = now;
            // A failed re-check never takes back a confirmed arrival (rule 10); only staff or a change of trip does.
            if (refusal is ArrivalRefusal.Outside or ArrivalRefusal.Unusable && row.t.ArrivedAt is null)
                row.t.ArrivalOutcome = refusal == ArrivalRefusal.Outside ? ArrivalOutcome.Outside : ArrivalOutcome.Unusable;
            if (refusal == ArrivalRefusal.None)
                (row.t.ArrivedAt, row.t.ArrivalMarketId, row.t.ArrivalMethod, row.t.ArrivalOutcome) = (now, market!.Id, ArrivalMethod.DeviceLocation, ArrivalOutcome.Confirmed);
            Audit.Record(db, now, me.AccountId, "account", refusal == ArrivalRefusal.None ? "trip.arrived" : "trip.arrival-refused", "trip", row.t.Id.ToString(),
                refusal == ArrivalRefusal.None ? $"device:{market!.Id}" : Wire(refusal));
            await db.SaveChangesAsync(cancellationToken);
            return refusal switch
            {
                ArrivalRefusal.None => Results.Ok(await TravelEndpoints.ViewAsync(db, row.t, row.c, me, now, cancellationToken)),
                ArrivalRefusal.TripNotLive => Problem(StatusCodes.Status409Conflict, ProblemCodes.TripNotLive, "This trip is cancelled, over, or somewhere Swoovr isn't open."),
                ArrivalRefusal.TooEarly => Problem(StatusCodes.Status403Forbidden, ProblemCodes.ArrivalTooEarly, "You can confirm your arrival from your arrival date."),
                ArrivalRefusal.Unusable => Problem(StatusCodes.Status400BadRequest, ProblemCodes.LocationUnusable, "We couldn't use your location. Try again with location turned on."),
                _ => Problem(StatusCodes.Status403Forbidden, ProblemCodes.LocationOutsideMarket, "Your location isn't in this trip's city yet."),
            };
        });

        // D142: Traveller Search through one of the member's own trips. Never a free choice of market (Q41 rule 8).
        api.MapGet("/travel/search", async (Guid? trip, string? mode, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            var row = trip is null ? null : await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                                                   where t.Id == trip && t.AccountId == me.AccountId select new { t, c }).SingleOrDefaultAsync(cancellationToken);
            // Another member's trip reads the same as none.
            if (row is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That trip isn't there.");
            var selected = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == mode && m.Available, cancellationToken);
            if (selected is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
            var market = PlaceNames.MarketOf(row.c, await MarketEndpoints.ZonesAsync(db, cancellationToken));
            var state = TravelPolicy.State(new TripFacts(row.t, row.c.TimeZone), [], now);
            switch (TravellerSearch.Check(row.t, state, market, TravelPolicy.LocalToday(row.c.TimeZone, now), me.HomeMarketId))
            {
                case SearchRefusal.TripNotLive: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.TripNotLive, "This trip is cancelled, over, or somewhere Swoovr isn't open.");
                case SearchRefusal.OwnMarket: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.SearchNotOpen, "This is your home market: use Explore.");
                case SearchRefusal.Hidden: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.SearchNotOpen, "Make the trip visible to locals to search there.");
                case SearchRefusal.TooEarly: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.SearchNotOpen, "Traveller Search opens 14 days before you arrive.");
            }
            if (!await db.MarketModes.AnyAsync(m => m.MarketId == market!.Id && m.ModeId == selected.Id, cancellationToken))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.MarketNotOpen, "Swoovr doesn't offer this mode there.");
            var (own, mine, notReady) = await ProfileEndpoints.ReadyAsync(db, me, selected, now, cancellationToken);
            if (notReady is not null) return notReady;
            // #90: searches are limited per hour so repeated queries can't triangulate anyone.
            if (await Profiles.Searches.LimitedAsync(db, me.AccountId, "search", now, cancellationToken) is { } limited) return limited;
            var people = await ProfileEndpoints.PeopleAsync(db, configuration, me, selected, own!, mine!, market!.Id, now, cancellationToken,
                accounts.Services.GetRequiredService<Media.MediaLinks>());
            return Results.Ok(new TravellerSearchResult(selected.Id, market.Name, row.t.Id, Wire(ArrivalPolicy.State(row.t)),
                ArrivalPolicy.MayInitiate(row.t, state, market), people));
        });

        // D150, D153: a home-location consistency check. The reading is compared with the home market and dropped.
        api.MapPost("/me/home-check", async (LocationCheck? reading, HttpRequest http, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            if (await LimitedAsync(db, me.AccountId, now, cancellationToken)) return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyAttempts, "That's a lot of location checks. Try again later.");
            var device = Reading(reading);
            if (!ArrivalPolicy.Usable(device, now)) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LocationUnusable, "We couldn't use your location. Try again with location turned on.");
            if (me.HomeMarketId is not { } home) return Results.Ok(new HomeCheckView("not-checked"));
            // Someone with a confirmed arrival on a live trip elsewhere is travelling, not inconsistent (Q47 rule 6).
            if (await TravellingAsync(db, me.AccountId, now, cancellationToken)) return Results.Ok(new HomeCheckView("not-checked"));
            var zones = (await MarketEndpoints.ZonesAsync(db, cancellationToken)).Where(z => z.Market.Id == home).ToList();
            var outcome = HomeLocationPolicy.Judge(ArrivalPolicy.InMarket(device!, zones));
            db.HomeLocationChecks.Add(new HomeLocationCheck { Id = Guid.NewGuid(), AccountId = me.AccountId, CheckedAt = now, Method = LocationMethod.Device, HomeMarketId = home, Outcome = outcome });
            // D153: Cloudflare's approximate IP location only where the host is behind Cloudflare (off by default, never locally).
            if (configuration.GetValue("Location:CloudflareHeaders", false) && IpReading(http, now) is { } ip)
                db.HomeLocationChecks.Add(new HomeLocationCheck { Id = Guid.NewGuid(), AccountId = me.AccountId, CheckedAt = now, Method = LocationMethod.ApproximateIp, HomeMarketId = home,
                    Outcome = HomeLocationPolicy.Judge(ArrivalPolicy.InMarket(ip, zones)) });
            Audit.Record(db, now, me.AccountId, "account", "home-check.recorded", "account", me.AccountId.ToString(), Wire(outcome));
            await db.SaveChangesAsync(cancellationToken);
            if (me.HomeReviewFlaggedAt is null && HomeLocationPolicy.ShouldFlag(await db.HomeLocationChecks.Where(c => c.AccountId == me.AccountId).ToListAsync(cancellationToken), now))
            {
                me.HomeReviewFlaggedAt = now;
                Audit.Record(db, now, me.AccountId, "account", "home-review.flagged", "account", me.AccountId.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.Ok(new HomeCheckView(Wire(outcome)));
        });

        var admin = app.MapGroup("/api/admin").RequireAuthorization();

        // D149 rule 7: support confirms an arrival the device couldn't, after a review, with a reason.
        admin.MapGet("/accounts/{id:guid}/trips", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var zones = await MarketEndpoints.ZonesAsync(db, cancellationToken);
            var rows = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId where t.AccountId == id
                              orderby t.Arrival descending select new { t, c }).Take(50).ToListAsync(cancellationToken);
            return Results.Ok(rows.Select(r => new TripAdminView(r.t.Id, PlaceNames.MarketOf(r.c, zones)?.Id, r.t.Arrival, r.t.Departure,
                Wire(TravelPolicy.State(new TripFacts(r.t, r.c.TimeZone), [], now)), Wire(ArrivalPolicy.State(r.t)), r.t.ArrivalMethod is { } m ? Wire(m) : null)).ToList());
        });

        admin.MapPost("/trips/{id:guid}/arrival", async (Guid id, ArrivalReview request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Support, cancellationToken);
            if (denied is not null) return denied;
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > ModerationPolicy.ReasonMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Give a reason.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var row = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId where t.Id == id select new { t, c }).SingleOrDefaultAsync(cancellationToken);
            if (row is null) return Results.NotFound();
            var market = PlaceNames.MarketOf(row.c, await MarketEndpoints.ZonesAsync(db, cancellationToken));
            var state = TravelPolicy.State(new TripFacts(row.t, row.c.TimeZone), [], now);
            if (state is TripState.Cancelled or TripState.Expired || market is not { Open: true })
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.TripNotLive, "This trip is cancelled, over, or somewhere Swoovr isn't open.");
            if (TravelPolicy.LocalToday(row.c.TimeZone, now) < row.t.Arrival)
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ArrivalTooEarly, "The trip hasn't started yet.");
            (row.t.ArrivedAt, row.t.ArrivalMarketId, row.t.ArrivalMethod, row.t.ArrivalOutcome, row.t.ArrivalCheckedAt) = (now, market.Id, ArrivalMethod.SupportReview, ArrivalOutcome.Confirmed, now);
            Audit.Record(db, now, current!.Account!.Id, "account", "trip.arrival-reviewed", "trip", row.t.Id.ToString(), market.Id);
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        // D150: an admin clears the home-location review after looking; nothing else changes.
        admin.MapPost("/accounts/{id:guid}/home-review/clear", async (Guid id, HomeReviewClear request, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminEndpoints.StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > ModerationPolicy.ReasonMax)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Give a reason.");
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var profile = await db.MemberProfiles.FindAsync([id], cancellationToken);
            if (profile is null) return Results.NotFound();
            profile.HomeReviewFlaggedAt = null;
            // Checks before the clearing no longer count towards another flag.
            db.HomeLocationChecks.RemoveRange(db.HomeLocationChecks.Where(c => c.AccountId == id && c.CheckedAt <= now));
            Audit.Record(db, now, current!.Account!.Id, "account", "home-review.cleared", "account", id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await Moderation.ModerationEndpoints.SummaryAsync(db, id, now, cancellationToken));
        });
    }

    /// <summary>How a traveller stands to a destination local (D142, D149): modes they may browse there and the trip that lets them start contact.</summary>
    public sealed record TravellerReach(IReadOnlySet<string> SearchModes, IReadOnlyDictionary<string, Guid> ArrivedTrips);

    /// <summary>
    /// The viewer's live, searchable trips to the person's market (TravellerSearch.Check), in the shared modes that market
    /// offers: browsing for each, and contact where the trip's arrival is confirmed (ArrivalPolicy.MayInitiate). A bot
    /// counts as local where it is assigned. Nothing here opens another market without a trip (Q41 rule 8).
    /// </summary>
    public static async Task<TravellerReach> ReachAsync(SwoovrDbContext db, MemberProfile viewer, ProfileEndpoints.Row person, IReadOnlyList<ConnectionMode> shared,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (search, arrived) = (new HashSet<string>(), new Dictionary<string, Guid>());
        var yesterday = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var trips = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                           where t.AccountId == viewer.AccountId && t.CancelledAt == null && t.Departure >= yesterday select new { t, c }).ToListAsync(cancellationToken);
        if (trips.Count == 0) return new(search, arrived);
        var zones = await MarketEndpoints.ZonesAsync(db, cancellationToken);
        var bots = person.Profile.Bot ? await MarketEndpoints.BotMarketsAsync(db, person.Profile.AccountId, cancellationToken) : null;
        foreach (var x in trips.OrderByDescending(x => x.t.ArrivedAt is not null).ThenBy(x => x.t.Arrival))
        {
            var market = PlaceNames.MarketOf(x.c, zones);
            var state = TravelPolicy.State(new TripFacts(x.t, x.c.TimeZone), [], now);
            if (TravellerSearch.Check(x.t, state, market, TravelPolicy.LocalToday(x.c.TimeZone, now), viewer.HomeMarketId) != SearchRefusal.None) continue;
            if (!MarketAccess.SameMarket(market!.Id, person.Profile.HomeMarketId, person.Profile.Bot, bots)) continue;
            var offered = await db.MarketModes.Where(m => m.MarketId == market.Id).Select(m => m.ModeId).ToListAsync(cancellationToken);
            foreach (var mode in shared.Where(m => offered.Contains(m.Id)))
            {
                search.Add(mode.Id);
                if (ArrivalPolicy.MayInitiate(x.t, state, market)) arrived.TryAdd(mode.Id, x.t.Id);
            }
        }
        return new(search, arrived);
    }

    private static LocationReading? Reading(LocationCheck? check) => check is null ? null : new(check.Latitude, check.Longitude, check.AccuracyMetres, check.TakenAt);

    /// <summary>Cloudflare's visitor location headers (latitude and longitude), treated as a coarse reading (D153).</summary>
    private static LocationReading? IpReading(HttpRequest http, DateTimeOffset now) =>
        double.TryParse(http.Headers["cf-iplatitude"], System.Globalization.CultureInfo.InvariantCulture, out var lat)
        && double.TryParse(http.Headers["cf-iplongitude"], System.Globalization.CultureInfo.InvariantCulture, out var lon)
            ? new(lat, lon, ArrivalPolicy.MaxAccuracyMetres, now) : null;

    /// <summary>Location checks are rate limited per member (D164): arrival and home checks together, ten an hour.</summary>
    private static async Task<bool> LimitedAsync(SwoovrDbContext db, Guid me, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.AuditEntries.CountAsync(a => a.ActorAccountId == me && (a.Action == "trip.arrived" || a.Action == "trip.arrival-refused" || a.Action == "home-check.recorded")
            && a.At > now.AddHours(-1), cancellationToken) >= ArrivalPolicy.ChecksPerHour;

    private static async Task<bool> TravellingAsync(SwoovrDbContext db, Guid me, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var yesterday = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var trips = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                           where t.AccountId == me && t.ArrivedAt != null && t.CancelledAt == null && t.Departure >= yesterday select new { t, c }).ToListAsync(cancellationToken);
        return trips.Any(x => TravelPolicy.State(new TripFacts(x.t, x.c.TimeZone), [], now) is not (TripState.Cancelled or TripState.Expired));
    }
}
