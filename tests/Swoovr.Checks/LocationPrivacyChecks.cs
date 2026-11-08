using Swoovr.Domain;

/// <summary>
/// Location and mode privacy (internal policy §6, §10; #90, #91): distance filters snap to bands so a kilometre-by-kilometre
/// sweep learns nothing finer, rounded band labels, hourly limits, and profile versions as visible at a moment.
/// </summary>
static class LocationPrivacyChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Location privacy check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

        Expect(LocationPrivacy.Snap(null) is null, "no filter stays none");
        Expect(LocationPrivacy.Snap(1) == 5 && LocationPrivacy.Snap(5) == 5 && LocationPrivacy.Snap(6) == 10 && LocationPrivacy.Snap(9) == 10, "small distances snap up");
        Expect(LocationPrivacy.Snap(26) == 50 && LocationPrivacy.Snap(499) == 500 && LocationPrivacy.Snap(900) == 500, "larger ones too, capped");
        Expect(LocationPrivacy.FilterOptions(PlanTier.Free).SequenceEqual([5, 10, 25, 50]), "D347: Free filter choices");
        Expect(LocationPrivacy.FilterOptions(PlanTier.Plus).SequenceEqual([5, 10, 25, 50, 100, 250, 500])
            && LocationPrivacy.FilterOptions(PlanTier.Premium).SequenceEqual(LocationPrivacy.FilterOptions(PlanTier.Plus)), "D347: paid filter choices");
        // The sweep: a person 7.3 km away is in or out for whole bands only.
        var viewer = new MatchFacts(30, new HashSet<Guid>(), new HashSet<Guid>(), false, null, [], -34.9285, 138.6007, false);
        var person = new MatchFacts(30, new HashSet<Guid>(), new HashSet<Guid>(), false, null, [], -34.9285 + 7.3 / 111.0, 138.6007, false);
        var d = MatchPolicy.DistanceKm(viewer, person)!.Value;
        Expect(d is > 7 and < 8, "the fixture is about 7.3 km away");
        var seen = Enumerable.Range(1, 30).Select(km => MatchPolicy.FilterAllows(new ModeFilter { MaxDistanceKm = km, RequiredChips = [], ExcludedChips = [] }, viewer, person, PlanTier.Plus)).ToList();
        Expect(seen.Take(5).All(s => !s) && seen.Skip(5).All(s => s), "filters 1–5 km hide them and 6–30 km all show them: nothing finer than the band");
        // D348 display bands: 0–2, over 2–10, then 10 km steps to 50, then over 50 km.
        Expect(LocationPrivacy.BandFor(0) == DistanceBand.Nearby && LocationPrivacy.BandFor(2) == DistanceBand.Nearby && LocationPrivacy.BandFor(2.01) == DistanceBand.Within10Km
            && LocationPrivacy.BandFor(d) == DistanceBand.Within10Km && LocationPrivacy.BandFor(10) == DistanceBand.Within10Km && LocationPrivacy.BandFor(10.5) == DistanceBand.Km10To20
            && LocationPrivacy.BandFor(25) == DistanceBand.Km20To30 && LocationPrivacy.BandFor(35) == DistanceBand.Km30To40 && LocationPrivacy.BandFor(50) == DistanceBand.Km40To50
            && LocationPrivacy.BandFor(50.1) == DistanceBand.Over50Km && LocationPrivacy.BandFor(900) == DistanceBand.Over50Km, "D348 band boundaries");
        // D348, Q152: hidden unless both show their distance; unknown without both home cities; never a GPS basis from home cities.
        Expect(LocationPrivacy.HomeCityBand(d, false, false) == (DistanceBand.Hidden, null), "hidden by default");
        Expect(LocationPrivacy.HomeCityBand(d, true, false) == (DistanceBand.Hidden, null), "the viewer must show theirs too (Q152)");
        Expect(LocationPrivacy.HomeCityBand(d, false, true) == (DistanceBand.Hidden, null), "the person must allow it");
        Expect(LocationPrivacy.HomeCityBand(null, true, true) == (DistanceBand.Unknown, null), "unknown without both home cities");
        Expect(LocationPrivacy.HomeCityBand(d, true, true) == (DistanceBand.Within10Km, (DistanceBasis?)DistanceBasis.HomeCity), "home-city band with its basis");
        Expect(new MemberProfile() is { AppearInNearby: false, ShowDistance: false }, "D348: both settings are off by default");

        // D348 contract scan: no member-facing response type carries coordinates or a numeric distance. Allowed: the member's
        // own device reading sent to the API (LocationCheck), their own filter setting and its choices, and staff market config.
        var allowed = new HashSet<string> { "LocationCheck.Latitude", "LocationCheck.Longitude", "LocationCheck.AccuracyMetres",
            "FilterView.MaxDistanceKm", "FilterUpdate.MaxDistanceKm", "FilterView.DistanceOptions", "MarketAreaView.RadiusKm" };
        var leaks = typeof(Swoovr.Contracts.ProfileCard).Assembly.GetTypes().Where(t => t.IsPublic)
            .SelectMany(t => t.GetProperties().Select(p => (Name: $"{t.Name}.{p.Name}", p.PropertyType)))
            .Where(p => System.Text.RegularExpressions.Regex.IsMatch(p.Name.Split('.')[1], "Latitude|Longitude|Coordinate|Km$|Metres|Radius|Distance", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                && IsNumber(p.PropertyType) && !allowed.Contains(p.Name))
            .Select(p => p.Name).ToList();
        Expect(leaks.Count == 0, "no coordinates or numeric distances in contracts: " + string.Join(", ", leaks));
        static bool IsNumber(Type t) => (Nullable.GetUnderlyingType(t) ?? t) is var u && (u == typeof(double) || u == typeof(float) || u == typeof(decimal) || u == typeof(int) || u == typeof(long)
            || u.IsGenericType && u.GetGenericArguments().Any(IsNumber));
        Expect(typeof(Swoovr.Contracts.ProfileCard).GetProperty("Distance")?.PropertyType == typeof(Swoovr.Contracts.DistanceView)
            && typeof(Swoovr.Contracts.PersonProfile).GetProperty("Distance")?.PropertyType == typeof(Swoovr.Contracts.DistanceView)
            && typeof(Swoovr.Contracts.ProfileCard).GetProperty("DistanceBand") is null, "cards and profiles carry only a DistanceView");
        Expect(LocationPrivacy.Allowed(9, LocationPrivacy.FilterChangesPerHour) && !LocationPrivacy.Allowed(10, LocationPrivacy.FilterChangesPerHour), "ten filter changes an hour");
        Expect(LocationPrivacy.Allowed(59, LocationPrivacy.SearchesPerHour) && !LocationPrivacy.Allowed(60, LocationPrivacy.SearchesPerHour), "sixty searches an hour");

        // Profile versions.
        var account = Guid.NewGuid();
        var v1 = new ProfileVersion { AccountId = account, ModeId = "core", CapturedAt = now.AddDays(-3), Sha256 = ProfileVersions.Hash("a") };
        var v2 = new ProfileVersion { AccountId = account, ModeId = "core", CapturedAt = now.AddDays(-1), Sha256 = ProfileVersions.Hash("b") };
        var f1 = new ProfileVersion { AccountId = account, ModeId = "friends", CapturedAt = now.AddDays(-2), Sha256 = ProfileVersions.Hash("c") };
        var at = ProfileVersions.VisibleAt([v1, v2, f1], now.AddDays(-1.5));
        Expect(at.Count == 2 && at.Contains(v1) && at.Contains(f1), "the versions visible at the report, not later edits");
        Expect(ProfileVersions.VisibleAt([v1, v2, f1], now).Contains(v2), "the latest now");
        Expect(ProfileVersions.VisibleAt([v2], now.AddDays(-2)).Count == 0, "nothing before the first version");
        Expect(!ProfileVersions.Changed(v2, ProfileVersions.Hash("b")) && ProfileVersions.Changed(v2, ProfileVersions.Hash("B")) && ProfileVersions.Changed(null, [1]), "unchanged content isn't stored again");

        Console.WriteLine($"PASS: location and mode privacy ({checks}).");
    }
}
