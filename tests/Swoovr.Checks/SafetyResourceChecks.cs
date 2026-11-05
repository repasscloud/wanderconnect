using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>
/// Country-aware Safety &amp; Support (#103, D204): the country comes from a manual choice, else Cloudflare's header only
/// where the host is behind Cloudflare, else the fallback, else unknown; unknown or unsupported countries never borrow
/// another country's numbers; launch readiness needs a verified emergency number and current reviews.
/// </summary>
static class SafetyResourceChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Safety resource check failed: " + what); checks++; }
        var today = new DateOnly(2026, 10, 28);

        // Country resolution (API and host helpers agree).
        Expect(SafetyPolicy.Resolve("nz", "AU", true, "AU") == new SafetyCountryChoice("NZ", "manual"), "a manual choice wins");
        Expect(SafetyPolicy.Resolve(null, "GB", true, "AU") == new SafetyCountryChoice("GB", "cloudflare"), "Cloudflare's country when trusted");
        Expect(SafetyPolicy.Resolve(null, "GB", false, "AU") == new SafetyCountryChoice("AU", "fallback"), "a client-supplied header is never trusted");
        Expect(SafetyPolicy.Resolve(null, "XX", true, null) == new SafetyCountryChoice(null, "unknown"), "XX is unknown");
        Expect(SafetyPolicy.Resolve(null, "T1", true, null).Code is null, "Tor (T1) is unknown");
        Expect(SafetyPolicy.Resolve("<script>", null, false, null) == new SafetyCountryChoice(null, "unknown"), "malformed is not a country");
        Expect(SafetyText.Default("GB", false, "AU") == ("AU", "fallback") && SafetyText.Default("GB", true, "AU") == ("GB", "cloudflare")
            && SafetyText.Default("XX", true, null) == (null, "unknown"), "the web host's default follows the same rule");
        Expect(SafetyText.Manual("nz") == "NZ" && SafetyText.Manual("NZL") is null, "manual choices are two letters");

        // Validation.
        var good = new SafetyResource { CountryCode = "AU", Name = "Lifeline", Description = "24/7 crisis support.", Phone = "131114", SourceUrl = "https://www.lifeline.org.au/", LanguageCode = "en-AU" };
        Expect(SafetyPolicy.CheckResource(good) is null, "a good resource saves");
        Expect(SafetyPolicy.CheckResource(new SafetyResource { CountryCode = "AU", Name = "x", Description = "y", Phone = "13 11 14", SourceUrl = "https://a.example/", LanguageCode = "en" }) == "phone", "dialable digits only");
        Expect(SafetyPolicy.CheckResource(new SafetyResource { CountryCode = "AU", Name = "x", Description = "y", WebsiteUrl = "http://insecure.example/", SourceUrl = "https://a.example/", LanguageCode = "en" }) == "website", "https only");
        Expect(SafetyPolicy.CheckResource(new SafetyResource { CountryCode = "AU", Name = "x", Description = "y", Phone = "000", SourceUrl = "javascript:alert(1)", LanguageCode = "en" }) == "source", "a real source");
        Expect(SafetyPolicy.CheckResource(new SafetyResource { CountryCode = "AU", Name = "x", Description = "y", SourceUrl = "https://a.example/", LanguageCode = "en" }) == "contact", "a phone or a website");
        Expect(SafetyPolicy.CheckResource(new SafetyResource { CountryCode = "au", Name = "x", Description = "y", Phone = "000", SourceUrl = "https://a.example/", LanguageCode = "en" }) == "country", "upper-case ISO");

        // Seeds and readiness.
        var au = SafetySeed.Countries.Single(c => c.Code == "AU");
        var auResources = SafetySeed.Resources.Where(r => r.CountryCode == "AU").ToList();
        Expect(au.Readiness == SafetyReadiness.Ready && SafetyPolicy.LaunchProblems(au, auResources, today).Count == 0, "AU is launch-ready (owner-approved list, D190)");
        Expect(SafetySeed.Countries.Single(c => c.Code == "NZ").Readiness == SafetyReadiness.NotReady, "NZ is populated but not launch-ready");
        Expect(SafetySeed.Countries.Single(c => c.Code == "GB").Readiness == SafetyReadiness.Demo, "GB is demo only");
        var nz = SafetySeed.Countries.Single(c => c.Code == "NZ");
        Expect(SafetyPolicy.LaunchProblems(nz, SafetySeed.Resources.Where(r => r.CountryCode == "NZ").ToList(), today).Contains("unverified-resources"), "NZ can't be launched unverified");
        Expect(SafetyPolicy.LaunchProblems(au, auResources.Where(r => r.Category != SafetyResourceCategory.Emergency).ToList(), today).Contains("no-verified-emergency-number"), "no emergency number, no launch");
        Expect(SafetyPolicy.LaunchProblems(au, auResources, new DateOnly(2027, 6, 1)).Contains("pack-review-overdue"), "an overdue review stops a launch");
        Expect(SafetyPolicy.Stale(au, auResources, new DateOnly(2027, 6, 1)) && !SafetyPolicy.Stale(au, auResources, today), "overdue packs show the warning");
        Expect(SafetySeed.Resources.All(r => SafetyPolicy.CheckResource(r) is null), "every seed is valid");

        // What a page shows.
        var unknown = new SafetyPack(null, null, "unknown", "unknown", true, 0, null, null, DateTimeOffset.UtcNow, [], []);
        Expect(SafetyText.Warning(unknown) is null && !SafetyText.Neutral.Any(char.IsDigit), "neutral guidance names no number");
        Expect(SafetyText.Warning(unknown with { Readiness = "demo", Stale = true }) == SafetyText.DemoWarning, "demo warning");
        Expect(SafetyText.Warning(unknown with { Readiness = "not-ready", Stale = true }) == SafetyText.StaleWarning, "not-ready warning");
        Expect(SafetyText.Hint("cloudflare").Contains("VPN") && SafetyText.Hint("manual").Contains("manually"), "the hint says where the country came from");

        // Issue 217: a member's choice, then their Home City's country, then the host default.
        var adelaide = new PlaceView(2078025, "Adelaide", "South Australia", "Australia", "Adelaide, South Australia, Australia", "au");
        Expect(SafetyText.HomeCountry(adelaide) == "AU" && SafetyText.HomeCountry(null) is null && SafetyText.HomeCountry(adelaide with { CountryCode = null }) is null, "the Home City's ISO code");
        Expect(SafetyText.Choose("nz", "AU", "GB", "cloudflare") == ("NZ", "manual"), "a choice wins over the Home City");
        Expect(SafetyText.Choose(null, "AU", "GB", "cloudflare") == ("AU", "home"), "the Home City wins over the network");
        Expect(SafetyText.Choose(null, null, "GB", "cloudflare") == ("GB", "cloudflare") && SafetyText.Choose("bad", "XX", null, "unknown") == (null, "unknown"), "else the host default");
        Expect(new[] { "connected", "pending", "blocked", "deleted", "disconnected", "ended", "declined" }.Select(SafetyText.PeopleGroup).Distinct().Order().SequenceEqual(["blocked", "connected", "deleted", "disconnected"]), "every state has a filter");
        var person = new SafetyPerson("conversation", Guid.Parse("11111111-1111-1111-1111-111111111111"), "Jordan", "blocked", DateTimeOffset.UtcNow);
        Expect(SafetyText.ReportPath(person, null, "people") == "/me/safety/report?type=conversation&id=11111111-1111-1111-1111-111111111111&state=blocked&from=people", "a report link returns to its list");
        Expect(SafetyText.Query("https://swoovr.app/me/safety/report?type=profile&id=a%20b", "id") == "a b" && SafetyText.Query("https://swoovr.app/x", "id") is null, "query values");
        Console.WriteLine($"PASS: country-aware safety resources ({checks}).");
    }
}
