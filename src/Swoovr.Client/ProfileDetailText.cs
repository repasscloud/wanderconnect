namespace Swoovr.Client;

/// <summary>Issue 448 (D457): wording for profile details on the web.</summary>
public static class ProfileDetailText
{
    /// <summary>Issue 571 (D460): "Visiting Adelaide until 10 Dec" or "Upcoming visit to Adelaide, 3–10 Dec" (city and dates only).</summary>
    public static string Trip(Swoovr.Contracts.ProfileTravelView trip)
    {
        string Day(DateOnly d) => d.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);
        return trip.Visiting ? $"Visiting {trip.City} until {Day(trip.Departure)}" : $"Upcoming visit to {trip.City}, {Day(trip.Arrival)}–{Day(trip.Departure)}";
    }

    /// <summary>Issue 452 (D459): "You both like hiking", "You both like hiking and travel", "You both like a, b and c".</summary>
    public static string BothLike(IReadOnlyList<string> labels)
    {
        var lower = labels.Select(l => l.Length > 1 && char.IsUpper(l[0]) && !char.IsUpper(l[1]) && l != "TV" && l != "DIY" ? char.ToLowerInvariant(l[0]) + l[1..] : l).ToList();
        return "You both like " + (lower.Count == 1 ? lower[0] : string.Join(", ", lower.Take(lower.Count - 1)) + " and " + lower[^1]);
    }

    /// <summary>"5′10″" from centimetres, shown beside the height field (the profile itself shows both units from the API).</summary>
    public static string Height(int centimetres)
    {
        var inches = (int)Math.Round(centimetres / 2.54);
        return $"About {inches / 12}′{inches % 12}″";
    }
}
