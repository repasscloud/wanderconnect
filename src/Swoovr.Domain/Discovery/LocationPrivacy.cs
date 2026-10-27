using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>
/// Location privacy (internal policy §10; #90). Distances are between home-city centres and never leave the server; what
/// members can learn is limited to rounded bands. The Plus distance filter snaps to a band, so changing it a kilometre at a
/// time can't narrow anyone down, and searches and filter changes are limited per hour so repeated queries can't be used
/// to triangulate a home, workplace or routine.
/// </summary>
public static class LocationPrivacy
{
    public static readonly IReadOnlyList<int> Bands = [5, 10, 25, 50, 100, 250, 500];
    /// <summary>DEFAULT: filter changes and searches (Explore, Traveller Search) per member per hour.</summary>
    public const int FilterChangesPerHour = 10;
    public const int SearchesPerHour = 60;
    public static readonly TimeSpan Window = TimeSpan.FromHours(1);
    /// <summary>Search events are kept a day for the limits, then go.</summary>
    public static readonly TimeSpan EventRetention = TimeSpan.FromDays(1);

    /// <summary>The smallest band at or above the request (null stays null; above the largest band, the largest).</summary>
    public static int? Snap(int? km) => km is not { } k ? null : Bands.FirstOrDefault(b => b >= k, Bands[^1]);

    /// <summary>The label others may see, from a distance between home cities.</summary>
    public static string? Band(double? km) => km switch
    {
        null => null,
        < 5 => "Under 5 km",
        < 10 => "5–10 km",
        < 25 => "10–25 km",
        < 50 => "25–50 km",
        < 100 => "50–100 km",
        _ => "Over 100 km",
    };

    public static bool Allowed(int recentInWindow, int limit) => recentInWindow < limit;
}

/// <summary>One search or filter change, kept a day for the hourly limits (#90). No query content.</summary>
public sealed class SearchEvent
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>filter, explore or traveller-search.</summary>
    public string Kind { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

/// <summary>
/// A snapshot of what others could see of a profile in one mode (or the core profile, mode "core"), written whenever it
/// changes (#91, policy §6). Moderators see the version visible when a report was made; legal holds capture them.
/// The content is the public presentation only: never the date of birth, home city, orientation or "show me".
/// </summary>
public sealed class ProfileVersion
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
    public DateTimeOffset CapturedAt { get; set; }
    public string ContentJson { get; set; } = "";
    public byte[] Sha256 { get; set; } = [];
}

public static class ProfileVersions
{
    public const string Core = "core";

    public static byte[] Hash(string json) => SHA256.HashData(Encoding.UTF8.GetBytes(json));

    /// <summary>Whether a new snapshot differs from the latest one for that mode (unchanged content isn't stored again).</summary>
    public static bool Changed(ProfileVersion? latest, byte[] hash) => latest is null || !latest.Sha256.SequenceEqual(hash);

    /// <summary>The versions visible at a moment: for each mode, the latest captured at or before it.</summary>
    public static IReadOnlyList<ProfileVersion> VisibleAt(IEnumerable<ProfileVersion> versions, DateTimeOffset at) =>
        versions.Where(v => v.CapturedAt <= at).GroupBy(v => v.ModeId).Select(g => g.OrderByDescending(v => v.CapturedAt).First()).OrderBy(v => v.ModeId).ToList();
}
