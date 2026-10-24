using Swoovr.Domain;

/// <summary>Q17 and D109–D111 Travel rules: destination-local dates, the 14-day window, itineraries, overlap, expiry, markets.</summary>
static class TravelChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Travel check failed: " + what); checks++; }
        DateOnly D(string s) => DateOnly.Parse(s);
        Trip T(string arrival, string departure, bool visible = true, Guid? itinerary = null) =>
            new() { Id = Guid.NewGuid(), Arrival = D(arrival), Departure = D(departure), Visible = visible, ItineraryId = itinerary };
        const string adelaide = "Australia/Adelaide", la = "America/Los_Angeles";

        // Dates are the destination's calendar dates: 2026-10-24T14:00Z is already the 25th in Adelaide, still the 24th in LA.
        var now = DateTimeOffset.Parse("2026-10-24T14:00:00Z");
        Expect(TravelPolicy.LocalToday(adelaide, now) == D("2026-10-25") && TravelPolicy.LocalToday(la, now) == D("2026-10-24"), "local today per time zone");
        Expect(TravelPolicy.LocalToday("Not/AZone", now) == D("2026-10-24"), "an unknown zone falls back to UTC");
        Expect(TravelPolicy.CheckDates(D("2026-11-01"), D("2026-11-05"), adelaide, now) == TripError.None, "an ordinary trip");
        Expect(TravelPolicy.CheckDates(D("2026-11-05"), D("2026-11-01"), adelaide, now) == TripError.DepartureBeforeArrival, "departure before arrival");
        Expect(TravelPolicy.CheckDates(D("2026-11-05"), D("2026-11-05"), adelaide, now) == TripError.None, "a one-day trip");
        Expect(TravelPolicy.CheckDates(D("2026-10-20"), D("2026-10-24"), adelaide, now) == TripError.AlreadyOver, "departed yesterday in Adelaide");
        Expect(TravelPolicy.CheckDates(D("2026-10-20"), D("2026-10-24"), la, now) == TripError.None, "the same dates are still current in LA");
        Expect(TravelPolicy.CheckDates(D("2026-10-20"), D("2026-10-30"), adelaide, now) == TripError.None, "already travelling");
        Expect(TravelPolicy.CheckDates(D("2028-10-26"), D("2028-10-30"), adelaide, now) == TripError.TooFarAhead, "more than two years ahead");
        Expect(TravelPolicy.CheckDates(D("2026-11-01"), D("2027-11-02"), adelaide, now) == TripError.TooLong, "longer than a year");

        // The window: a lone trip opens 14 days before its own arrival, in the destination's time zone.
        TripState State(Trip trip, string zone = adelaide, DateTimeOffset? at = null, params Trip[] group) =>
            TravelPolicy.State(new TripFacts(trip, zone), group.Select(g => new TripFacts(g, zone)).ToList(), at ?? now);
        Expect(State(T("2026-11-08", "2026-11-10")) == TripState.Visible, "arrival 14 days after local today opens");
        Expect(State(T("2026-11-09", "2026-11-10")) == TripState.Planned, "15 days ahead is not open yet");
        Expect(State(T("2026-11-08", "2026-11-10"), la) == TripState.Planned, "the same arrival is 15 days away in LA");
        Expect(State(T("2026-11-01", "2026-11-03", visible: false)) == TripState.Planned, "the switch off keeps it planned");
        Expect(State(T("2026-10-20", "2026-10-25")) == TripState.Visible, "the departure day itself still counts");
        Expect(State(T("2026-10-20", "2026-10-24")) == TripState.Expired, "expires after the local departure date");
        Expect(State(T("2026-10-20", "2026-10-24"), la) == TripState.Visible, "not yet expired where it is still the 24th");
        Expect(State(new Trip { Arrival = D("2026-11-01"), Departure = D("2026-11-02"), Visible = true, CancelledAt = now }) == TripState.Cancelled, "cancelled");

        // D109: itineraries are back to back with gaps of 14 days or less; every stop opens with the first.
        var group = Guid.NewGuid();
        var first = T("2026-11-05", "2026-11-10", itinerary: group);
        var second = T("2026-11-10", "2026-11-20", itinerary: group); // shares the changeover day
        var third = T("2026-12-04", "2026-12-08", itinerary: group); // 14-day gap
        Expect(TravelPolicy.BackToBack([first, second, third]), "back to back, changeover day and a 14-day gap");
        Expect(TravelPolicy.BackToBack([third, first, second]), "order of entry doesn't matter");
        Expect(!TravelPolicy.BackToBack([first, T("2026-12-06", "2026-12-08")]), "a 26-day gap");
        Expect(TravelPolicy.BackToBack([first, T("2026-12-05", "2026-12-08"), second, T("2026-11-25", "2026-12-04")]), "a chain entered out of order closes its gaps");
        Expect(!TravelPolicy.BackToBack([first, T("2026-11-09", "2026-11-12")]), "a stop that arrives before the last one leaves");
        Expect(TravelPolicy.BackToBack([first, new Trip { Arrival = D("2026-11-07"), Departure = D("2026-11-09"), CancelledAt = now }]), "cancelled stops don't count");
        Expect(State(third, group: [first, second, third]) == TripState.Visible, "a later stop opens once the first is within 14 days");
        Expect(State(third) == TripState.Planned, "the same trip alone waits for its own window");
        Expect(TravelPolicy.OpensOn(new TripFacts(third, adelaide), [new(first, adelaide), new(second, adelaide), new(third, adelaide)]) == D("2026-10-22"), "opens with the first stop");
        Expect(State(T("2026-11-12", "2026-11-14", itinerary: group), group: [T("2026-11-12", "2026-11-14", itinerary: group)]) == TripState.Planned,
            "an itinerary whose first stop is 18 days away stays planned");

        // Overlap (D109): refused only against another visible, live trip; the changeover day is shared.
        var visible = new TripFacts(T("2026-11-01", "2026-11-05"), adelaide);
        Expect(TravelPolicy.OverlapsVisible(T("2026-11-04", "2026-11-08"), [visible], now), "overlapping dates");
        Expect(TravelPolicy.OverlapsVisible(T("2026-11-02", "2026-11-03"), [visible], now), "inside another trip");
        Expect(!TravelPolicy.OverlapsVisible(T("2026-11-05", "2026-11-08"), [visible], now), "sharing the changeover day");
        Expect(!TravelPolicy.OverlapsVisible(T("2026-10-28", "2026-11-01"), [visible], now), "ending on the other's arrival day");
        Expect(TravelPolicy.OverlapsVisible(T("2026-11-01", "2026-11-05"), [visible], now), "the same dates");
        Expect(!TravelPolicy.OverlapsVisible(T("2026-11-04", "2026-11-08"), [new(T("2026-11-01", "2026-11-05", visible: false), adelaide)], now), "a hidden trip doesn't block");
        Expect(!TravelPolicy.OverlapsVisible(T("2026-10-22", "2026-11-08"), [new(T("2026-10-20", "2026-10-24"), adelaide)], now), "an expired trip doesn't block");
        Expect(!TravelPolicy.OverlapsVisible(visible.Trip, [visible], now), "a trip never overlaps itself");

        // Markets (D110) and who sees whom (Q17 rules 22–23).
        var centre = new GeoCity { GeoNameId = 2078025, Name = "Adelaide", Latitude = -34.92866, Longitude = 138.59863 };
        var active = new Market { Id = "adelaide", Name = "Adelaide", Active = true, CentreGeoNameId = 2078025, RadiusKm = 50 };
        (Market, GeoCity)[] markets = [(active, centre)];
        GeoCity At(double lat, double lon) => new() { Latitude = lat, Longitude = lon };
        Expect(PlaceNames.ActiveMarketOf(At(-34.60251, 138.74902), markets) == active, "Gawler (about 37 km) is in Adelaide");
        Expect(PlaceNames.ActiveMarketOf(At(-35.55042, 138.62154), markets) is null, "Victor Harbor (about 69 km) is not");
        Expect(PlaceNames.ActiveMarketOf(At(-37.814, 144.96332), markets) is null, "Melbourne is not");
        Expect(PlaceNames.ActiveMarketOf(At(-34.60251, 138.74902), [(new Market { Id = "adelaide", Active = false, RadiusKm = 50 }, centre)]) is null, "an inactive market has no locals");
        Expect(PlaceNames.ActiveMarketOf(null, markets) is null, "no home city, no market");
        Expect(Math.Abs(PlaceNames.DistanceKm(-34.92866, 138.59863, -37.814, 144.96332) - 654) < 10, "Adelaide to Melbourne is about 654 km");
        Expect(TravelPolicy.ShownToLocal(TripState.Visible, active, active, null), "a visible trip to the viewer's market from elsewhere");
        Expect(!TravelPolicy.ShownToLocal(TripState.Planned, active, active, null), "not before the window");
        Expect(!TravelPolicy.ShownToLocal(TripState.Expired, active, active, null), "not after expiry");
        Expect(!TravelPolicy.ShownToLocal(TripState.Visible, active, null, null), "the viewer must be local (travellers get no local feed)");
        Expect(!TravelPolicy.ShownToLocal(TripState.Visible, active, active, active), "a fellow local is not visiting");
        Expect(!TravelPolicy.ShownToLocal(TripState.Visible, null, active, null), "a trip to a city outside any active market");
        Expect(!TravelPolicy.ShownToLocal(TripState.Visible, active, new Market { Id = "melbourne", Active = true }, null), "another market's locals");

        // Names and labels (D111).
        Expect(PlaceNames.Key(" Zürich ") == "zurich" && PlaceNames.Key("SÃO PAULO") == PlaceNames.Key("são paulo"), "folded keys");
        Expect(PlaceNames.Label(new GeoCity { Name = "Adelaide", Admin1Name = "South Australia", CountryName = "Australia" }) == "Adelaide, South Australia, Australia", "label");
        Expect(PlaceNames.Label(new GeoCity { Name = "Singapore", Admin1Name = null, CountryName = "Singapore" }) == "Singapore, Singapore", "no region");
        Expect(PlaceNames.Label(new GeoCity { Name = "Tokyo", Admin1Name = "Tokyo", CountryName = "Japan" }) == "Tokyo, Japan", "a region that repeats the city");

        Console.WriteLine($"PASS: {checks} Travel cases (destination-local dates and expiry, 14-day window, itineraries back to back with every stop opening together, overlap with the shared changeover day, markets and locals, asymmetric discovery, GeoNames labels).");
    }
}
