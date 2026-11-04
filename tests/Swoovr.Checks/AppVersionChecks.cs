using System.Text.RegularExpressions;
using Swoovr.Contracts;

/// <summary>Q129 / D320: one compiled product version, Major.Minor.Build, presented consistently.</summary>
static class AppVersionChecks
{
    public static void Run()
    {
        static void Need(bool ok, string name) { if (!ok) throw new Exception("App version check failed: " + name); }
        var parsed = AppVersion.Parse("1.4.28+0a1b2c3d4e5f60718293a4b5c6d7e8f901234567");
        Need(parsed is { Major: 1, Minor: 4, Build: 28 } && parsed.ProductVersion == "1.4.28", "components and product version");
        Need(parsed.DisplayVersion == "Version 1.4.28" && !parsed.DisplayVersion.Contains('+'), "public display never shows the commit");
        Need(parsed.DiagnosticVersion == "1.4.28+0a1b2c3d4e5f60718293a4b5c6d7e8f901234567", "diagnostics keep the commit");
        Need(AppVersion.Parse("2.0.0").CommitSha is null && AppVersion.Parse("2.0.0").DiagnosticVersion == "2.0.0", "no commit, no suffix");
        foreach (var bad in new[] { null, "", "1.0", "1.0.0.0", "v1.0.0", "1.0.0-dev", "01.0.0", "1.0.x" })
        {
            var refused = false; try { AppVersion.Parse(bad); } catch (InvalidOperationException) { refused = true; }
            Need(refused, "refuses " + (bad ?? "null"));
        }
        // The compiled version is the one Directory.Build.props defines (development builds use the committed Build).
        var props = File.ReadAllText("Directory.Build.props");
        int Part(string name) => int.Parse(Regex.Match(props, $"<{name}(?: [^>]*)?>(\\d+)</{name}>").Groups[1].Value);
        var current = AppVersion.Current;
        Need(current.Major == Part("SwoovrVersionMajor") && current.Minor == Part("SwoovrVersionMinor"), "compiled Major.Minor matches the canonical definition");
        Need(current.ProductVersion == $"{current.Major}.{current.Minor}.{current.Build}" && AppVersion.CurrentDisplayVersion == "Version " + current.ProductVersion, "current version is consistent");
        var view = VersionView.From(current);
        Need(view.Version == $"{view.Major}.{view.Minor}.{view.Build}" && view.Version == current.ProductVersion, "GET /version components match the version string");
        Console.WriteLine($"PASS: app version {current.ProductVersion} (Major.Minor.Build, display, diagnostics, /version).");
    }
}
