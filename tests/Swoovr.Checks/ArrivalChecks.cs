using Swoovr.Domain;

/// <summary>
/// Slice T rules (D142, D149, D150, D164): confirm-arrival from a fresh device reading, contact only after arrival,
/// Traveller Search only through one's own live, visible trip within 14 days, and home-location checks that flag for
/// review but never punish.
/// </summary>
static class ArrivalChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Arrival check failed: " + what); checks++; }
        var now = DateTimeOffset.Parse("2026-10-26T03:00:00Z");
        var today = DateOnly.Parse("2026-10-26");
        var adelaide = new Market { Id = "adelaide", Name = "Adelaide", Status = MarketStatus.Beta };
        var darwin = new Market { Id = "darwin", Name = "Darwin", Status = MarketStatus.Beta };
        var zones = new List<MarketZone>
        {
            new(adelaide, new MarketArea { MarketId = "adelaide", CentreGeoNameId = 2078025, RadiusKm = 50 }, new GeoCity { GeoNameId = 2078025, Latitude = -34.92866, Longitude = 138.59863 }),
            new(darwin, new MarketArea { MarketId = "darwin", CentreGeoNameId = 2073124, RadiusKm = 30 }, new GeoCity { GeoNameId = 2073124, Latitude = -12.46113, Longitude = 130.84185 }),
        };
        LocationReading At(double lat, double lon, double accuracy = 25, int secondsAgo = 5) => new(lat, lon, accuracy, now.AddSeconds(-secondsAgo));
        var cbd = At(-34.9285, 138.6007);
        var glenelg = At(-34.98, 138.51);
        var melbourne = At(-37.8136, 144.9631);
        Trip T(string arrival = "2026-10-26", string departure = "2026-10-30", bool visible = true) =>
            new() { Id = Guid.NewGuid(), Arrival = DateOnly.Parse(arrival), Departure = DateOnly.Parse(departure), Visible = visible };

        // Usable readings only (rule 3, D164): real, fresh and accurate enough.
        Expect(ArrivalPolicy.Usable(cbd, now), "a fresh, accurate reading");
        Expect(!ArrivalPolicy.Usable(null, now), "no reading");
        Expect(!ArrivalPolicy.Usable(cbd with { Latitude = double.NaN }, now) && !ArrivalPolicy.Usable(cbd with { Longitude = double.PositiveInfinity }, now), "not a number");
        Expect(!ArrivalPolicy.Usable(cbd with { Latitude = 91 }, now) && !ArrivalPolicy.Usable(cbd with { Longitude = -181 }, now), "out of range");
        Expect(!ArrivalPolicy.Usable(At(0, 0), now), "null island is a failed fix, not a place");
        Expect(!ArrivalPolicy.Usable(cbd with { AccuracyMetres = 0 }, now) && !ArrivalPolicy.Usable(cbd with { AccuracyMetres = -5 }, now), "no accuracy");
        Expect(ArrivalPolicy.Usable(cbd with { AccuracyMetres = 2000 }, now) && !ArrivalPolicy.Usable(cbd with { AccuracyMetres = 2001 }, now), "2 km or better");
        Expect(ArrivalPolicy.Usable(At(-34.9, 138.6, secondsAgo: 120), now) && !ArrivalPolicy.Usable(At(-34.9, 138.6, secondsAgo: 121), now), "at most two minutes old");
        Expect(ArrivalPolicy.Usable(At(-34.9, 138.6, secondsAgo: -5), now) && !ArrivalPolicy.Usable(At(-34.9, 138.6, secondsAgo: -6), now), "five seconds of clock skew, no more");

        // Inside the trip's market (any of its areas), never another one.
        Expect(ArrivalPolicy.InMarket(cbd, zones.Where(z => z.Market.Id == "adelaide")) && ArrivalPolicy.InMarket(glenelg, zones.Where(z => z.Market.Id == "adelaide")), "Adelaide CBD and Glenelg are in Adelaide");
        Expect(!ArrivalPolicy.InMarket(melbourne, zones.Where(z => z.Market.Id == "adelaide")), "Melbourne isn't");
        Expect(!ArrivalPolicy.InMarket(cbd, zones.Where(z => z.Market.Id == "darwin")), "Adelaide isn't Darwin");
        Expect(!ArrivalPolicy.InMarket(cbd, []), "no areas, no market");

        // Confirm-arrival, in order: live trip in an open market, date reached, usable reading, inside.
        ArrivalRefusal Confirm(TripState state = TripState.Visible, Market? market = null, string localToday = "2026-10-26", string arrival = "2026-10-26", LocationReading? reading = null) =>
            ArrivalPolicy.Confirm(state, market ?? adelaide, DateOnly.Parse(localToday), DateOnly.Parse(arrival), reading, zones, now);
        Expect(Confirm(reading: cbd) == ArrivalRefusal.None, "arrived on the arrival date inside the market");
        Expect(Confirm(state: TripState.Planned, reading: cbd) == ArrivalRefusal.None, "a hidden trip can still arrive (visibility is the member's choice)");
        Expect(Confirm(localToday: "2026-10-25", reading: cbd) == ArrivalRefusal.TooEarly, "the day before arrival at the destination");
        Expect(Confirm(localToday: "2026-10-25", reading: null) == ArrivalRefusal.TooEarly, "too early is said before asking for a location");
        Expect(Confirm(localToday: "2026-10-29", reading: cbd) == ArrivalRefusal.None, "mid-trip");
        Expect(Confirm(state: TripState.Cancelled, reading: cbd) == ArrivalRefusal.TripNotLive && Confirm(state: TripState.Expired, reading: cbd) == ArrivalRefusal.TripNotLive, "cancelled or over");
        Expect(Confirm(market: new Market { Id = "adelaide", Status = MarketStatus.Suspended }, reading: cbd) == ArrivalRefusal.TripNotLive, "a suspended market");
        Expect(ArrivalPolicy.Confirm(TripState.Visible, null, today, today, cbd, zones, now) == ArrivalRefusal.TripNotLive, "outside every market");
        Expect(Confirm(reading: null) == ArrivalRefusal.Unusable && Confirm(reading: cbd with { AccuracyMetres = 5000 }) == ArrivalRefusal.Unusable, "no usable reading (IP alone never confirms)");
        Expect(Confirm(reading: melbourne) == ArrivalRefusal.Outside, "still at home in Melbourne");
        Expect(Confirm(market: darwin, reading: cbd) == ArrivalRefusal.Outside, "a reading in another market doesn't count");

        // State, contact and resets.
        var trip = T();
        Expect(ArrivalPolicy.State(trip) == ArrivalState.Upcoming, "upcoming before any check");
        trip.ArrivalOutcome = ArrivalOutcome.Outside;
        Expect(ArrivalPolicy.State(trip) == ArrivalState.Unconfirmed, "unconfirmed after a failed check");
        trip.ArrivalOutcome = ArrivalOutcome.NotYet;
        Expect(ArrivalPolicy.State(trip) == ArrivalState.Upcoming, "a too-early check leaves it upcoming");
        Expect(!ArrivalPolicy.MayInitiate(trip, TripState.Visible, adelaide), "no contact before arrival");
        (trip.ArrivedAt, trip.ArrivalMarketId, trip.ArrivalOutcome) = (now, "adelaide", ArrivalOutcome.Confirmed);
        Expect(ArrivalPolicy.State(trip) == ArrivalState.Arrived && ArrivalPolicy.MayInitiate(trip, TripState.Visible, adelaide), "contact once arrived");
        Expect(ArrivalPolicy.MayInitiate(trip, TripState.Planned, adelaide), "hiding the trip doesn't undo an arrival");
        Expect(!ArrivalPolicy.MayInitiate(trip, TripState.Expired, adelaide) && !ArrivalPolicy.MayInitiate(trip, TripState.Cancelled, adelaide), "the trip's end ends contact initiation");
        Expect(!ArrivalPolicy.MayInitiate(trip, TripState.Visible, darwin), "an arrival counts only for its market");
        Expect(!ArrivalPolicy.MayInitiate(trip, TripState.Visible, new Market { Id = "adelaide", Status = MarketStatus.Suspended }), "a suspended market stops it");
        Expect(ArrivalPolicy.ResetsArrival(trip, 2073124, trip.Arrival, trip.Departure), "a new destination needs a new check");
        Expect(ArrivalPolicy.ResetsArrival(trip, trip.GeoNameId, trip.Arrival.AddDays(-3), trip.Departure) && ArrivalPolicy.ResetsArrival(trip, trip.GeoNameId, trip.Arrival, trip.Departure.AddDays(5)), "new dates need a new check");
        Expect(!ArrivalPolicy.ResetsArrival(trip, trip.GeoNameId, trip.Arrival, trip.Departure), "visibility alone keeps it");

        // Traveller Search (D142): own live, visible trip, open market, from 14 days before arrival, never one's own market.
        SearchRefusal Search(Trip t, TripState state = TripState.Visible, Market? market = null, string localToday = "2026-10-26", string? home = "melbourne") =>
            TravellerSearch.Check(t, state, market ?? adelaide, DateOnly.Parse(localToday), home);
        Expect(Search(T("2026-11-09", "2026-11-12")) == SearchRefusal.None, "exactly 14 days before arrival");
        Expect(Search(T("2026-11-10", "2026-11-12")) == SearchRefusal.TooEarly, "15 days before");
        Expect(Search(T()) == SearchRefusal.None && Search(T(), home: null) == SearchRefusal.None, "on arrival day, with or without a home market");
        Expect(Search(T(visible: false), TripState.Planned) == SearchRefusal.Hidden, "a hidden trip doesn't search (no one-way looking)");
        Expect(Search(new Trip { Arrival = today, Departure = today, Visible = true, CancelledAt = now }, TripState.Cancelled) == SearchRefusal.TripNotLive, "a cancelled trip");
        Expect(Search(T("2026-10-20", "2026-10-24"), TripState.Expired) == SearchRefusal.TripNotLive, "an ended trip");
        Expect(Search(T(), market: new Market { Id = "perth", Status = MarketStatus.Planned }) == SearchRefusal.TripNotLive, "a market that isn't open");
        Expect(TravellerSearch.Check(T(), TripState.Visible, null, today, "melbourne") == SearchRefusal.TripNotLive, "outside every market (no arbitrary remote markets)");
        Expect(Search(T(), home: "adelaide") == SearchRefusal.OwnMarket, "locals use Explore");

        // The market rules with travellers added.
        Expect(MarketAccess.ProfileOpens(false, false, false, searching: true) && !MarketAccess.ProfileOpens(false, false, false), "a searching traveller may look; nobody else from outside");
        Expect(!MarketAccess.MayRequest(false, false, false) && MarketAccess.MayRequest(false, false, false, arrivedInMode: true), "contact after arrival only");
        Expect(MarketAccess.MayRequest(true, false, false) && MarketAccess.MayRequest(false, true, false) && MarketAccess.MayRequest(false, false, true), "earlier rules unchanged");

        // Home-location consistency (D150): three inconsistent device days in 30 with no consistent check after them.
        HomeLocationCheck C(int daysAgo, HomeConsistency outcome, LocationMethod method = LocationMethod.Device, int hour = 0) =>
            new() { CheckedAt = now.AddDays(-daysAgo).AddHours(-hour), Outcome = outcome, Method = method, HomeMarketId = "adelaide" };
        var bad = HomeConsistency.Inconsistent; var good = HomeConsistency.Consistent;
        Expect(HomeLocationPolicy.Judge(true) == good && HomeLocationPolicy.Judge(false) == bad, "inside is consistent");
        Expect(!HomeLocationPolicy.ShouldFlag([], now), "no checks");
        Expect(!HomeLocationPolicy.ShouldFlag([C(1, bad), C(2, bad)], now), "two days isn't enough");
        Expect(HomeLocationPolicy.ShouldFlag([C(1, bad), C(2, bad), C(3, bad)], now), "three days is");
        Expect(!HomeLocationPolicy.ShouldFlag([C(1, bad), C(1, bad, hour: 2), C(1, bad, hour: 4)], now), "three checks on one day count once");
        Expect(!HomeLocationPolicy.ShouldFlag([C(5, bad), C(4, bad), C(3, bad), C(2, good)], now), "a consistent check after them clears it");
        Expect(HomeLocationPolicy.ShouldFlag([C(9, good), C(5, bad), C(4, bad), C(3, bad)], now), "a consistent check before them doesn't");
        Expect(!HomeLocationPolicy.ShouldFlag([C(40, bad), C(35, bad), C(2, bad)], now), "only the last 30 days");
        Expect(!HomeLocationPolicy.ShouldFlag([C(1, bad, LocationMethod.ApproximateIp), C(2, bad, LocationMethod.ApproximateIp), C(3, bad, LocationMethod.ApproximateIp)], now), "IP alone never flags (D153)");
        Expect(!HomeLocationPolicy.ShouldFlag([C(1, bad), C(2, bad, LocationMethod.ApproximateIp), C(3, bad)], now), "IP doesn't make up the count");

        Console.WriteLine($"PASS: arrival, Traveller Search and home-location checks ({checks}).");
    }
}
