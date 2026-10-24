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

/// <summary>Where a market is in its life (D125). Only Beta and Active markets take part in discovery.</summary>
public enum MarketStatus { Planned, Beta, Active, Suspended }

/// <summary>
/// A market (D110, D62, D125): a stable id, a name and a status. Its coverage is one or more MarketArea rows and its
/// modes are MarketMode rows, so admins change all three without a release. Adelaide is the only market so far.
/// </summary>
public sealed class Market
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public MarketStatus Status { get; set; }
    /// <summary>Whether the market takes part in discovery now (D125 rules 2 and 5).</summary>
    public bool Open => Status is MarketStatus.Beta or MarketStatus.Active;
}

/// <summary>
/// One circle of a market's coverage: a GeoNames city at its centre and a radius (D146). A metro market has one; a
/// regional market can have several. Coordinates and radii never leave the server.
/// </summary>
public sealed class MarketArea
{
    public string MarketId { get; set; } = "";
    public int CentreGeoNameId { get; set; }
    public double RadiusKm { get; set; }
}

/// <summary>A connection mode a market offers (D125 rule 1).</summary>
public sealed class MarketMode
{
    public string MarketId { get; set; } = "";
    public string ModeId { get; set; } = "";
}

/// <summary>A market an official bot is assigned to (D125 rules 38–41); MarketId `*` means every open market.</summary>
public sealed class BotMarket
{
    public const string Everywhere = "*";
    public Guid AccountId { get; set; }
    public string MarketId { get; set; } = "";
    public DateTimeOffset AuthorisedAt { get; set; }
    public Guid? AuthorisedBy { get; set; }
}

/// <summary>A member outside every open market who asked to hear when theirs launches (D125 rule 37).</summary>
public sealed class MarketInterest
{
    public Guid AccountId { get; set; }
    public int GeoNameId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>One area of a market with its centre city, as the server works with it.</summary>
public sealed record MarketZone(Market Market, MarketArea Area, GeoCity Centre);

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

    /// <summary>
    /// The market a city belongs to, whatever its status (D146): of the markets with an area that reaches the city, an
    /// open one first, then the nearest area centre. Null outside every market.
    /// </summary>
    public static Market? MarketOf(GeoCity? city, IEnumerable<MarketZone> zones) =>
        city is null ? null : zones
            .Select(z => (z.Market, Km: DistanceKm(city.Latitude, city.Longitude, z.Centre.Latitude, z.Centre.Longitude), z.Area.RadiusKm))
            .Where(x => x.Km <= x.RadiusKm)
            .OrderByDescending(x => x.Market.Open).ThenBy(x => x.Km).ThenBy(x => x.Market.Id, StringComparer.Ordinal)
            .Select(x => x.Market).FirstOrDefault();

    /// <summary>The open market a city belongs to, if any (D110, D125).</summary>
    public static Market? OpenMarketOf(GeoCity? city, IEnumerable<MarketZone> zones) => MarketOf(city, zones) is { Open: true } market ? market : null;
}
