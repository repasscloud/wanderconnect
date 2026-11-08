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
/// Slice T (D142, D149, D164): confirm-arrival with a fresh device location, Traveller Search through one of the member's
/// own trips, the member's "Check my location matches" reading (D349: a Home City day outcome), and the staff routes that
/// confirm an arrival after a review. Readings are checked and dropped: no coordinates, IP addresses or derived places
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
                case SearchRefusal.Off: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.SearchNotOpen, "Turn on \u201cDiscover people there before arrival\u201d to search there.");
                case SearchRefusal.TooEarly: return Problem(StatusCodes.Status403Forbidden, ProblemCodes.SearchNotOpen, "Traveller Search opens 14 days before you arrive.");
            }
            if (!await db.MarketModes.AnyAsync(m => m.MarketId == market!.Id && m.ModeId == selected.Id, cancellationToken))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.MarketNotOpen, "Swoovr doesn't offer this mode there.");
            var (own, mine, notReady) = await ProfileEndpoints.ReadyAsync(db, me, selected, now, cancellationToken);
            if (notReady is not null) return notReady;
            // #90: searches are limited per hour so repeated queries can't triangulate anyone.
            if (await Profiles.Searches.LimitedAsync(db, me.AccountId, "search", now, cancellationToken) is { } limited) return limited;
            // D352: the traveller's home city isn't where they'll be, so no home-city bands or distance filter here.
            var people = await ProfileEndpoints.PeopleAsync(db, configuration, me, selected, own!, mine!, market!.Id, now, cancellationToken,
                accounts.Services.GetRequiredService<Media.MediaLinks>(), viewerAway: true);
            return Results.Ok(new TravellerSearchResult(selected.Id, market.Name, row.t.Id, Wire(ArrivalPolicy.State(row.t)),
                ArrivalPolicy.MayInitiate(row.t, state, market), people));
        });

        // D349, #281 (supersedes the D150/D164 flag and D153's IP evidence): "Check my location matches" sends one device
        // reading, which feeds that day's Home City outcome like an accepted Near me reading. The reading is dropped; the
        // answer is this reading's outcome ("consistent" home, "inconsistent" away, "not-checked" on a checked-in trip or
        // without a Home City), while the day keeps the higher-precedence one. Nothing is flagged, restricted or changed.
        api.MapPost("/me/home-check", async (LocationCheck? reading, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            if (await LimitedAsync(db, me.AccountId, now, cancellationToken)) return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyAttempts, "That's a lot of location checks. Try again later.");
            var device = Reading(reading);
            if (!ArrivalPolicy.Usable(device, now)) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LocationUnusable, "We couldn't use your location. Try again with location turned on.");
            var outcome = await HomeCityDayRecorder.RecordAsync(db, me, device!, now, HomeCityCheckSettings.Read(configuration), cancellationToken);
            var wire = outcome switch { HomeCityDayOutcome.Home => "consistent", HomeCityDayOutcome.Away => "inconsistent", _ => "not-checked" };
            Audit.Record(db, now, me.AccountId, "account", "home-check.recorded", "account", me.AccountId.ToString(), wire);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new HomeCheckView(wire));
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
    }

    /// <summary>How a traveller stands to a destination local (D142, D149): modes they may browse there and the trip that lets them start contact.</summary>
    public sealed record TravellerReach(IReadOnlySet<string> SearchModes, IReadOnlyDictionary<string, Guid> ArrivedTrips);

    /// <summary>
    /// The viewer's live, searchable trips to the person's market (TravellerSearch.Check), in the shared modes that market
    /// offers: browsing for each, and contact where the trip's arrival is confirmed (ArrivalPolicy.MayInitiate). A bot
    /// counts as local where it is assigned, and (D352) so does a traveller checked in there with a visible trip. Nothing here
    /// opens another market without a trip (Q41 rule 8).
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
        // D352: a traveller checked in to the destination with a visible trip is there too, as Explore shows them.
        var personHere = (await MarketEndpoints.ActiveTripsAsync(db, [person.Profile.AccountId], now, cancellationToken))
            .GetValueOrDefault(person.Profile.AccountId) is { Trip: { Visible: true } theirs } ? theirs.ArrivalMarketId : null;
        foreach (var x in trips.OrderByDescending(x => x.t.ArrivedAt is not null).ThenBy(x => x.t.Arrival))
        {
            var market = PlaceNames.MarketOf(x.c, zones);
            var state = TravelPolicy.State(new TripFacts(x.t, x.c.TimeZone), [], now);
            if (TravellerSearch.Check(x.t, state, market, TravelPolicy.LocalToday(x.c.TimeZone, now), viewer.HomeMarketId) != SearchRefusal.None) continue;
            if (!MarketAccess.SameMarket(market!.Id, person.Profile.HomeMarketId, person.Profile.Bot, bots) && personHere != market.Id) continue;
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

    /// <summary>Location checks are rate limited per member (D164): arrival and home checks together, ten an hour.</summary>
    private static async Task<bool> LimitedAsync(SwoovrDbContext db, Guid me, DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.AuditEntries.CountAsync(a => a.ActorAccountId == me && (a.Action == "trip.arrived" || a.Action == "trip.arrival-refused" || a.Action == "home-check.recorded")
            && a.At > now.AddHours(-1), cancellationToken) >= ArrivalPolicy.ChecksPerHour;
}
