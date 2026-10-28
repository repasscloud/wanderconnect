using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>
/// Safety &amp; Support wording and helpers shared by the web and the app (#103, D204), so every client says the same thing.
/// </summary>
public static partial class SafetyText
{
    public const string Title = "Safety & Support";
    public const string External = "These are outside services, not Swoovr. Opening one doesn't send them anything about you or your reports.";
    public const string Neutral = "If you're in immediate danger, contact your local emergency services now. We don't have checked contacts for this country yet, so we won't guess a number. Choose a country below to see the contacts we have.";
    public const string StaleWarning = "These contacts haven't been fully checked by Swoovr or are due for review. Check the number or website before relying on it.";
    public const string DemoWarning = "Demonstration only: these contacts are examples and not checked for use.";
    public const string SwoovrActions = "On Swoovr";
    public const string SwoovrHelp = "To report or block someone, open their profile or your conversation and choose Report or Block. For assault, violence, threats, stalking, coercion or child exploitation, report a serious safety incident: we preserve the evidence and review it urgently.";
    public const string OfflineCacheKey = "swoovr.safety.pack";
    public const string ChoiceKey = "swoovr.safety.country";
    /// <summary>DEFAULT (D204): an offline copy older than this is shown with the stale warning.</summary>
    public static readonly TimeSpan OfflineFreshFor = TimeSpan.FromDays(7);

    [GeneratedRegex("^[A-Za-z]{2}$")] private static partial Regex Iso();

    /// <summary>
    /// The country a host should ask for when the visitor hasn't chosen one: Cloudflare's CF-IPCountry only when the host
    /// is behind Cloudflare and says so (D153), else its configured fallback, else none. A client-supplied header never
    /// counts unless the host is behind Cloudflare, which strips and sets it.
    /// </summary>
    public static (string? Country, string Source) Default(string? cloudflareHeader, bool cloudflareTrusted, string? fallback)
    {
        static string? Clean(string? value) => value is not null && Iso().IsMatch(value.Trim()) && value.Trim().ToUpperInvariant() is var v && v != "XX" ? v : null;
        if (cloudflareTrusted && Clean(cloudflareHeader) is { } network) return (network, "cloudflare");
        if (Clean(fallback) is { } configured) return (configured, "fallback");
        return (null, "unknown");
    }

    public static string? Manual(string? value) => value is not null && Iso().IsMatch(value.Trim()) ? value.Trim().ToUpperInvariant() : null;

    public static string Hint(string source) => source switch
    {
        "manual" => "Country selected manually.",
        "cloudflare" => "Country based on your network — change it if it's wrong (a VPN can change it).",
        "fallback" => "Country set for this service — change it if it's wrong.",
        _ => "We couldn't tell your country. Choose one.",
    };

    public static string Category(string category) => category switch
    {
        "emergency" => "Emergency",
        "police-non-emergency" => "Police (not an emergency)",
        "crisis" => "Crisis support",
        "sexual-violence" => "Sexual assault and violence",
        "domestic-violence" => "Family and domestic violence",
        "online-safety" => "Online abuse",
        "child-exploitation" => "Child exploitation",
        _ => "Other help",
    };

    /// <summary>The warning a pack needs, if any.</summary>
    public static string? Warning(SafetyPack pack) => pack.Readiness switch
    {
        "demo" => DemoWarning,
        "unknown" or "unsupported" => null,
        _ => pack.Stale ? StaleWarning : null,
    };
}

/// <summary>The public Safety &amp; Support read (#103): no sign-in needed. The web host and the app use it; the browser goes through the BFF.</summary>
public sealed class SafetyClient(HttpClient transport)
{
    private readonly HttpClient http = ApiContractClient.Configure(transport);
    public async Task<SafetyPack?> PackAsync(string? country, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/safety/resources" + (country is null ? "" : "?country=" + Uri.EscapeDataString(country)), cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<SafetyPack>(cancellationToken) : null;
    }
}
