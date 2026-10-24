using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;
using static Swoovr.Api.Accounts.AdminEndpoints;

namespace Swoovr.Api.Profiles;

/// <summary>
/// Markets (D125, D146): the shared lookups Explore, profiles, requests and Travel use, the member's "tell me when it
/// opens" switch, and admin routes to configure markets and assign bots without a release. A member's home market is
/// worked out here from their home city and never returned to clients. The audit log holds ids and market ids only,
/// never a city.
/// </summary>
public static partial class MarketEndpoints
{
    public const int MaxAreas = 20;

    [GeneratedRegex("^[a-z][a-z0-9-]{1,31}$")]
    private static partial Regex MarketId();

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api/v1").RequireAuthorization();

        // Rule 37: members outside every open market can ask to hear when theirs launches.
        api.MapPut("/me/market-interest", async (MarketInterestUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (profile, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            if (profile.HomeGeoNameId is not { } home)
                return Problem(StatusCodes.Status403Forbidden, ProblemCodes.HomeCityRequired, "Add your home city to discover people in your community.");
            if (profile.HomeMarketId is { } id && await db.Markets.FindAsync([id], cancellationToken) is { Open: true })
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.InvalidRequest, "Swoovr is already available where you live.");
            var existing = await db.MarketInterests.FindAsync([profile.AccountId], cancellationToken);
            if (update.Interested && existing is null)
            {
                db.MarketInterests.Add(new MarketInterest { AccountId = profile.AccountId, GeoNameId = home, CreatedAt = now });
                Audit.Record(db, now, profile.AccountId, "account", "market-interest.added", "account", profile.AccountId.ToString());
            }
            else if (!update.Interested && existing is not null)
            {
                db.MarketInterests.Remove(existing);
                Audit.Record(db, now, profile.AccountId, "account", "market-interest.removed", "account", profile.AccountId.ToString());
            }
            await db.SaveChangesAsync(cancellationToken);
            return Results.NoContent();
        });

        var admin = app.MapGroup("/api/v1/admin").RequireAuthorization();

        admin.MapGet("/markets", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var markets = await db.Markets.OrderBy(m => m.Id).ToListAsync(cancellationToken);
            var views = new List<MarketAdminView>();
            foreach (var market in markets) views.Add(await AdminViewAsync(db, market, cancellationToken));
            return Results.Ok(views);
        });

        // Rules 1–6 and 48: create or replace a market's name, status, areas and modes, then recompute every home market.
        admin.MapPut("/markets/{id}", async (string id, MarketUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            var name = update.Name?.Trim();
            var status = Enum.GetValues<MarketStatus>().Where(s => Wire(s) == update.Status).Cast<MarketStatus?>().SingleOrDefault();
            var areas = update.Areas ?? [];
            var modes = (update.Modes ?? []).Distinct().ToList();
            var known = await db.ConnectionModes.Select(m => m.Id).ToListAsync(cancellationToken);
            var cities = areas.Select(a => a.CentreCityId).Distinct().ToList();
            if (!MarketId().IsMatch(id) || string.IsNullOrEmpty(name) || name.Length > 80 || name.Any(char.IsControl) || status is null
                || areas.Count is < 1 or > MaxAreas || cities.Count != areas.Count || areas.Any(a => !(a.RadiusKm > 0 && a.RadiusKm <= 1000))
                || modes.Any(m => !known.Contains(m))
                || await db.GeoCities.CountAsync(c => cities.Contains(c.GeoNameId), cancellationToken) != cities.Count)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidRequest,
                    $"A market needs a lowercase id, a name, a status (planned, beta, active, suspended), 1 to {MaxAreas} areas around different known cities with a radius up to 1000 km, and known modes.");
            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            var market = await db.Markets.FindAsync([id], cancellationToken);
            if (market is null) db.Markets.Add(market = new Market { Id = id });
            (market.Name, market.Status) = (name, status.Value);
            db.MarketAreas.RemoveRange(await db.MarketAreas.Where(a => a.MarketId == id).ToListAsync(cancellationToken));
            db.MarketModes.RemoveRange(await db.MarketModes.Where(m => m.MarketId == id).ToListAsync(cancellationToken));
            await db.SaveChangesAsync(cancellationToken);
            db.MarketAreas.AddRange(areas.Select(a => new MarketArea { MarketId = id, CentreGeoNameId = a.CentreCityId, RadiusKm = a.RadiusKm }));
            db.MarketModes.AddRange(modes.Select(m => new MarketMode { MarketId = id, ModeId = m }));
            Audit.Record(db, now, current!.Account!.Id, "account", "market.updated", "market", id, $"{Wire(status.Value)}:areas:{areas.Count}:modes:{string.Join(",", modes.Order())}");
            await db.SaveChangesAsync(cancellationToken);
            var moved = await RecomputeHomesAsync(db, cancellationToken);
            Audit.Record(db, now, current.Account.Id, "account", "market.homes-recomputed", "market", id, moved.ToString());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Results.Ok(await AdminViewAsync(db, market, cancellationToken));
        });

        admin.MapGet("/accounts/{id:guid}/bot-markets", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (_, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is not { Bot: true })
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotABot, "That account isn't an official bot.");
            return Results.Ok(new BotMarketsView((await BotMarketsAsync(db, id, cancellationToken)).Order(StringComparer.Ordinal).ToList()));
        });

        // Rules 38–41: a bot appears only in the markets an admin assigned it to, `*` meaning every open market.
        admin.MapPut("/accounts/{id:guid}/bot-markets", async (Guid id, BotMarketsRequest request, ClaimsPrincipal user, AccountResolver accounts,
            TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await StaffAsync(user, accounts, AccountRole.Admin, cancellationToken);
            if (denied is not null) return denied;
            var db = accounts.Db!;
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is not { Bot: true })
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.NotABot, "That account isn't an official bot.");
            var wanted = (request.Markets ?? []).Distinct().ToList();
            var known = await db.Markets.Select(m => m.Id).ToListAsync(cancellationToken);
            if (wanted.Count > known.Count + 1 || wanted.Any(m => m != BotMarket.Everywhere && !known.Contains(m)))
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.UnknownMarket, "Assign only existing markets, or * for every open market.");
            var now = clock.GetUtcNow();
            var existing = await db.BotMarkets.Where(b => b.AccountId == id).ToListAsync(cancellationToken);
            db.BotMarkets.RemoveRange(existing.Where(b => !wanted.Contains(b.MarketId)));
            db.BotMarkets.AddRange(wanted.Where(m => existing.All(b => b.MarketId != m))
                .Select(m => new BotMarket { AccountId = id, MarketId = m, AuthorisedAt = now, AuthorisedBy = current!.Account!.Id }));
            var sorted = wanted.Order(StringComparer.Ordinal).ToList();
            Audit.Record(db, now, current!.Account!.Id, "account", "bot.markets-assigned", "account", id.ToString(), string.Join(",", sorted));
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new BotMarketsView(sorted));
        });
    }

    /// <summary>Every market area with its market and centre city.</summary>
    public static async Task<List<MarketZone>> ZonesAsync(SwoovrDbContext db, CancellationToken cancellationToken) =>
        (await (from a in db.MarketAreas join m in db.Markets on a.MarketId equals m.Id join c in db.GeoCities on a.CentreGeoNameId equals c.GeoNameId
                select new { m, a, c }).ToListAsync(cancellationToken)).Select(x => new MarketZone(x.m, x.a, x.c)).ToList();

    /// <summary>The member's home market and the modes it offers (empty when there is none).</summary>
    public static async Task<(Market? Market, List<string> Modes)> HomeAsync(SwoovrDbContext db, MemberProfile member, CancellationToken cancellationToken)
    {
        var market = member.HomeMarketId is { } id ? await db.Markets.FindAsync([id], cancellationToken) : null;
        var modes = market is null ? [] : await db.MarketModes.Where(m => m.MarketId == market.Id).Select(m => m.ModeId).ToListAsync(cancellationToken);
        return (market, modes);
    }

    /// <summary>The member's open home market for local discovery in one mode, or the problem that refuses it (rules 30–33).</summary>
    public static async Task<(Market? Market, IResult? Refused)> LocalAsync(SwoovrDbContext db, MemberProfile member, string modeId, CancellationToken cancellationToken)
    {
        var (market, modes) = await HomeAsync(db, member, cancellationToken);
        return MarketAccess.Local(member.HomeGeoNameId is not null, market, modes, modeId) switch
        {
            LocalAccess.Open => (market, null),
            LocalAccess.HomeCityRequired => (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.HomeCityRequired, "Add your home city to discover people in your community.")),
            _ => (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.MarketNotOpen, "Swoovr isn't available in your area yet. We're expanding to new cities.")),
        };
    }

    /// <summary>People local to one market in the database (MarketAccess.SameMarket): by home market, or bots by assignment.</summary>
    public static IQueryable<ProfileEndpoints.Row> InMarket(this IQueryable<ProfileEndpoints.Row> rows, SwoovrDbContext db, string marketId) =>
        rows.Where(c => c.Profile.Bot
            ? db.BotMarkets.Any(b => b.AccountId == c.Profile.AccountId && (b.MarketId == marketId || b.MarketId == BotMarket.Everywhere))
            : c.Profile.HomeMarketId == marketId);

    public static async Task<HashSet<string>> BotMarketsAsync(SwoovrDbContext db, Guid bot, CancellationToken cancellationToken) =>
        (await db.BotMarkets.Where(b => b.AccountId == bot).Select(b => b.MarketId).ToListAsync(cancellationToken)).ToHashSet();

    /// <summary>Recomputes every member's home market after a market changes (D146); returns how many changed. Not a move.</summary>
    public static async Task<int> RecomputeHomesAsync(SwoovrDbContext db, CancellationToken cancellationToken)
    {
        var zones = await ZonesAsync(db, cancellationToken);
        var members = await (from p in db.MemberProfiles join c in db.GeoCities on p.HomeGeoNameId equals c.GeoNameId select new { p, c }).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var x in members)
            if (PlaceNames.MarketOf(x.c, zones)?.Id is var id && id != x.p.HomeMarketId) { x.p.HomeMarketId = id; changed++; }
        await db.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private static async Task<MarketAdminView> AdminViewAsync(SwoovrDbContext db, Market market, CancellationToken cancellationToken) => new(
        market.Id, market.Name, Wire(market.Status),
        (await db.MarketAreas.Where(a => a.MarketId == market.Id).OrderBy(a => a.CentreGeoNameId).ToListAsync(cancellationToken)).Select(a => new MarketAreaView(a.CentreGeoNameId, a.RadiusKm)).ToList(),
        await db.MarketModes.Where(m => m.MarketId == market.Id).OrderBy(m => m.ModeId).Select(m => m.ModeId).ToListAsync(cancellationToken),
        await db.MemberProfiles.CountAsync(p => p.HomeMarketId == market.Id, cancellationToken));
}
