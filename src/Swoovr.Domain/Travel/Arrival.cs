namespace Swoovr.Domain;

/// <summary>How an arrival was confirmed (D149 rule 11): the member's fresh device location, or support after a review.</summary>
public enum ArrivalMethod { DeviceLocation, SupportReview }

/// <summary>
/// The last arrival check's outcome when it didn't confirm (D149 rule 7): the trip stays upcoming and shows as
/// "arrival unconfirmed" until a later check or support confirms it.
/// </summary>
public enum ArrivalOutcome { Confirmed, NotYet, Outside, Unusable }

/// <summary>
/// A device location the client read for one check (D149, D150). The API never stores it: it decides the market (or
/// none) and keeps only the outcome. Accuracy is the device's own estimate in metres; TakenAt is when the device read it.
/// </summary>
public sealed record LocationReading(double Latitude, double Longitude, double AccuracyMetres, DateTimeOffset TakenAt);

public enum ArrivalRefusal
{
    None,
    /// <summary>Cancelled, ended, or in a market that isn't open: there is nothing to arrive at.</summary>
    TripNotLive,
    /// <summary>The arrival date hasn't been reached at the destination.</summary>
    TooEarly,
    /// <summary>No usable reading: missing, impossible, too old, from the future or too inaccurate.</summary>
    Unusable,
    /// <summary>The reading is outside every area of the trip's market.</summary>
    Outside,
}

/// <summary>Where the trip stands for the traveller: "upcoming" before arrival, "unconfirmed" after a failed check, "arrived".</summary>
public enum ArrivalState { Upcoming, Unconfirmed, Arrived }

/// <summary>
/// Arrival (Q46, D149) and the traveller's side of Traveller Search (Q41, D142). A traveller has arrived when the trip's
/// arrival date has been reached at the destination and a fresh device location, checked by the API, falls inside one of
/// the destination market's areas. Confirmation lasts for the trip (no check per request), is cleared when the trip's
/// city or dates change (rule 9) and ends with the trip (rule 12). Client flags are never trusted, IP alone never
/// confirms (rule 8), and coordinates are never stored (rule 11). DEFAULT thresholds (D164): a reading at most two
/// minutes old (five seconds of clock skew into the future) with an accuracy of 2 km or better.
/// </summary>
public static class ArrivalPolicy
{
    public static readonly TimeSpan MaxReadingAge = TimeSpan.FromMinutes(2), MaxClockSkew = TimeSpan.FromSeconds(5);
    public const double MaxAccuracyMetres = 2000;
    public const int ChecksPerHour = 10;

    /// <summary>Whether a reading can be used at all: real coordinates, fresh and accurate enough.</summary>
    public static bool Usable(LocationReading? reading, DateTimeOffset now) =>
        reading is { } r && double.IsFinite(r.Latitude) && double.IsFinite(r.Longitude) && double.IsFinite(r.AccuracyMetres)
        && r.Latitude is >= -90 and <= 90 && r.Longitude is >= -180 and <= 180 && !(r.Latitude == 0 && r.Longitude == 0)
        && r.AccuracyMetres is > 0 and <= MaxAccuracyMetres
        && r.TakenAt <= now + MaxClockSkew && now - r.TakenAt <= MaxReadingAge;

    /// <summary>Whether a usable reading is inside one of the market's areas (the area's radius around its centre city).</summary>
    public static bool InMarket(LocationReading reading, IEnumerable<MarketZone> marketAreas) =>
        marketAreas.Any(z => PlaceNames.DistanceKm(reading.Latitude, reading.Longitude, z.Centre.Latitude, z.Centre.Longitude) <= z.Area.RadiusKm);

    /// <summary>
    /// Confirm-arrival (rule 3): the trip must be live in an open market, its arrival date reached at the destination,
    /// and a usable reading inside the market. Checked in this order, so a too-early check says so before it asks for a
    /// location.
    /// </summary>
    public static ArrivalRefusal Confirm(TripState state, Market? tripMarket, DateOnly destinationToday, DateOnly arrival,
        LocationReading? reading, IReadOnlyCollection<MarketZone> marketAreas, DateTimeOffset now)
    {
        if (state is TripState.Cancelled or TripState.Expired || tripMarket is not { Open: true }) return ArrivalRefusal.TripNotLive;
        if (destinationToday < arrival) return ArrivalRefusal.TooEarly;
        if (!Usable(reading, now)) return ArrivalRefusal.Unusable;
        return InMarket(reading!, marketAreas.Where(z => z.Market.Id == tripMarket.Id)) ? ArrivalRefusal.None : ArrivalRefusal.Outside;
    }

    /// <summary>The traveller's view of a live trip's arrival.</summary>
    public static ArrivalState State(Trip trip) =>
        trip.ArrivedAt is not null ? ArrivalState.Arrived : trip.ArrivalOutcome is ArrivalOutcome.Outside or ArrivalOutcome.Unusable ? ArrivalState.Unconfirmed : ArrivalState.Upcoming;

    /// <summary>
    /// Whether the traveller may start contact with the destination's locals through this trip (Q41 rule 6, D149 rule
    /// 4): arrived, in the market it was confirmed for, and the trip still live.
    /// </summary>
    public static bool MayInitiate(Trip trip, TripState state, Market? tripMarket) =>
        trip.ArrivedAt is not null && state is not (TripState.Cancelled or TripState.Expired) && tripMarket is { Open: true } && trip.ArrivalMarketId == tripMarket.Id;

    /// <summary>Rule 9: changing the destination or the dates clears an arrival; visibility alone doesn't.</summary>
    public static bool ResetsArrival(Trip before, int newCity, DateOnly newArrival, DateOnly newDeparture) =>
        before.GeoNameId != newCity || before.Arrival != newArrival || before.Departure != newDeparture;
}

public enum SearchRefusal { None, TripNotLive, TooEarly, Hidden, OwnMarket }

/// <summary>
/// Traveller Search (Q41, D142): within one enabled destination market, only through one of the member's own trips there.
/// The trip must be live (not cancelled or ended), in an open market, visible to locals (DEFAULT, D164: browsing locals
/// while hidden from them would be one-way), and within 14 days of its own arrival (rule 3). A member whose home is in
/// that market uses Explore instead. Never a free choice of market (rule 8).
/// </summary>
public static class TravellerSearch
{
    public static SearchRefusal Check(Trip trip, TripState state, Market? tripMarket, DateOnly destinationToday, string? searcherHomeMarket)
    {
        if (trip.CancelledAt is not null || state == TripState.Expired || tripMarket is not { Open: true }) return SearchRefusal.TripNotLive;
        if (searcherHomeMarket == tripMarket.Id) return SearchRefusal.OwnMarket;
        if (!trip.Visible) return SearchRefusal.Hidden;
        return destinationToday < trip.Arrival.AddDays(-TravelPolicy.WindowDays) ? SearchRefusal.TooEarly : SearchRefusal.None;
    }
}

/// <summary>How a home-location check was made (D150, D153): the member's device, or Cloudflare's approximate IP location.</summary>
public enum LocationMethod { Device, ApproximateIp }

public enum HomeConsistency { Consistent, Inconsistent }

/// <summary>
/// One home-location consistency check (Q47 rules 2, 5–8; D150, D153): when, how, the home market at the time and
/// whether the evidence agreed. Never the coordinates, the IP address or the derived place.
/// </summary>
public sealed class HomeLocationCheck
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
    public LocationMethod Method { get; set; }
    public string HomeMarketId { get; set; } = "";
    public HomeConsistency Outcome { get; set; }
}

/// <summary>
/// Home-city consistency (Q47, D150). A device reading inside the home market's areas is consistent; outside is
/// inconsistent, unless the member has a confirmed arrival on a live trip elsewhere (they're travelling). Nothing is
/// ever suspended automatically (rule 6): DEFAULT (D164), three inconsistent device checks on different days within 30
/// days, with no consistent check after them, flag the account for an admin's review. Approximate IP checks are recorded
/// as supporting evidence only and never flag by themselves (D153).
/// </summary>
public static class HomeLocationPolicy
{
    public const int FlagAfter = 3;
    public static readonly TimeSpan Window = TimeSpan.FromDays(30);

    public static HomeConsistency Judge(bool insideHomeMarket) => insideHomeMarket ? HomeConsistency.Consistent : HomeConsistency.Inconsistent;

    public static bool ShouldFlag(IEnumerable<HomeLocationCheck> checks, DateTimeOffset now)
    {
        var device = checks.Where(c => c.Method == LocationMethod.Device && now - c.CheckedAt <= Window).OrderBy(c => c.CheckedAt).ToList();
        var lastConsistent = device.LastOrDefault(c => c.Outcome == HomeConsistency.Consistent)?.CheckedAt;
        var days = device.Where(c => c.Outcome == HomeConsistency.Inconsistent && (lastConsistent is null || c.CheckedAt > lastConsistent))
            .Select(c => DateOnly.FromDateTime(c.CheckedAt.UtcDateTime)).Distinct().Count();
        return days >= FlagAfter;
    }
}
