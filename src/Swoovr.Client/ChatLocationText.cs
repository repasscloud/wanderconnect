using System.Globalization;
using System.Text.RegularExpressions;
using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>
/// Issue 544 (D431, D455): reading a place the member pasted, and the maps links on a shared location's card. Nothing here
/// contacts a map provider: a link is only followed when the member taps it, and it carries the coordinates only, never the
/// label or anything about either member.
/// </summary>
public static partial class ChatLocationText
{
    /// <summary>The label limit; the API checks it again (ChatLocationPolicy.LabelMax).</summary>
    public const int LabelMax = 80;

    public const string Explain = "The other member sees the exact place you choose. Swoovr doesn't show a map preview, and the location stays in the chat like any message.";

    /// <summary>
    /// A point from what the member pasted: "lat, lon", a geo: link, or a link from Apple Maps (ll= or q=), Google Maps (@lat,lon,
    /// q= or query=) or OpenStreetMap (mlat/mlon or #map=zoom/lat/lon). Null when there is no usable point.
    /// </summary>
    public static (double Latitude, double Longitude)? Parse(string? input)
    {
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 2048) return null;
        try { text = Uri.UnescapeDataString(text); } catch (UriFormatException) { }
        if (Osm().Match(text) is { Success: true } osm) return Point(osm.Groups["lat"].Value, osm.Groups["lon"].Value);
        if (OsmHash().Match(text) is { Success: true } hash) return Point(hash.Groups["lat"].Value, hash.Groups["lon"].Value);
        if (Pair().Match(text) is { Success: true } pair) return Point(pair.Groups["lat"].Value, pair.Groups["lon"].Value);
        return null;
    }

    private static (double, double)? Point(string lat, string lon) =>
        double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var a) && double.TryParse(lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var b)
        && double.IsFinite(a) && double.IsFinite(b) && a is >= -90 and <= 90 && b is >= -180 and <= 180 ? (a, b) : null;

    /// <summary>"-33.86882, 151.20929", with the label when there is one.</summary>
    public static string Coordinates(ChatLocationView place) => Number(place.Latitude) + ", " + Number(place.Longitude);

    public static string Title(ChatLocationView place) => string.IsNullOrWhiteSpace(place.Label) ? "Shared location" : place.Label!;

    public static string AppleMaps(ChatLocationView p) => $"https://maps.apple.com/?ll={Number(p.Latitude)},{Number(p.Longitude)}&q={Number(p.Latitude)},{Number(p.Longitude)}";
    public static string GoogleMaps(ChatLocationView p) => $"https://www.google.com/maps/search/?api=1&query={Number(p.Latitude)},{Number(p.Longitude)}";
    public static string OpenStreetMap(ChatLocationView p) => $"https://www.openstreetmap.org/?mlat={Number(p.Latitude)}&mlon={Number(p.Longitude)}#map=17/{Number(p.Latitude)}/{Number(p.Longitude)}";

    private static string Number(double value) => Math.Round(value, 5).ToString("0.#####", CultureInfo.InvariantCulture);

    [GeneratedRegex(@"mlat=(?<lat>-?\d{1,2}(?:\.\d+)?)&mlon=(?<lon>-?\d{1,3}(?:\.\d+)?)")]
    private static partial Regex Osm();
    [GeneratedRegex(@"#map=\d{1,2}/(?<lat>-?\d{1,2}(?:\.\d+)?)/(?<lon>-?\d{1,3}(?:\.\d+)?)")]
    private static partial Regex OsmHash();
    // A latitude and longitude pair, after "@", "geo:", "ll=", "q=", "query=" or on its own; a comma, optional spaces.
    [GeneratedRegex(@"(?:^|[@:=\s(])(?<lat>[-+]?\d{1,2}(?:\.\d+)?)\s*,\s*(?<lon>[-+]?\d{1,3}(?:\.\d+)?)(?![\d.])")]
    private static partial Regex Pair();
}
