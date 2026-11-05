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
    // issue 181 §7: accurate and said once; opening a website is an ordinary browser request, so no absolute privacy claim.
    public const string External = "These organisations operate independently of Swoovr. Contacting them through their websites or telephone services is subject to their respective privacy policies.";
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

    /// <summary>
    /// Issue 217: a signed-in member's default country is their Home City's (Me → Profile), after any country they chose
    /// themselves and before the host's network or configured default. Choosing another country never changes the Home City.
    /// </summary>
    public static string? HomeCountry(PlaceView? homeCity) => Manual(homeCity?.CountryCode) is { } code && code != "XX" ? code : null;

    /// <summary>Issue 217's order: an explicit or remembered choice, the Home City's country, then the host default.</summary>
    public static (string? Country, string Source) Choose(string? manual, string? home, string? hostDefault, string hostSource) =>
        Manual(manual) is { } chosen ? (chosen, "manual") : HomeCountry(home) is { } own ? (own, "home") : (hostDefault, hostSource);

    private static string? HomeCountry(string? code) => Manual(code) is { } iso && iso != "XX" ? iso : null;

    public const string SeriousPeopleHelp = "You can report people you've blocked, disconnected from, or whose account was deleted. They're never told.";

    /// <summary>Me → Privacy & safety → People you've dealt with, keeping a meeting to include in the report.</summary>
    public static string PeoplePath(string? meeting) => "/me/safety/people" + (meeting is null ? "" : "?meeting=" + Uri.EscapeDataString(meeting));

    /// <summary>The serious-incident report page for one person; "from" is where Back and Cancel return (hub or people).</summary>
    public static string ReportPath(SafetyPerson person, string? meeting, string from) =>
        $"/me/safety/report?type={Uri.EscapeDataString(person.SubjectType)}&id={person.SubjectId}&state={Uri.EscapeDataString(person.State)}"
        + (meeting is null ? "" : "&meeting=" + Uri.EscapeDataString(meeting)) + (from == "people" ? "&from=people" : "");

    /// <summary>"Chat" or "Chat request": how the member dealt with them.</summary>
    public static string Via(string subjectType) => subjectType == "conversation" ? "Chat" : "Chat request";

    /// <summary>One query value from a URL (WebAssembly-safe; no System.Web).</summary>
    public static string? Query(string uri, string key)
    {
        var query = new Uri(uri).Query.TrimStart('?');
        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            if (parts.Length == 2 && Uri.UnescapeDataString(parts[0]) == key) return Uri.UnescapeDataString(parts[1].Replace('+', ' '));
        }
        return null;
    }

    /// <summary>The People you've dealt with filters (issue 217): every state the API reports falls in one of them.</summary>
    public static string PeopleGroup(string state) => state switch
    {
        "connected" or "pending" => "connected",
        "blocked" => "blocked",
        "deleted" => "deleted",
        _ => "disconnected",
    };

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
