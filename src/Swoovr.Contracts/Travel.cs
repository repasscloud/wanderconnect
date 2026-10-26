namespace Swoovr.Contracts;

/// <summary>A GeoNames city as search and trips show it (D111): id, name, region, country and "City, Region, Country". No coordinates.</summary>
public sealed record PlaceView(int Id, string Name, string? Region, string Country, string Label);

/// <summary>A trip as its owner sees it. State is "planned", "visible", "expired" or "cancelled"; OpensOn is the first day locals may see it.</summary>
/// <param name="ArrivalState">"upcoming", "unconfirmed" (a check didn't confirm) or "arrived" (D149); ArrivedAt is when it was confirmed.</param>
/// <param name="SearchOpen">Whether Traveller Search is open through this trip now (D142); SearchOpensOn is the first day it opens.</param>
public sealed record TripView(Guid Id, PlaceView City, DateOnly Arrival, DateOnly Departure, bool Visible, string State, Guid? ItineraryId,
    DateOnly OpensOn, bool ActiveMarket, string ArrivalState = "upcoming", DateTimeOffset? ArrivedAt = null, bool SearchOpen = false, DateOnly? SearchOpensOn = null);

/// <summary>
/// A device location for one check (D149, D150): degrees, the device's accuracy estimate in metres and when it was read.
/// The API decides the market and keeps only the outcome; the reading itself is never stored or shown to anyone.
/// </summary>
public sealed record LocationCheck(double Latitude, double Longitude, double AccuracyMetres, DateTimeOffset TakenAt);

/// <summary>A home-location check's outcome (D150): "consistent" or "inconsistent", or "not-checked" when there was nothing to compare.</summary>
public sealed record HomeCheckView(string Outcome);

/// <summary>
/// Traveller Search (D142): the destination's locals in one mode, through one of the member's own trips. CanRequest is
/// true once arrival is confirmed (D149); before that the traveller may browse and open profiles but not start contact.
/// </summary>
public sealed record TravellerSearchResult(string Mode, string Market, Guid TripId, string ArrivalState, bool CanRequest, IReadOnlyList<ProfileCard> People);

/// <summary>A trip as support sees it (D149 rule 7): the market (not the city), dates, lifecycle, arrival and how it was confirmed.</summary>
public sealed record TripAdminView(Guid Id, string? Market, DateOnly Arrival, DateOnly Departure, string State, string ArrivalState, string? ArrivalMethod);

/// <summary>Staff: confirm a traveller's arrival after a review (D149 rule 7), with a reason; audited, never shown to others.</summary>
public sealed record ArrivalReview(string? Reason);
public sealed record TripPage(IReadOnlyList<TripView> Items, int Page, bool More);
public sealed record TripUpdate(int CityId, DateOnly Arrival, DateOnly Departure, bool Visible, Guid? ItineraryId);
public sealed record ItineraryView(Guid Id);

/// <summary>What a local sees of a visiting traveller's trip (Q17 rules 24–27): city, country and the exact dates only.</summary>
public sealed record VisitView(Guid TripId, string City, string Country, DateOnly Arrival, DateOnly Departure);

/// <summary>A traveller in the destination feed, always marked as visiting.</summary>
public sealed record VisitorCard(Guid Id, string DisplayName, IReadOnlyList<BadgeView> Badges, IReadOnlyList<string> Intentions, VisitView Visiting);
public sealed record VisitorPage(string Mode, string Market, IReadOnlyList<VisitorCard> People, int Page, bool More);
