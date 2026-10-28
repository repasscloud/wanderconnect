using System.Globalization;

namespace Swoovr.Contracts;

/// <summary>Published Swoovr wire contracts. Dates change only for breaking contracts, never for a deployment.</summary>
public static class ApiContract
{
    public const string Header = "Swoovr-Version";
    public const string Initial = "2026-10-28";
    public const string Current = Initial;
    public static IReadOnlyList<string> Supported { get; } = Array.AsReadOnly(new[] { Initial });

    // D222: missing always means Initial, even after Current advances. Do not treat an explicit empty value as missing.
    public static ApiContractSelection Select(string? requested)
    {
        if (requested is null) return new(Initial, null);
        if (requested.Length != 10 || !DateOnly.TryParseExact(requested, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date) || date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) != requested)
            return new(null, "invalid-api-version");
        return Supported.Contains(requested, StringComparer.Ordinal)
            ? new(requested, null) : new(null, "unsupported-api-version");
    }
}

public sealed record ApiContractSelection(string? Version, string? Code);
