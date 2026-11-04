using System.Reflection;

namespace Swoovr.Web;

/// <summary>issue 177: the single Swoovr version (Directory.Build.props SwoovrVersion), with the short commit when the build
/// recorded one, e.g. "1.0.0 · build 3c6bad98".</summary>
public static class SwoovrBuild
{
    public static string Display { get; } = Describe(typeof(SwoovrBuild).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    public static string Describe(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational)) return "unknown";
        var parts = informational.Split('+', 2);
        return parts.Length == 2 && parts[1].Length > 0 ? $"{parts[0]} · build {parts[1][..Math.Min(8, parts[1].Length)]}" : parts[0];
    }
}
