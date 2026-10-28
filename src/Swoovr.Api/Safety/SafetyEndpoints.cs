using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Safety;

/// <summary>
/// Country-aware Safety &amp; Support (#103, D204). One public read route for every client, signed in or not: the country is
/// the caller's choice, else Cloudflare's CF-IPCountry when the host is behind Cloudflare and says so (D153, never in
/// Development), else `Safety:FallbackCountry` (AU locally), else unknown. Nothing about the request (IP, country) is
/// stored or logged, and nothing on the member's profile, home city, Travel or Explore changes. Admins edit the packs;
/// every change is an immutable revision and audited with ids.
/// </summary>
public static class SafetyEndpoints
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/safety/resources", async (string? country, HttpContext http, IServiceProvider services, IConfiguration configuration,
            IHostEnvironment environment, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            if (country is not null && SafetyPolicy.Normalise(country) is null && country.Trim().ToUpperInvariant() is not ("XX" or "T1"))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownCountry, "Use a two-letter country code.");
            if (services.GetService<SwoovrDbContext>() is not { } db)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Safety resources aren't available right now.");
            var choice = SafetyPolicy.Resolve(country, http.Request.Headers["CF-IPCountry"], CloudflareTrusted(configuration, environment),
                configuration["Safety:FallbackCountry"]);
            if (country is not null && SafetyPolicy.Normalise(country) is null) choice = new(null, "manual");
            http.Response.Headers.CacheControl = "private, max-age=300";
            http.Response.Headers.Vary = "CF-IPCountry";
            return Results.Ok(await PackAsync(db, choice, clock.GetUtcNow(), cancellationToken));
        });

        var admin = app.MapGroup("/api/admin/safety").RequireAuthorization();
        admin.MapGet("/countries", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await AdminAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var resources = await db.SafetyResources.ToListAsync(cancellationToken);
            return Results.Ok((await db.SafetyCountries.OrderBy(c => c.Name).ToListAsync(cancellationToken))
                .Select(c => CountryView(c, resources.Where(r => r.CountryCode == c.Code).ToList(), today)).ToList());
        });
        admin.MapPut("/countries/{code}", async (string code, SafetyCountryUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await AdminAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (db, now) = (accounts.Db!, clock.GetUtcNow());
            var today = DateOnly.FromDateTime(now.UtcDateTime);
            if (SafetyPolicy.Normalise(code) is not { } iso) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownCountry, "Use a two-letter country code.");
            if (string.IsNullOrWhiteSpace(update.Name) || update.Name.Length > SafetyPolicy.NameMax || update.Owner is { Length: > SafetyPolicy.NameMax }
                || update.Notes is { Length: > SafetyPolicy.NotesMax } || ReadinessOf(update.Readiness) is not { } readiness
                || update.LastReviewedOn is { } reviewed && update.ReviewDueOn is { } due && due < reviewed)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidResource, "Check the country's details.");
            var country = await db.SafetyCountries.FindAsync([iso], cancellationToken);
            if (country is null) db.SafetyCountries.Add(country = new SafetyCountry { Code = iso });
            (country.Name, country.Enabled, country.Readiness, country.LastReviewedOn, country.ReviewDueOn, country.Owner, country.Notes) =
                (update.Name.Trim(), update.Enabled, readiness, update.LastReviewedOn, update.ReviewDueOn, update.Owner?.Trim(), update.Notes?.Trim());
            var resources = await db.SafetyResources.Where(r => r.CountryCode == iso).ToListAsync(cancellationToken);
            // #103: a pack is launch-ready only with a verified emergency number and everything reviewed.
            if (readiness == SafetyReadiness.Ready && SafetyPolicy.LaunchProblems(country, resources, today) is { Count: > 0 } problems)
                return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "This country isn't ready to launch.",
                    extensions: new Dictionary<string, object?> { ["code"] = "country-not-ready", ["problems"] = problems });
            (country.Version, country.UpdatedAt) = (country.Version + 1, now);
            Revise(db, iso, null, "country.updated", current!.Account!.Id, now, country);
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(CountryView(country, resources, today));
        });
        admin.MapGet("/countries/{code}/resources", async (string code, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await AdminAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var iso = SafetyPolicy.Normalise(code);
            return Results.Ok((await accounts.Db!.SafetyResources.Where(r => r.CountryCode == iso).OrderBy(r => r.DisplayOrder).ThenBy(r => r.Name)
                .ToListAsync(cancellationToken)).Select(AdminView).ToList());
        });
        admin.MapPost("/resources", (SafetyResourceEdit edit, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
            SaveAsync(null, edit, user, accounts, clock, cancellationToken));
        admin.MapPut("/resources/{id:guid}", (Guid id, SafetyResourceEdit edit, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
            SaveAsync(id, edit, user, accounts, clock, cancellationToken));
        admin.MapGet("/revisions", async (string? country, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await AdminAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var iso = SafetyPolicy.Normalise(country);
            return Results.Ok(await accounts.Db!.SafetyRevisions.Where(r => iso == null || r.CountryCode == iso).OrderByDescending(r => r.Id).Take(200)
                .Select(r => new SafetyRevisionView(r.Id, r.CountryCode, r.ResourceId, r.Action, r.ActorAccountId, r.At, r.SnapshotJson)).ToListAsync(cancellationToken));
        });
    }

    /// <summary>D153: Cloudflare's headers count only where the host is behind Cloudflare and says so, never in Development.</summary>
    public static bool CloudflareTrusted(IConfiguration configuration, IHostEnvironment environment) =>
        !environment.IsDevelopment() && configuration.GetValue("Location:CloudflareHeaders", false);

    public static async Task<SafetyPack> PackAsync(SwoovrDbContext db, SafetyCountryChoice choice, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var countries = await db.SafetyCountries.Where(c => c.Enabled).OrderBy(c => c.Name).ToListAsync(cancellationToken);
        var options = countries.Select(c => new SafetyCountryOption(c.Code, c.Name, Wire(c.Readiness))).ToList();
        var country = countries.FirstOrDefault(c => c.Code == choice.Code);
        if (country is null)
            return new(choice.Code, null, choice.Source, choice.Code is null ? "unknown" : "unsupported", true, 0, null, null, now, [], options);
        var resources = await db.SafetyResources.Where(r => r.CountryCode == country.Code && r.Enabled).OrderBy(r => r.DisplayOrder).ThenBy(r => r.Name)
            .ToListAsync(cancellationToken);
        // Emergency first, whatever order an editor gave.
        resources = resources.OrderBy(r => r.Category == SafetyResourceCategory.Emergency ? 0 : 1).ThenBy(r => r.DisplayOrder).ToList();
        return new(country.Code, country.Name, choice.Source, Wire(country.Readiness), SafetyPolicy.Stale(country, resources, today), country.Version,
            country.LastReviewedOn, country.ReviewDueOn, now,
            resources.Select(r => new SafetyResourceView(r.Id, Wire(r.Category), r.Name, r.Description, r.Phone, r.PhoneDisplay, r.WebsiteUrl, r.RegionCode,
                r.LanguageCode, r.DisplayOrder, r.LastVerifiedOn)).ToList(), options);
    }

    private static async Task<IResult> SaveAsync(Guid? id, SafetyResourceEdit edit, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var (current, denied) = await AdminAsync(user, accounts, cancellationToken);
        if (denied is not null) return denied;
        var (db, now) = (accounts.Db!, clock.GetUtcNow());
        var resource = id is { } existing ? await db.SafetyResources.FindAsync([existing], cancellationToken) : new SafetyResource { Id = Guid.NewGuid() };
        if (resource is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.InvalidResource, "That resource doesn't exist.");
        var iso = SafetyPolicy.Normalise(edit.Country);
        if (iso is null || await db.SafetyCountries.FindAsync([iso], cancellationToken) is null)
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownCountry, "Add the country first.");
        if (id is not null && resource.CountryCode != iso) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidResource, "A resource stays in its country.");
        if (CategoryOf(edit.Category) is not { } category) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidResource, "Choose a category.");
        (resource.CountryCode, resource.RegionCode, resource.LanguageCode, resource.Category, resource.Name, resource.Description, resource.Phone, resource.PhoneDisplay,
         resource.WebsiteUrl, resource.SourceUrl, resource.DisplayOrder, resource.Enabled, resource.LastVerifiedOn, resource.ReviewDueOn) =
            (iso, Blank(edit.Region), Blank(edit.Language) ?? "en", category, edit.Name?.Trim() ?? "", edit.Description?.Trim() ?? "", Blank(edit.Phone)?.Replace(" ", ""),
             Blank(edit.PhoneDisplay), Blank(edit.WebsiteUrl), edit.SourceUrl?.Trim() ?? "", edit.Order, edit.Enabled, edit.LastVerifiedOn, edit.ReviewDueOn);
        if (SafetyPolicy.CheckResource(resource) is { } problem)
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidResource, $"Check the resource ({problem}).");
        (resource.UpdatedAt, resource.UpdatedBy) = (now, current!.Account!.Id);
        if (id is null) db.SafetyResources.Add(resource);
        var country = (await db.SafetyCountries.FindAsync([iso], cancellationToken))!;
        (country.Version, country.UpdatedAt) = (country.Version + 1, now);
        // A launched pack that no longer meets the rules drops back to not ready rather than showing as verified.
        var all = await db.SafetyResources.Where(r => r.CountryCode == iso && r.Id != resource.Id).ToListAsync(cancellationToken);
        if (country.Readiness == SafetyReadiness.Ready && SafetyPolicy.LaunchProblems(country, [.. all, resource], DateOnly.FromDateTime(now.UtcDateTime)).Count > 0)
        {
            country.Readiness = SafetyReadiness.NotReady;
            Revise(db, iso, null, "country.not-ready", current.Account.Id, now, country);
        }
        Revise(db, iso, resource.Id, id is null ? "resource.created" : "resource.updated", current.Account.Id, now, resource);
        await db.SaveChangesAsync(cancellationToken);
        return id is null ? Results.Created($"/api/admin/safety/resources/{resource.Id}", AdminView(resource)) : Results.Ok(AdminView(resource));
    }

    private static void Revise(SwoovrDbContext db, string country, Guid? resource, string action, Guid actor, DateTimeOffset now, object snapshot)
    {
        db.SafetyRevisions.Add(new SafetyRevision { CountryCode = country, ResourceId = resource, Action = action, ActorAccountId = actor, At = now,
            SnapshotJson = JsonSerializer.Serialize(snapshot, Json) });
        Audit.Record(db, now, actor, "account", "safety." + action, resource is null ? "safety-country" : "safety-resource", resource?.ToString() ?? country);
    }

    private static async Task<(CurrentAccount? Current, IResult? Denied)> AdminAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null) return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        return current.Allows(AccountRole.Admin) ? (current, null) : (null, Results.Forbid());
    }

    private static SafetyCountryAdminView CountryView(SafetyCountry c, List<SafetyResource> resources, DateOnly today) =>
        new(c.Code, c.Name, c.Enabled, Wire(c.Readiness), c.LastReviewedOn, c.ReviewDueOn, c.Owner, c.Notes, c.Version, c.UpdatedAt,
            SafetyPolicy.LaunchProblems(c, resources, today), resources.Count);

    private static SafetyResourceAdminView AdminView(SafetyResource r) => new(r.Id, r.CountryCode, r.RegionCode, r.LanguageCode, Wire(r.Category), r.Name,
        r.Description, r.Phone, r.PhoneDisplay, r.WebsiteUrl, r.SourceUrl, r.DisplayOrder, r.Enabled, r.LastVerifiedOn, r.ReviewDueOn, r.UpdatedAt);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string Wire(SafetyReadiness readiness) => readiness switch { SafetyReadiness.Ready => "ready", SafetyReadiness.Demo => "demo", _ => "not-ready" };
    private static SafetyReadiness? ReadinessOf(string? value) => value switch
        { "ready" => SafetyReadiness.Ready, "not-ready" => SafetyReadiness.NotReady, "demo" => SafetyReadiness.Demo, _ => null };

    public static string Wire(SafetyResourceCategory category) => category switch
    {
        SafetyResourceCategory.Emergency => "emergency", SafetyResourceCategory.PoliceNonEmergency => "police-non-emergency",
        SafetyResourceCategory.Crisis => "crisis", SafetyResourceCategory.SexualViolence => "sexual-violence",
        SafetyResourceCategory.DomesticViolence => "domestic-violence", SafetyResourceCategory.OnlineSafety => "online-safety",
        SafetyResourceCategory.ChildExploitation => "child-exploitation", _ => "other",
    };
    private static SafetyResourceCategory? CategoryOf(string? value) => Enum.GetValues<SafetyResourceCategory>().Cast<SafetyResourceCategory?>()
        .FirstOrDefault(c => Wire(c!.Value) == value);
}
