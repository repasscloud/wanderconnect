namespace Swoovr.Contracts;

/// <summary>A GeoNames city as search and trips show it (D111): id, name, region, country and "City, Region, Country". No coordinates.</summary>
public sealed record PlaceView(int Id, string Name, string? Region, string Country, string Label);

/// <summary>A trip as its owner sees it. State is "planned", "visible", "expired" or "cancelled"; OpensOn is the first day locals may see it.</summary>
public sealed record TripView(Guid Id, PlaceView City, DateOnly Arrival, DateOnly Departure, bool Visible, string State, Guid? ItineraryId,
    DateOnly OpensOn, bool ActiveMarket);
public sealed record TripPage(IReadOnlyList<TripView> Items, int Page, bool More);
public sealed record TripUpdate(int CityId, DateOnly Arrival, DateOnly Departure, bool Visible, Guid? ItineraryId);
public sealed record ItineraryView(Guid Id);

/// <summary>What a local sees of a visiting traveller's trip (Q17 rules 24–27): city, country and the exact dates only.</summary>
public sealed record VisitView(Guid TripId, string City, string Country, DateOnly Arrival, DateOnly Departure);

/// <summary>A traveller in the destination feed, always marked as visiting.</summary>
public sealed record VisitorCard(Guid Id, string DisplayName, IReadOnlyList<BadgeView> Badges, IReadOnlyList<string> Intentions, VisitView Visiting);
public sealed record VisitorPage(string Mode, string Market, IReadOnlyList<VisitorCard> People, int Page, bool More);
