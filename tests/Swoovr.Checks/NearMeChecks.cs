using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Travel;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;

/// <summary>
/// D348, #278: Near me's stored reading. Acceptance (D149 thresholds, one every five minutes, 900 km/h), the ~500 m cell,
/// accuracy classes, exactly two hours of use (and never after "sign out everywhere"), and sealing with Location:Keys.
/// </summary>
static class NearMeChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Near me check failed: " + what); checks++; }
        var now = new DateTimeOffset(2026, 11, 8, 2, 0, 0, TimeSpan.Zero);
        LocationReading R(double lat = -34.9285, double lon = 138.6007, double accuracy = 50, int secondsAgo = 10) => new(lat, lon, accuracy, now.AddSeconds(-secondsAgo));

        // Acceptance: D149's thresholds, then the five-minute gate, then the speed check.
        Expect(NearMePolicy.Accept(R(), null, null, now) == NearMeRefusal.None, "a fresh, accurate first reading");
        Expect(NearMePolicy.Accept(R(secondsAgo: 121), null, null, now) == NearMeRefusal.Unusable, "older than two minutes");
        Expect(NearMePolicy.Accept(R(accuracy: 2001), null, null, now) == NearMeRefusal.Unusable, "coarser than 2 km (approximate permission)");
        Expect(NearMePolicy.Accept(R(accuracy: 2000), null, null, now) == NearMeRefusal.None, "exactly 2 km is accepted");
        Expect(NearMePolicy.Accept(R(lat: 0, lon: 0), null, null, now) == NearMeRefusal.Unusable, "0,0");
        Expect(NearMePolicy.Accept(null, null, null, now) == NearMeRefusal.Unusable, "no reading");
        var previous = new MemberLocation { Accuracy = LocationAccuracy.Fine, ObservedAt = now.AddMinutes(-6), AcceptedAt = now.AddMinutes(-6) };
        var adelaide = NearMePolicy.Snap(-34.9285, 138.6007);
        Expect(NearMePolicy.Accept(R(), new MemberLocation { AcceptedAt = now.AddMinutes(-4), ObservedAt = now.AddMinutes(-4) }, adelaide, now) == NearMeRefusal.TooSoon, "one update every five minutes");
        Expect(NearMePolicy.Accept(R(), previous, adelaide, now) == NearMeRefusal.None, "after five minutes, nearby");
        // Six minutes at 900 km/h is 90 km: Gawler (~39 km) is possible, Melbourne (~650 km) isn't.
        Expect(NearMePolicy.Accept(R(lat: -34.6, lon: 138.74), previous, adelaide, now) == NearMeRefusal.None, "39 km in six minutes is under 900 km/h");
        Expect(NearMePolicy.Accept(R(lat: -37.8136, lon: 144.9631), previous, adelaide, now) == NearMeRefusal.Unusable, "650 km in six minutes is refused (speed)");
        var earlier = new MemberLocation { Accuracy = LocationAccuracy.Coarse, ObservedAt = now.AddHours(-1), AcceptedAt = now.AddHours(-1) };
        Expect(NearMePolicy.Accept(R(lat: -37.8136, lon: 144.9631), earlier, adelaide, now) == NearMeRefusal.None, "650 km in an hour is a flight");

        // The cell: ~500 m, every point in it gives the same centre, and the centre is near the reading.
        var a = NearMePolicy.Snap(-34.92850, 138.60070);
        var b = NearMePolicy.Snap(-34.92851, 138.60071);
        Expect(a == b, "nearby points share a cell");
        Expect(PlaceNames.DistanceKm(-34.92850, 138.60070, a.Latitude, a.Longitude) <= 0.36, "the centre is within half a cell diagonal");
        var c = NearMePolicy.Snap(-34.92850 + 0.0046, 138.60070);
        Expect(a != c && PlaceNames.DistanceKm(a.Latitude, a.Longitude, c.Latitude, c.Longitude) is > 0.4 and < 0.6, "cells are about 500 m apart");
        Expect(NearMePolicy.Snap(89.9999, 179.9999).Latitude <= 90 && Math.Abs(NearMePolicy.Snap(10, 179.9999).Longitude) <= 180, "edges stay valid");
        Expect(NearMePolicy.Class(250) == LocationAccuracy.Fine && NearMePolicy.Class(251) == LocationAccuracy.Coarse, "accuracy classes");

        // Freshness: exactly two hours from the device's reading, and never before the latest sign-out everywhere.
        var row = new MemberLocation { ObservedAt = now.AddHours(-2).AddSeconds(1) };
        Expect(NearMePolicy.Fresh(row, null, now), "just under two hours");
        Expect(!NearMePolicy.Fresh(new MemberLocation { ObservedAt = now.AddHours(-2) }, null, now), "at two hours it is no longer used, purged or not");
        Expect(!NearMePolicy.Fresh(row, now.AddMinutes(-1), now), "a reading taken before sign out everywhere isn't used");
        Expect(NearMePolicy.Fresh(row, now.AddHours(-3), now), "an older sign-out doesn't affect it");
        Expect(NearMePolicy.UsedUntil(row) == row.ObservedAt + TimeSpan.FromHours(2) && NearMePolicy.PurgeEvery == TimeSpan.FromMinutes(5), "used for two hours; purged every five minutes");

        // Sealing: readable only with the key; the sealed bytes carry no coordinate text.
        using var services = new ServiceCollection().BuildServiceProvider();
        LocationKeys Keys(params (string Key, string Value)[] values)
        {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values.Select(p => KeyValuePair.Create<string, string?>(p.Key, p.Value))).Build();
            return new LocationKeys(configuration, new Founders(services, configuration, new UnconfiguredEmailSender(), NullLogger<Founders>.Instance));
        }
        var key1 = Convert.ToBase64String(Enumerable.Repeat((byte)41, 32).ToArray());
        var key2 = Convert.ToBase64String(Enumerable.Repeat((byte)42, 32).ToArray());
        var keys = Keys(("Location:Keys:1", key1));
        var sealedCell = keys.Seal(a)!;
        Expect(keys.Open(sealedCell) is { } opened && opened == a, "round trip");
        Expect(sealedCell[0] == 1 && !Encoding.UTF8.GetString(sealedCell).Contains("-34.9") && !Encoding.UTF8.GetString(sealedCell).Contains("138.6"), "no coordinate text in the sealed value");
        Expect(Keys(("Location:Keys:1", key2)).Open(sealedCell) is null, "another key can't open it");
        Expect(Keys().CurrentVersion is null && Keys().Seal(a) is null, "without a key (or founder secret) nothing is stored");
        var both = Keys(("Location:Keys:1", key1), ("Location:Keys:2", key2));
        Expect(both.CurrentVersion == 2 && both.Seal(a)![0] == 2 && both.Open(sealedCell) == a, "a newer version seals; the older stays readable until rotated");
        Expect(Swoovr.Api.Security.KeyRotation.Sets.Contains("location"), "keys status/rotate cover location");
        var tampered = sealedCell.ToArray(); tampered[^1] ^= 1;
        Expect(keys.Open(tampered) is null, "a tampered value doesn't open");

        // #279 GPS bands (D348, Q152, Q161 b): hidden unless the person appears and shows distance and the viewer shows theirs;
        // "nearby" only with both readings fine (250 m) and at most 15 minutes old, otherwise unknown; never a neighbouring band.
        MemberLocation L(LocationAccuracy accuracy = LocationAccuracy.Fine, int minutesAgo = 1) => new() { Accuracy = accuracy, ObservedAt = now.AddMinutes(-minutesAgo) };
        var here = NearMePolicy.Snap(-34.9285, 138.6007);
        var fiveKm = NearMePolicy.Snap(-34.9285 + 5 / 111.32, 138.6007);
        Expect(NearMePolicy.GpsBand(L(), here, L(), here, true, true, true, now) == (DistanceBand.Nearby, (DistanceBasis?)DistanceBasis.Gps), "same cell, fine and fresh: nearby");
        Expect(NearMePolicy.GpsBand(L(), here, L(LocationAccuracy.Coarse), here, true, true, true, now) == (DistanceBand.Unknown, null), "nearby with a coarse reading is unknown");
        Expect(NearMePolicy.GpsBand(L(), here, L(minutesAgo: 16), here, true, true, true, now) == (DistanceBand.Unknown, null), "nearby with a reading over 15 minutes old is unknown");
        Expect(NearMePolicy.GpsBand(L(LocationAccuracy.Coarse, 60), here, L(), fiveKm, true, true, true, now) == (DistanceBand.Within10Km, (DistanceBasis?)DistanceBasis.Gps), "other bands need only fresh (2 h) readings");
        Expect(NearMePolicy.GpsBand(L(), here, L(), fiveKm, false, true, true, now).Band == DistanceBand.Hidden, "the person must appear in nearby searches");
        Expect(NearMePolicy.GpsBand(L(), here, L(), fiveKm, true, false, true, now).Band == DistanceBand.Hidden, "and show their distance");
        Expect(NearMePolicy.GpsBand(L(), here, L(), fiveKm, true, true, false, now).Band == DistanceBand.Hidden, "and the viewer must show theirs (Q152)");
        Expect(NearMePolicy.Rank(DistanceBand.Nearby) < NearMePolicy.Rank(DistanceBand.Within10Km) && NearMePolicy.Rank(DistanceBand.Over50Km) < NearMePolicy.Rank(DistanceBand.Unknown)
            && NearMePolicy.Rank(DistanceBand.Hidden) == NearMePolicy.Rank(DistanceBand.Unknown), "nearest visible band first; hidden and unknown last, unordered by distance");

        Console.WriteLine($"PASS: Near me store ({checks}): D149 thresholds, five-minute gate, 900 km/h, ~500 m cells, two-hour use and sign-out cutoff, sealed with Location:Keys; #279 GPS bands and ordering.");
    }
}
