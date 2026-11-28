using System.Globalization;

namespace Swoovr.Domain;

/// <summary>A place shared in a chat (#544, D431, D455): the exact point the member picked and an optional short label.</summary>
public sealed record ChatLocation(double Latitude, double Longitude, string? Label);

/// <summary>Why a location can't be shared.</summary>
public enum ChatLocationRefusal { None, InvalidPoint, InvalidLabel }

/// <summary>
/// Issue 544 (D431, D455): sharing one chosen location in a chat, once, on purpose. Never live, never tracked, never used
/// for discovery. A location message keeps its place in the message's own text, as a "geo:" line (RFC 5870 coordinates,
/// 5 decimal places, about a metre) and an optional label line, so every rule for a message's text applies to it unchanged:
/// delete for both within five minutes, the 30-day evidence window and purge after it (D167), holds, reports, the export
/// and account deletion. The label is screened like any message text. No third-party map is loaded in the chat (D431).
/// </summary>
public static class ChatLocationPolicy
{
    public const int LabelMax = 80, Decimals = 5;
    private const string Prefix = "geo:";

    public static ChatLocationRefusal Check(double? latitude, double? longitude, string? label)
    {
        if (latitude is not { } lat || longitude is not { } lon || !double.IsFinite(lat) || !double.IsFinite(lon)
            || lat is < -90 or > 90 || lon is < -180 or > 180) return ChatLocationRefusal.InvalidPoint;
        // DEFAULT: 0,0 ("Null Island") is what a failed reading or an empty form produces, never a place someone meant.
        if (Math.Round(lat, Decimals) == 0 && Math.Round(lon, Decimals) == 0) return ChatLocationRefusal.InvalidPoint;
        if (label is not null && !string.IsNullOrWhiteSpace(label) && CleanLabel(label) is null) return ChatLocationRefusal.InvalidLabel;
        return ChatLocationRefusal.None;
    }

    /// <summary>One line, up to 80 characters, no control characters; empty means no label.</summary>
    public static string? CleanLabel(string? label)
    {
        var trimmed = label?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > LabelMax || trimmed.Any(char.IsControl)) return null;
        return trimmed;
    }

    /// <summary>The message text for a checked location: "geo:lat,lon", then the label on its own line when there is one.</summary>
    public static string Format(double latitude, double longitude, string? label)
    {
        var point = Prefix + Number(latitude) + "," + Number(longitude);
        return CleanLabel(label) is { } clean ? point + "\n" + clean : point;
    }

    /// <summary>Reads a location message's text back; null for anything else (a purged, deleted or removed message has none).</summary>
    public static ChatLocation? Read(string? body)
    {
        if (string.IsNullOrEmpty(body) || !body.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        var newline = body.IndexOf('\n');
        var point = (newline < 0 ? body[Prefix.Length..] : body[Prefix.Length..newline]).Split(',');
        if (point.Length != 2 || !double.TryParse(point[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(point[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) return null;
        var label = newline < 0 ? null : CleanLabel(body[(newline + 1)..]);
        return Check(lat, lon, label) == ChatLocationRefusal.None ? new(lat, lon, label) : null;
    }

    private static string Number(double value) => Math.Round(value, Decimals).ToString("0.#####", CultureInfo.InvariantCulture);
}
