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
        // The sweep: a person 7.3 km away is in or out for whole bands only.
        var viewer = new MatchFacts(30, new HashSet<Guid>(), new HashSet<Guid>(), false, null, [], -34.9285, 138.6007, false);
        var person = new MatchFacts(30, new HashSet<Guid>(), new HashSet<Guid>(), false, null, [], -34.9285 + 7.3 / 111.0, 138.6007, false);
        var d = MatchPolicy.DistanceKm(viewer, person)!.Value;
        Expect(d is > 7 and < 8, "the fixture is about 7.3 km away");
        var seen = Enumerable.Range(1, 30).Select(km => MatchPolicy.FilterAllows(new ModeFilter { MaxDistanceKm = km, RequiredChips = [], ExcludedChips = [] }, viewer, person, PlanTier.Plus)).ToList();
        Expect(seen.Take(5).All(s => !s) && seen.Skip(5).All(s => s), "filters 1–5 km hide them and 6–30 km all show them: nothing finer than the band");
        Expect(LocationPrivacy.Band(d) == "5–10 km" && LocationPrivacy.Band(2) == "Under 5 km" && LocationPrivacy.Band(140) == "Over 100 km" && LocationPrivacy.Band(null) is null, "band labels");
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
