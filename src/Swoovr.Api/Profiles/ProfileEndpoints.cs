using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Profiles;

/// <summary>
/// Connection modes (D92), the member's own core profile and mode settings, Explore for one mode, and another
/// member's profile. Every rule is checked here (Q12: application services, not the client). Callers must pass the
/// same member gate as /api/v1/me (active, adult-locked accounts refused, beta enrolled). Explore and profiles are local
/// to the viewer's home market (D125); MarketEndpoints holds the market lookups.
/// </summary>
public static class ProfileEndpoints
{
    /// <summary>DEFAULT page size until Explore pages through results.</summary>
    public const int ExploreLimit = 50;

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();
        api.MapGet("/modes", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await CatalogueAsync(accounts.Db!, cancellationToken));
        });
        api.MapGet("/me/profile", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(await OwnAsync(accounts.Db!, current!.Profile!, cancellationToken));
        });
        api.MapPut("/me/profile", async (ProfileUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (name, area, about) = (ProfileTextPolicy.Clean(update.DisplayName), ProfileTextPolicy.Clean(update.Area), ProfileTextPolicy.Clean(update.About));
            if (ProfileTextPolicy.Check(name, area, about) != ProfileTextError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, "Check the profile fields.");
            var (profile, db) = (current!.Profile!, accounts.Db!);
            // D110: the home city must be a GeoNames city from Swoovr's own copy.
            var city = update.HomeCityId is { } home ? await db.GeoCities.FindAsync([home], cancellationToken) : null;
            if (update.HomeCityId is not null && city is null)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownPlace, "Pick your home city from the list.");
            var now = clock.GetUtcNow();
            // D125, D146: the home city decides the home market. Moving into a different market waits 30 days after the
            // last move (Q24 rules 28 and 34); refusals and moves are audited with market ids only, never the city.
            var market = PlaceNames.MarketOf(city, await MarketEndpoints.ZonesAsync(db, cancellationToken))?.Id;
            if (MarketAccess.CanMoveHome(profile.HomeMarketId, market, profile.HomeMarketMovedTo, profile.HomeMarketMovedAt, now) == HomeMove.Locked)
            {
                Audit.Record(db, now, profile.AccountId, "account", "home-market.refused", "account", profile.AccountId.ToString(), $"{profile.HomeMarketId ?? "none"}>{market}");
                await db.SaveChangesAsync(cancellationToken);
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.HomeMarketLocked, "You can move your home city to another Swoovr market once every 30 days.");
            }
            if (market != profile.HomeMarketId)
                Audit.Record(db, now, profile.AccountId, "account", "home-market.changed", "account", profile.AccountId.ToString(), $"{profile.HomeMarketId ?? "none"}>{market ?? "none"}");
            if (market is not null && market != profile.HomeMarketMovedTo) (profile.HomeMarketMovedTo, profile.HomeMarketMovedAt) = (market, now);
            (profile.DisplayName, profile.Area, profile.About, profile.HomeGeoNameId, profile.HomeMarketId) = (name, area, about, update.HomeCityId, market);
            // Interest in a launch follows the home city; it lapses with no home city or once the home market is open.
            if (await db.MarketInterests.FindAsync([profile.AccountId], cancellationToken) is { } interest)
            {
                if (city is null || market is not null && await db.Markets.AnyAsync(m => m.Id == market
                    && (m.Status == MarketStatus.Beta || m.Status == MarketStatus.Active), cancellationToken))
                    db.MarketInterests.Remove(interest);
                else interest.GeoNameId = city.GeoNameId;
            }
            // The audit records that the profile changed, never what it says.
            Audit.Record(db, now, profile.AccountId, "account", "profile.updated", "account", profile.AccountId.ToString());
            if (FounderEligibility.Stamp(profile, current.Account!.Status, now))
                Audit.Record(db, now, null, "system", "founder.eligible", "account", profile.AccountId.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await OwnAsync(db, profile, cancellationToken));
        });
        api.MapPut("/me/modes/{modeId}", async (string modeId, ModeUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (profile, db) = (current!.Profile!, accounts.Db!);
            var mode = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == modeId, cancellationToken);
            var offered = await db.ModeIntentions.Where(i => i.ModeId == modeId).ToListAsync(cancellationToken);
            var stored = await db.MemberModeIntentions.Where(i => i.AccountId == profile.AccountId && i.ModeId == modeId).ToListAsync(cancellationToken);
            var change = ModeSettingsPolicy.Check(mode, update.Intentions ?? [], stored.Select(i => i.IntentionId).ToList(), offered);
            switch (change.Error)
            {
                case ModeChangeError.UnknownMode or ModeChangeError.ModeUnavailable:
                    return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
                case ModeChangeError.TooManyIntentions:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.TooManyIntentions, $"Pick up to {ModeSettingsPolicy.MaxIntentions} intentions.");
                case not ModeChangeError.None:
                    return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidMode, "Check the intentions for this mode.");
            }
            // Q11 rule 49: an official bot switches on only the modes an admin authorised for it.
            if (update.Enabled && profile.Bot && !DiscoveryEligibility.MaySwitchOn(true, await BotModesAsync(db, profile.AccountId, cancellationToken), modeId))
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.BotModeNotAuthorised, "This mode isn't authorised for this account.");
            var now = clock.GetUtcNow();
            var setting = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == profile.AccountId && m.ModeId == modeId, cancellationToken);
            if (setting is null) db.MemberModes.Add(setting = new MemberMode { AccountId = profile.AccountId, ModeId = modeId });
            // Each mode is independent: only this row and its intentions change (Q12 rule 2).
            // D175: this mode's own about (full replacement; blank shows the core about here).
            var about = ProfileTextPolicy.Clean(update.About);
            if (ProfileTextPolicy.Check(null, null, about) != ProfileTextError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidProfile, $"Keep this mode's about to {ProfileTextPolicy.AboutMax} characters of plain text.");
            (setting.Enabled, setting.Visible, setting.About, setting.UpdatedAt) = (update.Enabled, update.Visible, about, now);
            // Only the difference is written, so kept rows (including retired chips, D113) are never touched.
            db.MemberModeIntentions.RemoveRange(stored.Where(i => !change.Stored.Contains(i.IntentionId)));
            db.MemberModeIntentions.AddRange(change.Stored.Where(id => stored.All(i => i.IntentionId != id))
                .Select(id => new MemberModeIntention { AccountId = profile.AccountId, ModeId = modeId, IntentionId = id }));
            Audit.Record(db, now, profile.AccountId, "account", "mode.updated", "connection-mode", modeId,
                $"{(update.Enabled ? "on" : "off")}:{(update.Visible ? "visible" : "hidden")}:intentions:{change.Stored.Count}");
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(await OwnAsync(db, profile, cancellationToken));
        });
        api.MapGet("/explore", async (string? mode, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var selected = await db.ConnectionModes.SingleOrDefaultAsync(m => m.Id == mode && m.Available, cancellationToken);
            if (selected is null) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That connection mode isn't available.");
            // D125: Explore is local. It needs a home city in an open market that offers this mode, and lists only people
            // whose home is in that market (or bots assigned to it). Travel is the way to see anywhere else.
            var (market, refused) = await MarketEndpoints.LocalAsync(db, current!.Profile!, selected.Id, cancellationToken);
            if (refused is not null) return refused;
            var (own, mine, notReady) = await ReadyAsync(db, current.Profile!, selected, clock.GetUtcNow(), cancellationToken);
            if (notReady is not null) return notReady;
            return Results.Ok(new ExploreResult(selected.Id, await PeopleAsync(db, configuration, current.Profile!, selected, own!, mine!, market!.Id, clock.GetUtcNow(), cancellationToken), market.Name));
        });
        // D175: ?mode= is the mode the profile was opened from; the view shows that mode and the modes the pair is connected in.
        api.MapGet("/profiles/{id:guid}", async (Guid id, string? mode, ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var viewer = current!.Profile!.AccountId;
            var person = await Candidates(db, configuration, viewer).SingleOrDefaultAsync(c => c.Profile.PublicId == id, cancellationToken);
            // Shown only in modes both have on and the person made visible; otherwise (including a block either way) it
            // reads as not found.
            var shared = person is null ? [] : await SharedModesAsync(db, viewer, person, cancellationToken);
            if (person is null || shared.Count == 0) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            var (low, high) = ConnectionRequest.Pair(viewer, person.Profile.AccountId);
            var between = await db.ConnectionRequests.Where(r => r.PairLow == low && r.PairHigh == high
                && (r.Status == ConnectionRequestStatus.Pending || r.Status == ConnectionRequestStatus.Accepted)).ToListAsync(cancellationToken);
            // D175: never list the person's other modes; an unshared requested mode reads as not found.
            var shownIds = ModePrivacy.Shown(mode, shared.Select(m => m.Id).ToList(), between.Select(r => r.ModeId).ToHashSet());
            if (shownIds.Count == 0) return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            // D125: the profile also needs the same market, an existing request or connection, or a visit to the viewer's
            // market; anything else is the same 404, so a profile link from another market reveals nothing.
            var reach = await ReachAsync(db, configuration, current!.Profile!, person, shared, between, clock.GetUtcNow(), cancellationToken);
            // D142: a traveller whose Traveller Search is open for the person's market may look before arrival.
            var travel = await Travel.ArrivalEndpoints.ReachAsync(db, current.Profile!, person, shared, clock.GetUtcNow(), cancellationToken);
            if (!MarketAccess.ProfileOpens(reach.SameMarket, between.Count > 0, reach.VisitingModes.Count > 0, travel.SearchModes.Count > 0))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.UnknownMode, "That profile isn't available.");
            var chips = await IntentionLabelsAsync(db, [person.Profile.AccountId], null, cancellationToken);
            var shownAs = Public((await IdentitiesAsync(db, [person.Profile.AccountId], cancellationToken)).GetValueOrDefault(person.Profile.AccountId),
                person.Profile.DateOfBirth is { } dob ? AgePolicy.YearsOn(dob, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)) : null, await GenderLabelsAsync(db, cancellationToken));
            var abouts = await db.MemberModes.Where(m => m.AccountId == person.Profile.AccountId && shownIds.Contains(m.ModeId))
                .ToDictionaryAsync(m => m.ModeId, m => m.About, cancellationToken);
            string? AboutIn(string modeId) => ModePrivacy.About(abouts.GetValueOrDefault(modeId), person.Profile.About);
            return Results.Ok(new PersonProfile(person.Profile.PublicId, person.Profile.DisplayName!, person.Profile.Area, AboutIn(shownIds[0]),
                shared.Where(m => shownIds.Contains(m.Id)).Select(m =>
                {
                    var may = MarketAccess.MayRequest(reach.SameMarket && reach.MarketModes.Contains(m.Id), between.Any(r => r.Status == ConnectionRequestStatus.Accepted),
                        reach.VisitingModes.Contains(m.Id), travel.ArrivedTrips.ContainsKey(m.Id));
                    return ModeView(m, chips.GetValueOrDefault((person.Profile.AccountId, m.Id), []), viewer, between, may) with
                        { ArrivalRequired = !may && travel.SearchModes.Contains(m.Id), About = AboutIn(m.Id),
                          ConnectionId = between.FirstOrDefault(r => r.ModeId == m.Id && r.Status == ConnectionRequestStatus.Accepted)?.Id };
                }).ToList(),
                Badges(person.Profile, await BadgeOrderAsync(db, cancellationToken)), shownAs.Age, shownAs.Gender, shownAs.Pronouns));
        });
    }

    /// <summary>
    /// What discovery needs from the viewer in one mode, for Explore and Traveller Search alike: the mode on (D92), a date
    /// of birth (D139) and, in Dating, a gender and a "show me" (D138). Returns their mode setting and matching facts, or
    /// the problem that refuses.
    /// </summary>
    public static async Task<(MemberMode? Own, MatchFacts? Facts, IResult? Refused)> ReadyAsync(SwoovrDbContext db, MemberProfile viewer, ConnectionMode selected,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var own = await db.MemberModes.SingleOrDefaultAsync(m => m.AccountId == viewer.AccountId && m.ModeId == selected.Id, cancellationToken);
        if (own is not { Enabled: true })
            return (null, null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.ModeNotEnabled, "Turn this mode on to see who's here."));
        // D139: no date of birth, no discovery (either way). D138: Dating needs a gender and a "show me".
        if (viewer.DateOfBirth is null)
            return (null, null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.DateOfBirthRequired, "Add your date of birth to discover people."));
        var mine = (await FactsAsync(db, [viewer.AccountId], selected.Id, DateOnly.FromDateTime(now.UtcDateTime), cancellationToken))[viewer.AccountId];
        if (selected.Id == ConnectionModeIds.Dating && !MatchPolicy.DatingReady(mine))
            return (null, null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.DatingDetailsRequired, "Add your gender and who you'd like to see to use Dating."));
        return (own, mine, null);
    }

    /// <summary>
    /// The people local to one market whom the viewer may see in one mode: the account, mode, bot, block and sanction
    /// rules (Candidates, DiscoveryEligibility), the reciprocal rules and the viewer's own filters, with Plus filters only
    /// while the entitlement service says Plus (D137–D140, D151). Explore uses the viewer's home market (D125); Traveller
    /// Search the trip's destination market (D142). Cards carry age, gender and pronouns only as each person allows.
    /// </summary>
    public static async Task<List<ProfileCard>> PeopleAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile viewer, ConnectionMode selected,
        MemberMode own, MatchFacts mine, string marketId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var people = await Candidates(db, configuration, viewer.AccountId).InMarket(db, marketId)
            .Where(c => db.MemberModes.Any(m => m.AccountId == c.Profile.AccountId
                && m.ModeId == selected.Id && m.Enabled && m.Visible)
                && (!c.Profile.Bot || db.BotModeAuthorisations.Any(b => b.AccountId == c.Profile.AccountId && b.ModeId == selected.Id)))
            .OrderByDescending(c => db.MemberModes.Where(m => m.AccountId == c.Profile.AccountId && m.ModeId == selected.Id).Select(m => m.UpdatedAt).First())
            .Take(ExploreLimit * 4)
            .ToListAsync(cancellationToken);
        var bots = await BotModesAsync(db, people.Where(c => c.Profile.Bot).Select(c => c.Profile.AccountId).ToList(), cancellationToken);
        var facts = await FactsAsync(db, people.Select(c => c.Profile.AccountId).ToList(), selected.Id, today, cancellationToken);
        var filter = await db.ModeFilters.FindAsync([viewer.AccountId, selected.Id], cancellationToken);
        var tier = await Moderation.Entitlements.TierAsync(db, viewer, now, cancellationToken);
        // Checked again in code with the same policy the database filter mirrors, so the rule has one source.
        var shown = people.Where(c => DiscoveryEligibility.ShownIn(selected.Id, selected, own,
                new MemberMode { ModeId = selected.Id, Enabled = true, Visible = true }, c.Candidate(bots))
            && MatchPolicy.Reciprocal(selected.Id, mine, facts[c.Profile.AccountId])
            && MatchPolicy.FilterAllows(filter, mine, facts[c.Profile.AccountId], tier)).ToList();
        if (filter is { Sort: ExploreSort.Compatibility } && tier >= PlanTier.Plus)
            shown = shown.OrderByDescending(c => MatchPolicy.Shared(mine, facts[c.Profile.AccountId])).ToList();
        shown = shown.Take(ExploreLimit).ToList();
        var identities = await IdentitiesAsync(db, shown.Select(c => c.Profile.AccountId).ToList(), cancellationToken);
        var chips = await IntentionLabelsAsync(db, shown.Select(c => c.Profile.AccountId).ToList(), selected.Id, cancellationToken);
        var shownIds = shown.Select(c => c.Profile.AccountId).ToList();
        // D175: the card shows the person's about for this mode.
        var abouts = await db.MemberModes.Where(m => m.ModeId == selected.Id && shownIds.Contains(m.AccountId) && m.About != null)
            .ToDictionaryAsync(m => m.AccountId, m => m.About, cancellationToken);
        var order = await BadgeOrderAsync(db, cancellationToken);
        var genders = await GenderLabelsAsync(db, cancellationToken);
        return shown.Select(c =>
        {
            var shownAs = Public(identities.GetValueOrDefault(c.Profile.AccountId), facts[c.Profile.AccountId].Age, genders);
            return new ProfileCard(c.Profile.PublicId, c.Profile.DisplayName!, c.Profile.Area, ModePrivacy.About(abouts.GetValueOrDefault(c.Profile.AccountId), c.Profile.About),
                chips.GetValueOrDefault((c.Profile.AccountId, selected.Id), []), Badges(c.Profile, order), shownAs.Age, shownAs.Gender, shownAs.Pronouns);
        }).ToList();
    }

    /// <summary>What a person allows others to see (D138, D139): age unless hidden, gender and pronouns unless hidden.</summary>
    private static (int? Age, string? Gender, string? Pronouns) Public(MemberIdentity? identity, int? age, IReadOnlyDictionary<string, string> genders)
    {
        if (identity is null) return (age, null, null);
        var gender = identity.ShowGender && identity.GenderId is { } g
            ? g == "another" && identity.GenderDescription is { } d ? d : genders.GetValueOrDefault(g) : null;
        return (identity.ShowAge ? age : null, gender, identity.ShowGender ? identity.Pronouns : null);
    }

    private static async Task<Dictionary<string, string>> GenderLabelsAsync(SwoovrDbContext db, CancellationToken cancellationToken) =>
        await db.ProfileOptions.Where(o => o.Kind == "gender").ToDictionaryAsync(o => o.Id, o => o.Label, cancellationToken);

    private static async Task<Dictionary<Guid, MemberIdentity>> IdentitiesAsync(SwoovrDbContext db, List<Guid> accounts, CancellationToken cancellationToken) =>
        await db.MemberIdentities.Where(i => accounts.Contains(i.AccountId)).ToDictionaryAsync(i => i.AccountId, cancellationToken);

    /// <summary>
    /// Matching facts (D137–D140) for each account in one mode, from application records: age from the private date of
    /// birth, the mode's chips, the chips they excluded in that mode, ID verification, gender and "show me", and the
    /// home city's coordinates (for the Plus distance filter). Never returned to clients.
    /// </summary>
    public static async Task<Dictionary<Guid, MatchFacts>> FactsAsync(SwoovrDbContext db, List<Guid> accounts, string modeId, DateOnly today, CancellationToken cancellationToken)
    {
        var profiles = await (from p in db.MemberProfiles
                              where accounts.Contains(p.AccountId)
                              join c in db.GeoCities on p.HomeGeoNameId equals (int?)c.GeoNameId into cities
                              from city in cities.DefaultIfEmpty()
                              select new { p.AccountId, p.DateOfBirth, p.IdStatus, p.Bot, Lat = (double?)city.Latitude, Lon = (double?)city.Longitude }).ToListAsync(cancellationToken);
        var chips = (await db.MemberModeIntentions.Where(i => accounts.Contains(i.AccountId) && i.ModeId == modeId).ToListAsync(cancellationToken))
            .GroupBy(i => i.AccountId).ToDictionary(g => g.Key, g => (IReadOnlySet<Guid>)g.Select(i => i.IntentionId).ToHashSet());
        var excluded = (await db.ModeFilters.Where(f => accounts.Contains(f.AccountId) && f.ModeId == modeId).ToListAsync(cancellationToken))
            .ToDictionary(f => f.AccountId, f => (IReadOnlySet<Guid>)f.ExcludedChips.ToHashSet());
        var identities = await IdentitiesAsync(db, accounts, cancellationToken);
        IReadOnlySet<Guid> none = new HashSet<Guid>();
        return profiles.ToDictionary(p => p.AccountId, p =>
        {
            var identity = identities.GetValueOrDefault(p.AccountId);
            return new MatchFacts(p.DateOfBirth is { } dob ? AgePolicy.YearsOn(dob, today) : null, chips.GetValueOrDefault(p.AccountId, none),
                excluded.GetValueOrDefault(p.AccountId, none), p.IdStatus == VerificationStatus.Verified, identity?.GenderId,
                (IReadOnlyCollection<string>?)identity?.DatingShowMe ?? [], p.Lat, p.Lon, p.Bot);
        });
    }

    /// <summary>How the viewer stands to a person under the market rules (D125): same open market, which modes the
    /// viewer's market offers, and in which shared modes the person is visiting the viewer's market.</summary>
    public sealed record Reach(bool SameMarket, IReadOnlyCollection<string> MarketModes, IReadOnlySet<string> VisitingModes);

    public static async Task<Reach> ReachAsync(SwoovrDbContext db, IConfiguration configuration, MemberProfile viewer, Row person,
        IReadOnlyList<ConnectionMode> shared, List<ConnectionRequest> between, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var (market, modes) = await MarketEndpoints.HomeAsync(db, viewer, cancellationToken);
        if (market is not { Open: true } || viewer.HomeGeoNameId is null) return new(false, [], new HashSet<string>());
        var bots = person.Profile.Bot ? await MarketEndpoints.BotMarketsAsync(db, person.Profile.AccountId, cancellationToken) : null;
        var same = MarketAccess.SameMarket(market.Id, person.Profile.HomeMarketId, person.Profile.Bot, bots);
        var visiting = new HashSet<string>();
        if (!same)
        {
            var zones = await MarketEndpoints.ZonesAsync(db, cancellationToken);
            foreach (var mode in shared.Where(m => modes.Contains(m.Id)))
                if ((await Travel.TravelEndpoints.VisitsToAsync(db, configuration, viewer.AccountId, market, zones, mode.Id, now, cancellationToken, person.Profile.AccountId)).Count > 0)
                    visiting.Add(mode.Id);
        }
        return new(same, modes, visiting);
    }

    private static SharedModeView ModeView(ConnectionMode mode, IReadOnlyList<string> chips, Guid viewer, List<ConnectionRequest> between, bool canRequest)
    {
        var request = between.Where(r => r.ModeId == mode.Id).OrderBy(r => r.Status).FirstOrDefault();
        var state = request switch
        {
            null => "none",
            { Status: ConnectionRequestStatus.Accepted } => "connected",
            _ when request.FromAccountId == viewer => "sent",
            _ => "received",
        };
        return new(mode.Id, mode.Label, chips, state, request is { Status: ConnectionRequestStatus.Pending } ? request.Id : null, canRequest);
    }

    /// <summary>The available modes in which the person is shown to the viewer (DiscoveryEligibility.ShownIn), in order.</summary>
    public static async Task<List<ConnectionMode>> SharedModesAsync(SwoovrDbContext db, Guid viewer, Row person, CancellationToken cancellationToken)
    {
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var mine = await db.MemberModes.Where(m => m.AccountId == viewer).ToDictionaryAsync(m => m.ModeId, cancellationToken);
        var theirs = await db.MemberModes.Where(m => m.AccountId == person.Profile.AccountId).ToDictionaryAsync(m => m.ModeId, cancellationToken);
        var bots = person.Profile.Bot ? await BotModesAsync(db, [person.Profile.AccountId], cancellationToken) : [];
        var eligible = modes.Where(m => DiscoveryEligibility.ShownIn(m.Id, m, mine.GetValueOrDefault(m.Id), theirs.GetValueOrDefault(m.Id), person.Candidate(bots))).ToList();
        // D138, D140: a mode where the reciprocal rules fail (an excluded chip either way, Dating "show me" either way)
        // isn't shared: neither can find nor contact the other there. Date of birth is required by Candidates (D139).
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = new List<ConnectionMode>();
        foreach (var mode in eligible)
        {
            var facts = await FactsAsync(db, [viewer, person.Profile.AccountId], mode.Id, today, cancellationToken);
            if (facts.Count == 2 && MatchPolicy.Reciprocal(mode.Id, facts[viewer], facts[person.Profile.AccountId])) result.Add(mode);
        }
        return result;
    }

    // Member-init (not a constructor) so EF can keep composing queries over it.
    public sealed class Row
    {
        public required MemberProfile Profile { get; init; }
        public AccountStatus Status { get; init; }
        public bool BetaActive { get; init; }
        public DiscoveryCandidate Candidate(Dictionary<Guid, HashSet<string>> bots) =>
            new(Status, Profile.AgeStatus, BetaActive, Profile.Bot, Profile.DisplayName, bots.GetValueOrDefault(Profile.AccountId));
    }

    /// <summary>
    /// Members-realm people other than the viewer who pass DiscoveryEligibility.Discoverable, filtered in the database,
    /// leaving out anyone the viewer blocked or who blocked the viewer (D112: a block hides both people everywhere).
    /// </summary>
    public static IQueryable<Row> Candidates(SwoovrDbContext db, IConfiguration configuration, Guid viewer)
    {
        var betaOn = configuration.GetValue("Beta:Enabled", true);
        AgeStatus[] adult = [AgeStatus.Attested, AgeStatus.SelfDeclaredAdult, AgeStatus.Verified, AgeStatus.AdultOverride];
        var now = DateTimeOffset.UtcNow;
        return from profile in db.MemberProfiles
               join account in db.Accounts on profile.AccountId equals account.Id
               where account.Realm == AccountRealm.Members && account.Status == AccountStatus.Active
                     && adult.Contains(profile.AgeStatus) && profile.DisplayName != null && (profile.DateOfBirth != null || profile.Bot) && profile.AccountId != viewer
                     && !db.Blocks.Any(b => b.BlockerAccountId == viewer && b.BlockedAccountId == profile.AccountId
                         || b.BlockerAccountId == profile.AccountId && b.BlockedAccountId == viewer)
                     // D133, D160: restricted, suspended and terminated accounts are out of discovery while the sanction lasts.
                     && !db.Sanctions.Any(s => s.AccountId == profile.AccountId && s.LiftedAt == null && s.StartsAt <= now && (s.EndsAt == null || s.EndsAt > now)
                         && s.Kind != SanctionKind.Warning)
               let beta = !betaOn || db.BetaEnrolments.Any(e => e.AccountId == account.Id && e.RevokedAt == null)
               where beta
               select new Row { Profile = profile, Status = account.Status, BetaActive = beta };
    }

    /// <summary>The configured badge order (Q11 rule 61); falls back to the D74 order when the table is empty.</summary>
    public static async Task<IReadOnlyList<Badge>> BadgeOrderAsync(SwoovrDbContext db, CancellationToken cancellationToken)
    {
        var order = await db.BadgeDefinitions.OrderBy(b => b.SortOrder).Select(b => b.Badge).ToListAsync(cancellationToken);
        return order.Count > 0 ? order : BadgePolicy.DefaultOrder;
    }

    /// <summary>Public badges only: never admin verified, evidence or roles (Q11 rule 64).</summary>
    public static IReadOnlyList<BadgeView> Badges(MemberProfile profile, IReadOnlyList<Badge> order) =>
        BadgePolicy.For(profile, order).Select(b => new BadgeView(Wire(b.Badge), b.CheckedAt)).ToList();

    public static async Task<HashSet<string>> BotModesAsync(SwoovrDbContext db, Guid bot, CancellationToken cancellationToken) =>
        (await db.BotModeAuthorisations.Where(b => b.AccountId == bot).Select(b => b.ModeId).ToListAsync(cancellationToken)).ToHashSet();

    public static async Task<Dictionary<Guid, HashSet<string>>> BotModesAsync(SwoovrDbContext db, List<Guid> bots, CancellationToken cancellationToken) =>
        bots.Count == 0 ? [] : (await db.BotModeAuthorisations.Where(b => bots.Contains(b.AccountId)).ToListAsync(cancellationToken))
            .GroupBy(b => b.AccountId).ToDictionary(g => g.Key, g => g.Select(b => b.ModeId).ToHashSet());

    public static async Task<IReadOnlyList<ConnectionModeView>> CatalogueAsync(SwoovrDbContext db, CancellationToken cancellationToken)
    {
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var intentions = await db.ModeIntentions.Where(i => i.Available).OrderBy(i => i.SortOrder).ToListAsync(cancellationToken);
        return modes.Select(m => new ConnectionModeView(m.Id, m.Label,
            intentions.Where(i => i.ModeId == m.Id).Select(i => new IntentionView(i.Id, i.Label)).ToList(), ModeSettingsPolicy.MaxIntentions)).ToList();
    }

    private static async Task<MyProfileView> OwnAsync(SwoovrDbContext db, MemberProfile profile, CancellationToken cancellationToken)
    {
        var modes = await db.ConnectionModes.Where(m => m.Available).OrderBy(m => m.SortOrder).ToListAsync(cancellationToken);
        var settings = await db.MemberModes.Where(m => m.AccountId == profile.AccountId).ToDictionaryAsync(m => m.ModeId, cancellationToken);
        // Retired chips stay stored but hidden, from the member too (D113).
        var chosen = await (from i in db.MemberModeIntentions
                            join offered in db.ModeIntentions on i.IntentionId equals offered.Id
                            where i.AccountId == profile.AccountId && offered.Available
                            orderby offered.SortOrder
                            select i).ToListAsync(cancellationToken);
        var home = profile.HomeGeoNameId is { } id ? await db.GeoCities.FindAsync([id], cancellationToken) : null;
        return new(profile.PublicId, profile.DisplayName, profile.Area, profile.About, modes.Select(m => settings.GetValueOrDefault(m.Id) is { } s
            ? new MyModeView(m.Id, s.Enabled, s.Visible, chosen.Where(i => i.ModeId == m.Id).Select(i => i.IntentionId).ToList(), s.About)
            : new MyModeView(m.Id, false, false, [])).ToList(), home is null ? null : Swoovr.Api.Travel.TravelEndpoints.Place(home),
            await db.MarketInterests.AnyAsync(i => i.AccountId == profile.AccountId, cancellationToken));
    }

    /// <summary>Labels of the available intentions each person chose, by (account, mode).</summary>
    private static async Task<Dictionary<(Guid, string), IReadOnlyList<string>>> IntentionLabelsAsync(SwoovrDbContext db, List<Guid> people, string? modeId,
        CancellationToken cancellationToken)
    {
        var rows = await (from chosen in db.MemberModeIntentions
                          join intention in db.ModeIntentions on chosen.IntentionId equals intention.Id
                          where people.Contains(chosen.AccountId) && intention.Available && (modeId == null || chosen.ModeId == modeId)
                          orderby intention.SortOrder
                          select new { chosen.AccountId, chosen.ModeId, intention.Label }).ToListAsync(cancellationToken);
        return rows.GroupBy(r => (r.AccountId, r.ModeId)).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(r => r.Label).ToList());
    }
}
