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
/// Travel (Q17, D107, D109–D111, D120–D122): GeoNames city search, the member's own trips and itineraries, and the
/// destination feed in which locals of an open market (D125) see visiting travellers. Travel is asymmetric: there is no
/// feed of locals for travellers, and traveller search is not built (Q17 rule 36; its prerequisite is a qualifying trip
/// for the destination's GeoNames id). Coordinates, distances and radii never leave the server. The audit log holds
/// trip ids and visibility only, never cities or dates.
/// </summary>
public static class TravelEndpoints
{
    public const int PageSize = 50, SearchLimit = 10, TripWritesPerHour = 30;

    public static PlaceView Place(GeoCity city) => new(city.GeoNameId, city.Name,
        city.Admin1Name is { Length: > 0 } region && region != city.Name ? region : null, city.CountryName, PlaceNames.Label(city));

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        // Autocomplete: prefix match on any spelling, largest places first, "City, Region, Country" (D111).
        api.MapGet("/places", async (string? q, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var key = PlaceNames.Key(q ?? "");
            if (key.Length is < 2 or > 60 || key.Any(char.IsControl))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Type 2 to 60 letters of a city.");
            var pattern = key.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            var db = accounts.Db!;
            var cities = await db.GeoCities
                .Where(c => db.GeoCityNames.Any(n => n.GeoNameId == c.GeoNameId && EF.Functions.Like(n.Key, pattern, @"\")))
                .OrderByDescending(c => c.Population).ThenBy(c => c.GeoNameId).Take(SearchLimit).ToListAsync(cancellationToken);
            return Results.Ok(cities.Select(Place).ToList());
        });

        api.MapGet("/me/trips", async (int? page, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (page is < 1 or > 10000) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Check the page number.");
            var (me, db) = (current!.Profile!.AccountId, accounts.Db!);
            var rows = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId where t.AccountId == me
                              orderby t.Arrival, t.Departure, t.Id select new { t, c })
                .Skip(((page ?? 1) - 1) * PageSize).Take(PageSize + 1).ToListAsync(cancellationToken);
            var markets = await MarketEndpoints.ZonesAsync(db, cancellationToken);
            var siblings = await SiblingsAsync(db, rows.Select(r => r.t).ToList(), cancellationToken);
            var now = clock.GetUtcNow();
            return Results.Ok(new TripPage(rows.Take(PageSize).Select(r => View(r.t, r.c, siblings, markets, now)).ToList(), page ?? 1, rows.Count > PageSize));
        });

        api.MapPost("/me/itineraries", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!.AccountId, accounts.Db!, clock.GetUtcNow());
            var itinerary = new Itinerary { Id = Guid.NewGuid(), AccountId = me, CreatedAt = now };
            db.Itineraries.Add(itinerary);
            Audit.Record(db, now, me, "account", "itinerary.created", "itinerary", itinerary.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Created($"/api/v1/me/itineraries/{itinerary.Id}", new ItineraryView(itinerary.Id));
        });

        api.MapPost("/me/trips", (TripUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
            SaveAsync(null, update, user, accounts, clock, cancellationToken));
        api.MapPut("/me/trips/{id:guid}", (Guid id, TripUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
            SaveAsync(id, update, user, accounts, clock, cancellationToken));

        api.MapPost("/me/trips/{id:guid}/cancel", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!.AccountId, accounts.Db!, clock.GetUtcNow());
            var trip = await db.Trips.SingleOrDefaultAsync(t => t.Id == id && t.AccountId == me, cancellationToken);
            if (trip is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That trip isn't there.");
            if (trip.CancelledAt is null)
            {
                (trip.CancelledAt, trip.UpdatedAt) = (now, now);
                Audit.Record(db, now, me, "account", "trip.cancelled", "trip", trip.Id.ToString());
                await db.SaveChangesAsync(cancellationToken);
            }
            return Results.NoContent();
        });

        api.MapDelete("/me/trips/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db) = (current!.Profile!.AccountId, accounts.Db!);
            var trip = await db.Trips.SingleOrDefaultAsync(t => t.Id == id && t.AccountId == me, cancellationToken);
            if (trip is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That trip isn't there.");
            db.Trips.Remove(trip);
            Audit.Record(db, clock.GetUtcNow(), me, "account", "trip.removed", "trip", trip.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        // The destination feed for locals (Q17 rules 22–28): visible travellers in modes both people have on.
        api.MapGet("/travel/visitors", async (string? mode, int? page, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (page is < 1 or > 1000) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest, "Check the page number.");
            var (viewer, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            var selected = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == mode && m.Available, cancellationToken);
            if (selected is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
            var own = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == viewer.AccountId && m.ModeId == selected.Id, cancellationToken);
            if (own is not { Enabled: true })
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.ModeNotEnabled, "Turn this mode on to see who's visiting.");
            // Travellers get no feed of locals: only a local of an open market offering the mode sees visitors (Q17 rule 23, D125).
            var (market, refused) = await MarketEndpoints.LocalAsync(db, viewer, selected.Id, cancellationToken);
            if (refused is not null)
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.NotLocal, "Visitors show to people whose home city is in a Swoovr market.");
            var visits = await VisitsToAsync(db, configuration, viewer.AccountId, market!, await MarketEndpoints.ZonesAsync(db, cancellationToken), selected.Id, now, cancellationToken);
            // D137: the viewer's visitor visibility (current, upcoming, both or neither); D138, D140: the same reciprocal
            // rules as Explore. A stay has started once the destination's arrival date is reached.
            var filter = await db.ModeFilters.FindAsync([viewer.AccountId, selected.Id], cancellationToken);
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            var facts = await ProfileEndpoints.FactsAsync(db, [viewer.AccountId, .. visits.Select(v => v.Person.Profile.AccountId)], selected.Id, today, cancellationToken);
            visits = visits.Where(v => MatchPolicy.ShowsVisitor(filter?.Visitors ?? VisitorVisibility.Both, TravelPolicy.LocalToday(v.City.TimeZone, now) >= v.Trip.Arrival)
                && MatchPolicy.Reciprocal(selected.Id, facts[viewer.AccountId], facts[v.Person.Profile.AccountId])).ToList();
            var p = page ?? 1;
            var shown = visits.Skip((p - 1) * PageSize).Take(PageSize + 1).ToList();
            var chips = await ChipsAsync(db, shown.Select(v => v.Person.Profile.AccountId).ToList(), selected.Id, cancellationToken);
            var order = await ProfileEndpoints.BadgeOrderAsync(db, cancellationToken);
            return Results.Ok(new VisitorPage(selected.Id, market!.Name, shown.Take(PageSize).Select(v => new VisitorCard(v.Person.Profile.PublicId,
                v.Person.Profile.DisplayName!, ProfileEndpoints.Badges(v.Person.Profile, order), chips.GetValueOrDefault(v.Person.Profile.AccountId, []),
                Visit(v.Trip, v.City))).ToList(), p, shown.Count > PageSize));
        });
    }

    public static VisitView Visit(Trip trip, GeoCity city) => new(trip.Id, city.Name, city.CountryName, trip.Arrival, trip.Departure);

    public sealed record Visitor(ProfileEndpoints.Row Person, Trip Trip, GeoCity City);

    /// <summary>
    /// The visiting travellers a local of the given market may see in one mode, earliest arrival first, one entry per
    /// traveller. Uses the same account, mode, bot and block rules as Explore, then TravelPolicy for the trip itself.
    /// Only, when given, limits the search to one traveller (a profile or request check).
    /// </summary>
    public static async Task<List<Visitor>> VisitsToAsync(SwoovrDbContext db, IConfiguration configuration, Guid viewer, Market market,
        List<MarketZone> zones, string modeId, DateTimeOffset now, CancellationToken cancellationToken, Guid? only = null)
    {
        var areas = zones.Where(z => z.Market.Id == market.Id).ToList();
        if (areas.Count == 0) return [];
        // A coarse box around the market's areas in the database, then the exact radius in code; neither leaves the server.
        double DLat(MarketZone z) => z.Area.RadiusKm / 110.0 + 0.1;
        double DLon(MarketZone z) => z.Area.RadiusKm / (111.0 * Math.Max(0.1, Math.Cos(z.Centre.Latitude * Math.PI / 180))) + 0.1;
        var (south, north) = (areas.Min(z => z.Centre.Latitude - DLat(z)), areas.Max(z => z.Centre.Latitude + DLat(z)));
        var (west, east) = (areas.Min(z => z.Centre.Longitude - DLon(z)), areas.Max(z => z.Centre.Longitude + DLon(z)));
        var yesterday = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var candidates = await (from t in db.Trips
                                join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                                join person in ProfileEndpoints.Candidates(db, configuration, viewer) on t.AccountId equals person.Profile.AccountId
                                where t.Visible && t.CancelledAt == null && t.Departure >= yesterday && (only == null || t.AccountId == only)
                                      && c.Latitude >= south && c.Latitude <= north && c.Longitude >= west && c.Longitude <= east
                                      && db.MemberModes.Any(m => m.AccountId == t.AccountId && m.ModeId == modeId && m.Enabled && m.Visible)
                                select new { t, c, person }).ToListAsync(cancellationToken);
        var siblings = await SiblingsAsync(db, candidates.Select(x => x.t).ToList(), cancellationToken);
        var bots = await ProfileEndpoints.BotModesAsync(db, candidates.Where(x => x.person.Profile.Bot).Select(x => x.person.Profile.AccountId).Distinct().ToList(), cancellationToken);
        var mode = await db.ConnectionModes.SingleAsync(m => m.Id == modeId, cancellationToken);
        var on = new MemberMode { ModeId = modeId, Enabled = true, Visible = true };
        return candidates
            .Where(x => DiscoveryEligibility.ShownIn(modeId, mode, on, on, x.person.Candidate(bots))
                && TravelPolicy.ShownToLocal(TravelPolicy.State(Facts(x.t, x.c), siblings.GetValueOrDefault(x.t.ItineraryId ?? Guid.Empty, []), now),
                    PlaceNames.OpenMarketOf(x.c, zones), market.Id, x.person.Profile.HomeMarketId))
            .GroupBy(x => x.person.Profile.AccountId).Select(g => g.OrderBy(x => x.t.Arrival).First())
            .OrderBy(x => x.t.Arrival).ThenBy(x => x.person.Profile.PublicId)
            .Select(x => new Visitor(x.person, x.t, x.c)).ToList();
    }

    private static async Task<IResult> SaveAsync(Guid? id, TripUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken)
    {
        var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
        if (denied is not null) return denied;
        var (me, db, now) = (current!.Profile!.AccountId, accounts.Db!, clock.GetUtcNow());
        Trip? trip = null;
        if (id is { } existing)
        {
            trip = await db.Trips.SingleOrDefaultAsync(t => t.Id == existing && t.AccountId == me, cancellationToken);
            if (trip is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That trip isn't there.");
            if (trip.CancelledAt is not null) return Problem(StatusCodes.Status409Conflict, ProblemCodes.TripCancelled, "A cancelled trip can't be changed.");
        }
        // Q17 rule 43: unlimited trips, but writes are rate limited.
        if (await db.AuditEntries.CountAsync(a => a.ActorAccountId == me && (a.Action == "trip.created" || a.Action == "trip.updated") && a.At > now.AddHours(-1), cancellationToken) >= TripWritesPerHour)
            return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyTrips, "That's a lot of trip changes. Try again later.");
        var city = await db.GeoCities.FindAsync([update.CityId], cancellationToken);
        if (city is null) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownPlace, "Pick the city from the list.");
        if (TravelPolicy.CheckDates(update.Arrival, update.Departure, city.TimeZone, now) is not TripError.None and var error)
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidTripDates, error switch
            {
                TripError.DepartureBeforeArrival => "Departure can't be before arrival.",
                TripError.AlreadyOver => "That trip has already ended.",
                TripError.TooFarAhead => "Trips can be planned up to two years ahead.",
                _ => "A trip can be at most a year long.",
            });
        if (update.ItineraryId is { } itinerary && !await db.Itineraries.AnyAsync(i => i.Id == itinerary && i.AccountId == me, cancellationToken))
            return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotAvailable, "That itinerary isn't there.");
        var proposed = new Trip
        {
            Id = trip?.Id ?? Guid.NewGuid(), AccountId = me, GeoNameId = city.GeoNameId, Arrival = update.Arrival, Departure = update.Departure,
            Visible = update.Visible, ItineraryId = update.ItineraryId, CreatedAt = trip?.CreatedAt ?? now, UpdatedAt = now,
        };
        var mine = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                          where t.AccountId == me && t.Id != proposed.Id && t.CancelledAt == null select new { t, c }).ToListAsync(cancellationToken);
        if (proposed.ItineraryId is { } group && !TravelPolicy.BackToBack(mine.Where(x => x.t.ItineraryId == group).Select(x => x.t).Append(proposed)))
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.ItineraryNotBackToBack,
                "Stops in an itinerary run back to back: each arrives on or after the last one leaves, at most 14 days later.");
        if (proposed.Visible && TravelPolicy.OverlapsVisible(proposed, mine.Select(x => Facts(x.t, x.c)), now))
            return Problem(StatusCodes.Status409Conflict, ProblemCodes.TripOverlaps, "Another visible trip overlaps these dates.");
        if (trip is null) db.Trips.Add(trip = proposed);
        else (trip.GeoNameId, trip.Arrival, trip.Departure, trip.Visible, trip.ItineraryId, trip.UpdatedAt) =
            (proposed.GeoNameId, proposed.Arrival, proposed.Departure, proposed.Visible, proposed.ItineraryId, now);
        Audit.Record(db, now, me, "account", id is null ? "trip.created" : "trip.updated", "trip", trip.Id.ToString(), trip.Visible ? "visible" : "hidden");
        await db.SaveChangesAsync(cancellationToken);
        var markets = await MarketEndpoints.ZonesAsync(db, cancellationToken);
        var siblings = await SiblingsAsync(db, [trip], cancellationToken);
        var view = View(trip, city, siblings, markets, now);
        return id is null ? Results.Created($"/api/v1/me/trips/{trip.Id}", view) : Results.Ok(view);
    }

    private static TripFacts Facts(Trip trip, GeoCity city) => new(trip, city.TimeZone);

    private static TripView View(Trip trip, GeoCity city, Dictionary<Guid, List<TripFacts>> siblings, List<MarketZone> markets, DateTimeOffset now)
    {
        var group = siblings.GetValueOrDefault(trip.ItineraryId ?? Guid.Empty, []);
        var facts = Facts(trip, city);
        return new(trip.Id, Place(city), trip.Arrival, trip.Departure, trip.Visible, Wire(TravelPolicy.State(facts, group, now)), trip.ItineraryId,
            TravelPolicy.OpensOn(facts, group), PlaceNames.OpenMarketOf(city, markets) is not null);
    }

    /// <summary>Every live stop of the itineraries these trips belong to, keyed by itinerary.</summary>
    private static async Task<Dictionary<Guid, List<TripFacts>>> SiblingsAsync(SwoovrDbContext db, List<Trip> trips, CancellationToken cancellationToken)
    {
        var groups = trips.Where(t => t.ItineraryId is not null).Select(t => t.ItineraryId!.Value).Distinct().ToList();
        if (groups.Count == 0) return [];
        var rows = await (from t in db.Trips join c in db.GeoCities on t.GeoNameId equals c.GeoNameId
                          where t.ItineraryId != null && groups.Contains(t.ItineraryId.Value) && t.CancelledAt == null select new { t, c }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.t.ItineraryId!.Value).ToDictionary(g => g.Key, g => g.Select(r => Facts(r.t, r.c)).ToList());
    }

    private static async Task<Dictionary<Guid, IReadOnlyList<string>>> ChipsAsync(SwoovrDbContext db, List<Guid> people, string modeId, CancellationToken cancellationToken)
    {
        var rows = await (from chosen in db.MemberModeIntentions join intention in db.ModeIntentions on chosen.IntentionId equals intention.Id
                          where people.Contains(chosen.AccountId) && chosen.ModeId == modeId && intention.Available orderby intention.SortOrder
                          select new { chosen.AccountId, intention.Label }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.AccountId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Label).ToList());
    }
}
