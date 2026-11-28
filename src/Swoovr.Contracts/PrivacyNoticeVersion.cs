using System.Globalization;

namespace Swoovr.Contracts;

/// <summary>The privacy notice in force (text: Swoovr.Client.PrivacyNotice), where hosts can check it too.</summary>
public static class PrivacyNoticeVersion
{
    public const string Current = "0.22, 28 November 2026";

    /// <summary>Issue 488 (D413): staff may view members' ID documents only once notice 0.21 or later is in force.</summary>
    public static bool CoversIdReview(string version, DateOnly today) => InForce(version, today, new Version(0, 21));

    /// <summary>D427 (#530): Photo Verified (the photo holding the code) reaches members only once notice 0.22 or later is in force.</summary>
    public static bool CoversPhotoVerification(string version, DateOnly today) => InForce(version, today, new Version(0, 22));

    private static bool InForce(string version, DateOnly today, Version minimum)
    {
        var parts = version.Split(',', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2 && Version.TryParse(parts[0], out var number) && number >= minimum
            && DateOnly.TryParseExact(parts[1], "d MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var from) && from <= today;
    }
}
