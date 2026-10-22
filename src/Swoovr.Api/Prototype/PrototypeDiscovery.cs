using Swoovr.Contracts;
using Swoovr.Domain;

namespace Swoovr.Api.Prototype;

// Explicitly isolated fictional data. This module must never read application/provider storage.
internal static class PrototypeDiscovery
{
    private static readonly Guid ViewerId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid AreaId = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly HashSet<DiscoveryContext> Contexts = [DiscoveryContext.Connect, DiscoveryContext.Friends, DiscoveryContext.Travel];
    private static readonly DiscoveryAccessPolicy Policy = new();
    private sealed record Person(Guid Id, string Name, int Age, string Bio, bool Substantive,
        bool DemoImage, bool IncognitoAccepted, bool Active, bool Blocked,
        IReadOnlySet<ConnectionIntention> Applicable, IReadOnlyList<string> Displayed);
    private static Person Fixture(int index, string name, int age, string bio,
        bool substantive = true, bool image = false, bool acceptsIncognito = true,
        bool active = true, bool blocked = false, bool fwb = false, bool friend = false) =>
        new(Guid.Parse($"10000000-0000-0000-0000-{index:000000000000}"), name, age, bio,
            substantive, image, acceptsIncognito, active, blocked,
            new HashSet<ConnectionIntention>(new[] { ConnectionIntention.LongTermRelationship }
                .Concat(fwb ? [ConnectionIntention.FriendsWithBenefits] : Array.Empty<ConnectionIntention>())
                .Concat(friend ? [ConnectionIntention.Friendship] : Array.Empty<ConnectionIntention>())),
            friend ? ["Long-term relationship", "Friendship"] : ["Long-term relationship"]);
    private static readonly Person[] People = [
        Fixture(1, "Morgan", 35, "Fictional hidden intention exclusion fixture.", fwb: true),
        Fixture(2, "Jamie", 34, "A coastal walk, a good book and a conversation that wanders. Happiest finding small places with big stories.", friend: true),
        Fixture(3, "Rowan", 40, "Still writing my introduction.", substantive: false, image: true),
        Fixture(4, "Casey", 29, "Weekend markets, ambitious recipes and company for the next gallery opening.", acceptsIncognito: false),
        Fixture(5, "Taylor", 31, "Fictional inactive fixture.", active: false),
        Fixture(6, "Quinn", 37, "Fictional blocked fixture.", blocked: true)
    ];

    private sealed record Options(string Travel, bool Incognito, bool HideSparse, bool RequireFriendship,
        RequiredIntentionMatch Match, bool Disclose, bool RequireActivity);
    private static Options? Read(IQueryCollection query)
    {
        var allowed = new HashSet<string> { "travel", "incognito", "hideSparse", "requireFriendship", "match", "disclose", "requireActivity" };
        if (query.Any(pair => !allowed.Contains(pair.Key) || pair.Value.Count != 1)) return null;
        var travel = query["travel"].FirstOrDefault() ?? "local";
        var match = query["match"].FirstOrDefault() ?? "any";
        if (travel is not ("local" or "upcoming" or "arrived") || match is not ("any" or "all")) return null;
        foreach (var key in new[] { "incognito", "hideSparse", "requireFriendship", "disclose", "requireActivity" })
            if (query.ContainsKey(key) && query[key] is not ["true"] and not ["false"]) return null;
        return new(travel, query["incognito"] == "true", query["hideSparse"] == "true",
            query["requireFriendship"] == "true", match == "all" ? RequiredIntentionMatch.All : RequiredIntentionMatch.Any,
            query["disclose"] == "true", query["requireActivity"] == "true");
    }

    private static bool Allows(Person person, Options options, DiscoveryResource resource)
    {
        var now = DateTimeOffset.UtcNow;
        var required = new HashSet<ConnectionIntention> { ConnectionIntention.LongTermRelationship };
        if (options.RequireFriendship) required.Add(ConnectionIntention.Friendship);
        if (options.RequireActivity) required.Add(ConnectionIntention.ActivityCompanion);
        var viewerRules = new DiscoveryRules(true, true, true, options.HideSparse,
            new(required, options.Match, new HashSet<ConnectionIntention> { ConnectionIntention.FriendsWithBenefits, ConnectionIntention.CasualConnection }));
        var ownerRules = new DiscoveryRules(true, true, person.IncognitoAccepted, false,
            new(new HashSet<ConnectionIntention>(), RequiredIntentionMatch.Any, new HashSet<ConnectionIntention>()));
        var request = new DiscoveryAccessRequest(
            new(AccountRole.Member, SignInMethod.EmailPassword, false, true, new HashSet<Guid>()), ViewerId,
            new(ViewerId, true, true, true, true, Contexts, new HashSet<ConnectionIntention> { ConnectionIntention.LongTermRelationship, ConnectionIntention.Friendship }),
            new(person.Id, person.Active, true, true, person.Substantive, Contexts, person.Applicable),
            viewerRules, ownerRules, options.Travel == "local" ? DiscoveryContext.Connect : DiscoveryContext.Travel,
            resource, options.Incognito, person.Blocked, true, true, options.Disclose,
            options.Travel != "local", AreaId, options.Travel == "arrived"
                ? new(ViewerId, AreaId, now.AddSeconds(-1), now.AddMinutes(5), true, true) : null);
        // Every true identity/location/session value above is SIMULATION, never provider evidence.
        return Policy.Allows(request, now);
    }
    private static PrototypePerson Project(Person person, Options options) => new(person.Id,
        person.Name, person.Age, person.Bio, "Harbour neighbourhood · fictional area", person.Displayed,
        person.DemoImage, Allows(person, options, DiscoveryResource.Contact), true);

    public static void Map(WebApplication app)
    {
        if (!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Prototype:Enabled")) return;
        var group = app.MapGroup("/api/v1/prototype");
        group.AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            context.HttpContext.Response.Headers.XContentTypeOptions = "nosniff";
            return await next(context);
        });
        group.MapGet("/people", (HttpRequest request) => {
            var options = Read(request.Query);
            return options is null ? Results.BadRequest() : Results.Ok(new PrototypePeople(true,
                "Fictional adult viewer: long-term relationship required; casual and FWB excluded.",
                People.Where(person => Allows(person, options, DiscoveryResource.List)).Select(person => Project(person, options)).ToArray()));
        });
        group.MapGet("/people/{id:guid}", (Guid id, HttpRequest request) => {
            var options = Read(request.Query);
            if (options is null) return Results.BadRequest();
            var person = People.SingleOrDefault(p => p.Id == id);
            return person is not null && Allows(person, options, DiscoveryResource.Profile)
                ? Results.Ok(Project(person, options)) : Results.NotFound();
        });
        group.MapGet("/people/{id:guid}/media", (Guid id, HttpRequest request) => {
            var options = Read(request.Query);
            if (options is null) return Results.BadRequest();
            var person = People.SingleOrDefault(p => p.Id == id);
            return person is not null && person.DemoImage && Allows(person, options, DiscoveryResource.Media)
                ? Results.Text("<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 480 280\"><rect width=\"480\" height=\"280\" fill=\"#EEE3FF\"/><circle cx=\"240\" cy=\"115\" r=\"52\" fill=\"#7132D0\"/><path d=\"M100 280a140 115 0 01280 0\" fill=\"#7132D0\"/><text x=\"240\" y=\"260\" text-anchor=\"middle\" fill=\"white\" font-family=\"sans-serif\" font-size=\"18\">Demo image placeholder</text></svg>", "image/svg+xml")
                : Results.NotFound();
        });
        group.MapGet("/people/{id:guid}/contact-eligibility", (Guid id, HttpRequest request) => {
            var options = Read(request.Query);
            if (options is null) return Results.BadRequest();
            var person = People.SingleOrDefault(p => p.Id == id);
            return person is not null && Allows(person, options, DiscoveryResource.Contact)
                ? Results.Ok(new PrototypeContact(true, true, "Demo contact is eligible. No request or message was sent.")) : Results.NotFound();
        });
    }
}
