namespace Swoovr.Domain;

/// <summary>D348: how accurate an accepted Near me reading was. Fine (250 m or better) is needed for the "nearby" band.</summary>
public enum LocationAccuracy { Fine, Coarse }

/// <summary>
/// D348, #278: a member's latest accepted Near me reading. One row per member, overwritten, never a history. The position
/// is snapped to a ~500 m cell and sealed with Location:Keys; it never leaves the server, is never logged or audited, and
/// is used for exactly two hours from when the device took it (NearMePolicy.Fresh), then purged.
/// </summary>
public sealed class MemberLocation
{
    public Guid AccountId { get; set; }
    /// <summary>The open market the reading fell in, or null; plaintext so Near me reads one market's rows only.</summary>
    public string? MarketId { get; set; }
    /// <summary>The cell centre sealed with Location:Keys: a version byte, nonce, tag and ciphertext.</summary>
    public byte[] SealedCell { get; set; } = [];
    public LocationAccuracy Accuracy { get; set; }
    /// <summary>When the device took the reading (at most "now").</summary>
    public DateTimeOffset ObservedAt { get; set; }
    /// <summary>When the API accepted it, for the one-update-per-five-minutes rule.</summary>
    public DateTimeOffset AcceptedAt { get; set; }
}

public enum NearMeRefusal { None, Unusable, TooSoon }

/// <summary>
/// D348 (Q161, #270): accepting, storing and using Near me readings. A reading is accepted under D149's thresholds (at most
/// two minutes old, accuracy 2 km or better, not 0,0), at most once every five minutes, and only if it doesn't imply
/// moving faster than 900 km/h since the last accepted one. It is used for exactly two hours from when it was taken, and
/// never if it was taken before the member's latest "sign out everywhere"; a five-minute purge deletes it after that.
/// </summary>
public static class NearMePolicy
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromHours(2), MinInterval = TimeSpan.FromMinutes(5), PurgeEvery = TimeSpan.FromMinutes(5);
    public const double CellMetres = 500, FineMetres = 250, MaxSpeedKmh = 900;

    /// <summary>The centre of the ~500 m cell the position falls in; every position in a cell gives the same centre.</summary>
    public static (double Latitude, double Longitude) Snap(double latitude, double longitude)
    {
        var latStep = CellMetres / 111_320.0;
        var lat = Math.Clamp((Math.Floor(latitude / latStep) + 0.5) * latStep, -90, 90);
        var lonStep = CellMetres / (111_320.0 * Math.Max(0.01, Math.Cos(lat * Math.PI / 180)));
        var lon = (Math.Floor(longitude / lonStep) + 0.5) * lonStep;
        return (lat, lon > 180 ? lon - 360 : lon < -180 ? lon + 360 : lon);
    }

    public static LocationAccuracy Class(double accuracyMetres) => accuracyMetres <= FineMetres ? LocationAccuracy.Fine : LocationAccuracy.Coarse;

    /// <summary>
    /// Whether to accept a reading, given the member's previous accepted one (and its cell, opened). The speed check allows
    /// for the snapping (up to ~0.36 km) and both readings' accuracy, so an honest walk never trips it.
    /// </summary>
    public static NearMeRefusal Accept(LocationReading? reading, MemberLocation? previous, (double Latitude, double Longitude)? previousCell, DateTimeOffset now)
    {
        if (!ArrivalPolicy.Usable(reading, now)) return NearMeRefusal.Unusable;
        if (previous is not null && now - previous.AcceptedAt < MinInterval) return NearMeRefusal.TooSoon;
        if (previous is not null && previousCell is { } cell)
        {
            var hours = Math.Abs((reading!.TakenAt - previous.ObservedAt).TotalHours);
            var slack = 1 + reading.AccuracyMetres / 1000 + (previous.Accuracy == LocationAccuracy.Fine ? FineMetres : ArrivalPolicy.MaxAccuracyMetres) / 1000;
            var km = PlaceNames.DistanceKm(cell.Latitude, cell.Longitude, reading.Latitude, reading.Longitude) - slack;
            if (km > 0 && km > MaxSpeedKmh * hours) return NearMeRefusal.Unusable;
        }
        return NearMeRefusal.None;
    }

    /// <summary>Whether a stored reading may be used now: under two hours old and taken after the latest sign-out everywhere.</summary>
    public static bool Fresh(MemberLocation location, DateTimeOffset? sessionsRevokedAt, DateTimeOffset now) =>
        location.ObservedAt > now - FreshFor && (sessionsRevokedAt is null || location.ObservedAt > sessionsRevokedAt);

    public static DateTimeOffset UsedUntil(MemberLocation location) => location.ObservedAt + FreshFor;

    /// <summary>D348 (Q161 b): "nearby" (0–2 km) needs both readings accurate to 250 m and no more than 15 minutes old.</summary>
    public static readonly TimeSpan NearbyFreshFor = TimeSpan.FromMinutes(15);

    /// <summary>
    /// D348, #279: the GPS band between two fresh readings' cells. Hidden unless the person appears in nearby searches and
    /// shows their distance and the viewer shows theirs (Q152). "Nearby" falls back to unknown, never a neighbouring band,
    /// when either reading is coarse or older than 15 minutes. Never a distance.
    /// </summary>
    public static (DistanceBand Band, DistanceBasis? Basis) GpsBand(MemberLocation viewer, (double Latitude, double Longitude) viewerCell,
        MemberLocation person, (double Latitude, double Longitude) personCell, bool personAppears, bool personShowsDistance, bool viewerShowsDistance,
        DateTimeOffset now)
    {
        if (!(personAppears && personShowsDistance && viewerShowsDistance)) return (DistanceBand.Hidden, null);
        var band = LocationPrivacy.BandFor(PlaceNames.DistanceKm(viewerCell.Latitude, viewerCell.Longitude, personCell.Latitude, personCell.Longitude));
        var precise = new[] { viewer, person }.All(l => l.Accuracy == LocationAccuracy.Fine && now - l.ObservedAt <= NearbyFreshFor);
        return band == DistanceBand.Nearby && !precise ? (DistanceBand.Unknown, null) : (band, DistanceBasis.Gps);
    }

    /// <summary>D348 ordering (Q154): nearest visible band first; hidden and unknown last. Never orders by a hidden distance.</summary>
    public static int Rank(DistanceBand band) => band is DistanceBand.Hidden or DistanceBand.Unknown ? int.MaxValue : (int)band;
}
