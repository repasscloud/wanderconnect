namespace Swoovr.Domain;

/// <summary>
/// A trip (Q17 rules 7–19, D107): a GeoNames city, mandatory arrival and departure dates (calendar dates at the
/// destination), a visibility switch and a lifecycle. Unlimited per member. Cancelling keeps the row in the member's
/// private history; removing deletes it.
/// </summary>
public sealed class Trip
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public int GeoNameId { get; set; }
    public DateOnly Arrival { get; set; }
    public DateOnly Departure { get; set; }
    /// <summary>The member's switch. A trip is discoverable only while this is on and TravelPolicy says it is open.</summary>
    public bool Visible { get; set; }
    public Guid? ItineraryId { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A group of back-to-back stops (D109). Its stops open together once the first is within 14 days.</summary>
public sealed class Itinerary
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public enum TripState { Planned, Visible, Expired, Cancelled }

public enum TripError { None, DepartureBeforeArrival, AlreadyOver, TooFarAhead, TooLong, ItineraryNotBackToBack, OverlapsVisibleTrip }

/// <summary>A trip with what the policy needs: its destination's IANA time zone.</summary>
public sealed record TripFacts(Trip Trip, string TimeZone);

/// <summary>
/// Server-side Travel rules (Q17 rules 8–20, D107, D109, D120). Dates are calendar dates at the destination, so "today"
/// is the destination's local date. A trip outside an itinerary opens 14 days before its own arrival; every stop in an
/// itinerary opens once its first stop is within 14 days. A trip expires once its departure date has passed locally.
/// DEFAULT limits: arrival at most two years ahead, a trip at most 365 nights, a departure already past is refused.
/// </summary>
public static class TravelPolicy
{
    public const int WindowDays = 14, MaxItineraryGapDays = 14, MaxNights = 365, MaxDaysAhead = 730;

    public static DateOnly LocalToday(string timeZone, DateTimeOffset now)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { zone = TimeZoneInfo.Utc; }
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime);
    }

    /// <summary>Checks one trip's own dates (on create or edit). Overlap and itinerary checks are separate.</summary>
    public static TripError CheckDates(DateOnly arrival, DateOnly departure, string timeZone, DateTimeOffset now)
    {
        var today = LocalToday(timeZone, now);
        if (departure < arrival) return TripError.DepartureBeforeArrival;
        if (departure < today) return TripError.AlreadyOver;
        if (arrival.DayNumber - today.DayNumber > MaxDaysAhead) return TripError.TooFarAhead;
        return departure.DayNumber - arrival.DayNumber > MaxNights ? TripError.TooLong : TripError.None;
    }

    /// <summary>D109: ordered by arrival, each stop arrives on or after the previous one departs, with a gap of 14 days or less.</summary>
    public static bool BackToBack(IEnumerable<Trip> stops)
    {
        var ordered = stops.Where(t => t.CancelledAt is null).OrderBy(t => t.Arrival).ThenBy(t => t.Departure).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            var gap = ordered[i].Arrival.DayNumber - ordered[i - 1].Departure.DayNumber;
            if (gap is < 0 or > MaxItineraryGapDays) return false;
        }
        return true;
    }

    /// <summary>Two trips overlap unless one departs on or before the day the other arrives (the changeover day is shared).</summary>
    public static bool Overlap(Trip a, Trip b) => a.Arrival < b.Departure && b.Arrival < a.Departure
        || a.Arrival == b.Arrival && a.Departure == b.Departure;

    /// <summary>D109: turning visibility on is refused while another visible, live trip of the member overlaps it.</summary>
    public static bool OverlapsVisible(Trip trip, IEnumerable<TripFacts> others, DateTimeOffset now) =>
        others.Any(o => o.Trip.Id != trip.Id && o.Trip.Visible && State(o, [], now) != TripState.Cancelled
            && State(o, [], now) != TripState.Expired && Overlap(trip, o.Trip));

    /// <summary>The first day the trip may be discovered: 14 days before its own arrival, or before its itinerary's first stop.</summary>
    public static DateOnly OpensOn(TripFacts trip, IReadOnlyCollection<TripFacts> itinerary)
    {
        var first = itinerary.Where(s => s.Trip.CancelledAt is null && s.Trip.ItineraryId is not null && s.Trip.ItineraryId == trip.Trip.ItineraryId)
            .Append(trip).MinBy(s => s.Trip.Arrival)!;
        return first.Trip.Arrival.AddDays(-WindowDays);
    }

    /// <summary>
    /// Lifecycle (Q17 rule 14): cancelled; expired once the departure date has passed at the destination; visible while
    /// the switch is on and the window is open (judged in the opening stop's time zone); otherwise planned.
    /// </summary>
    public static TripState State(TripFacts trip, IReadOnlyCollection<TripFacts> itinerary, DateTimeOffset now)
    {
        if (trip.Trip.CancelledAt is not null) return TripState.Cancelled;
        if (LocalToday(trip.TimeZone, now) > trip.Trip.Departure) return TripState.Expired;
        if (!trip.Trip.Visible) return TripState.Planned;
        var first = itinerary.Where(s => s.Trip.CancelledAt is null && s.Trip.ItineraryId is not null && s.Trip.ItineraryId == trip.Trip.ItineraryId)
            .Append(trip).MinBy(s => s.Trip.Arrival)!;
        return LocalToday(first.TimeZone, now) >= first.Trip.Arrival.AddDays(-WindowDays) ? TripState.Visible : TripState.Planned;
    }

    /// <summary>
    /// Whether a local may see this traveller through this trip (Q17 rules 22–28, 38–39): the trip is visible, its city
    /// is in an open market, the viewer's home market is that market and the traveller's is not (D125). Account, mode
    /// and block checks are the caller's (DiscoveryEligibility, blocks).
    /// </summary>
    public static bool ShownToLocal(TripState state, Market? tripMarket, string? viewerHomeMarket, string? travellerHomeMarket) =>
        state == TripState.Visible && tripMarket is { Open: true } && viewerHomeMarket == tripMarket.Id && travellerHomeMarket != tripMarket.Id;
}
