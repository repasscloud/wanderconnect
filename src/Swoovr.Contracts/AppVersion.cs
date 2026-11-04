using System.Reflection;
using System.Text.RegularExpressions;

namespace Swoovr.Contracts;

/// <summary>
/// Q129 / D320: the Swoovr product version, Major.Minor.Build, read from the compiled assembly (Directory.Build.props),
/// so every app reports what was actually built, offline, with no network call. The product version is never the native
/// store build number, the API date version (Swoovr-Version), a deployment id or the database schema version.
/// </summary>
public sealed partial record AppVersion(int Major, int Minor, int Build, string? CommitSha)
{
    /// <summary>"1.4.28".</summary>
    public string ProductVersion => $"{Major}.{Minor}.{Build}";
    /// <summary>"Version 1.4.28": the public footer and app display. Never includes the commit.</summary>
    public string DisplayVersion => $"Version {ProductVersion}";
    /// <summary>"1.4.28+&lt;commit&gt;" for internal diagnostics only.</summary>
    public string DiagnosticVersion => CommitSha is null ? ProductVersion : $"{ProductVersion}+{CommitSha}";

    /// <summary>The version this build was compiled with.</summary>
    public static AppVersion Current { get; } = Parse(typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
    /// <summary>For XAML (x:Static) and Razor without an instance.</summary>
    public static string CurrentDisplayVersion => Current.DisplayVersion;

    /// <summary>Parses an informational version "X.Y.Z" or "X.Y.Z+commit"; anything else is a build error, not a guess.</summary>
    public static AppVersion Parse(string? informational)
    {
        var match = Pattern().Match(informational ?? "");
        if (!match.Success) throw new InvalidOperationException("The compiled Swoovr version is not Major.Minor.Build.");
        return new(int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value), int.Parse(match.Groups[3].Value),
            match.Groups[4].Success ? match.Groups[4].Value.ToLowerInvariant() : null);
    }

    [GeneratedRegex(@"^(0|[1-9]\d{0,8})\.(0|[1-9]\d{0,8})\.(0|[1-9]\d{0,8})(?:\+([0-9A-Fa-f]{7,64}))?$")]
    private static partial Regex Pattern();
}

/// <summary>Public GET /version body (Q129): the deployed API's compiled product version, nothing operational.</summary>
public sealed record VersionView(string Version, int Major, int Minor, int Build)
{
    public static VersionView From(AppVersion version) => new(version.ProductVersion, version.Major, version.Minor, version.Build);
}
