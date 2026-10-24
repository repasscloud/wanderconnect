namespace Swoovr.Domain;

/// <summary>
/// A city from Swoovr's own GeoNames copy (D111, Q17 rules 1–4): cities1000 with admin1 and country names. The GeoNames
/// id is the key; names are never keys. Coordinates stay on the server (D110, Q17 rule 42).
/// </summary>
public sealed class GeoCity
{
    public int GeoNameId { get; set; }
    public string Name { get; set; } = "";
    public string AsciiName { get; set; } = "";
    public string CountryCode { get; set; } = "";
    public string CountryName { get; set; } = "";
    public string? Admin1Code { get; set; }
    public string? Admin1Name { get; set; }
    public long Population { get; set; }
    public string TimeZone { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

/// <summary>One searchable spelling of a city (its name, ASCII name or an alternate name), folded by PlaceNames.Key.</summary>
public sealed class GeoCityName
{
    public int GeoNameId { get; set; }
    public string Key { get; set; } = "";
}

/// <summary>
/// A market (D110, D62): a stable id, a name, whether it is active, the GeoNames city at its centre and an internal
/// radius. Adelaide is the only active market. The radius never leaves the server.
/// </summary>
public sealed class Market
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public int CentreGeoNameId { get; set; }
    public double RadiusKm { get; set; }
}

public static class PlaceNames
{
    /// <summary>Lower case without accents or surrounding spaces, so "Zürich", "zurich" and "ZURICH " match.</summary>
    public static string Key(string value)
    {
        var decomposed = value.Trim().ToLowerInvariant().Normalize(System.Text.NormalizationForm.FormD);
        var kept = decomposed.Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark);
        return string.Concat(kept).Normalize(System.Text.NormalizationForm.FormC);
    }

    /// <summary>"City, Region, Country", leaving out a region that is missing or repeats the city (D111).</summary>
    public static string Label(GeoCity city) =>
        string.Join(", ", new[] { city.Name, city.Admin1Name is { Length: > 0 } region && region != city.Name ? region : null, city.CountryName }
            .Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>Great-circle distance in kilometres (server-side only).</summary>
    public static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180;
        var a = Math.Pow(Math.Sin(Rad(lat2 - lat1) / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(Rad(lon2 - lon1) / 2), 2);
        return 6371.0088 * 2 * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    /// <summary>The active market a city belongs to, if any (D110): the first active market whose radius reaches it.</summary>
    public static Market? ActiveMarketOf(GeoCity? city, IEnumerable<(Market Market, GeoCity Centre)> markets) =>
        city is null ? null : markets.Where(m => m.Market.Active)
            .FirstOrDefault(m => DistanceKm(city.Latitude, city.Longitude, m.Centre.Latitude, m.Centre.Longitude) <= m.Market.RadiusKm).Market;
}
