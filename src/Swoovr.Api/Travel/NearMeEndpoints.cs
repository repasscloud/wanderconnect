using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Profiles;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Travel;

/// <summary>
/// D348, #278: keys for Near me cells, `Location:Keys:{version}` (base64, 32 bytes) held outside the database like the other
/// versioned keys (DATA-KEYS.md). Without an explicit key, version 1 is derived from the founder secret with its own
/// label, as contact history does. Values are sealed with AES-GCM: a version byte, a 12-byte nonce, a 16-byte tag and the
/// ciphertext, so `keys rotate location` can re-seal them.
/// </summary>
public sealed class LocationKeys(IConfiguration configuration, Founders founders)
{
    public IReadOnlyDictionary<byte, byte[]> Encryption { get; } = Read(configuration, founders);
    public byte? CurrentVersion => Encryption.Count == 0 ? null : Encryption.Keys.Max();

    private static Dictionary<byte, byte[]> Read(IConfiguration configuration, Founders founders)
    {
        var keys = new Dictionary<byte, byte[]>();
        foreach (var entry in configuration.GetSection("Location:Keys").GetChildren())
            if (byte.TryParse(entry.Key, out var version) && entry.Value is { Length: > 0 } value && Convert.FromBase64String(value) is { Length: 32 } key)
                keys[version] = key;
        if (!keys.ContainsKey(1) && founders.DeriveLegacyKey("swoovr:location:encryption:v1") is { } legacy) keys[1] = legacy;
        return keys;
    }

    /// <summary>The sealed cell, or null when no key is configured (then nothing is stored).</summary>
    public byte[]? Seal((double Latitude, double Longitude) cell) => CurrentVersion is { } version
        ? ContactHistoryPolicy.Encrypt(version, Encryption[version], string.Create(CultureInfo.InvariantCulture, $"{cell.Latitude:R},{cell.Longitude:R}"))
        : null;

    /// <summary>The cell, or null when the value is malformed or sealed under a version that isn't configured.</summary>
    public (double Latitude, double Longitude)? Open(byte[] sealedCell)
    {
        if (sealedCell.Length < 30 || !Encryption.TryGetValue(sealedCell[0], out var key)) return null;
        try
        {
            var plain = new byte[sealedCell.Length - 29];
            using (var aes = new AesGcm(key, 16)) aes.Decrypt(sealedCell.AsSpan(1, 12), sealedCell.AsSpan(29), sealedCell.AsSpan(13, 16), plain, [sealedCell[0]]);
            var parts = Encoding.UTF8.GetString(plain).Split(',');
            return parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
                && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon) ? (lat, lon) : null;
        }
        catch (CryptographicException) { return null; }
    }
}

/// <summary>
/// #278 → #281: told about each accepted Near me reading (unsnapped, never stored by this call) so the scheduled Home City
/// check can derive the day's outcome without keeping the reading. No implementations yet.
/// </summary>
public interface IAcceptedReadingObserver
{
    Task ObserveAsync(SwoovrDbContext db, MemberProfile member, LocationReading reading, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>D348, #278: Near me's stored reading. Discovery from it is #279.</summary>
public static class NearMeEndpoints
{
    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();

        // One reading while Near me is open (never in the background). Stored snapped and sealed; every refusal reason
        // reads the same, so the answer reveals nothing about the previous reading.
        api.MapPut("/me/location", async (LocationCheck? check, ClaimsPrincipal user, AccountResolver accounts, LocationKeys keys,
            IEnumerable<IAcceptedReadingObserver> observers, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (me, db, now) = (current!.Profile!, accounts.Db!, clock.GetUtcNow());
            var reading = check is null ? null : new LocationReading(check.Latitude, check.Longitude, check.AccuracyMetres, check.TakenAt);
            var previous = await db.MemberLocations.FindAsync([me.AccountId], cancellationToken);
            switch (NearMePolicy.Accept(reading, previous, previous is null ? null : keys.Open(previous.SealedCell), now))
            {
                case NearMeRefusal.TooSoon: return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyAttempts, "Your location can update every five minutes.");
                case NearMeRefusal.Unusable: return Problem(StatusCodes.Status400BadRequest, ProblemCodes.LocationUnusable, "We couldn't use your location. Near me needs a recent reading accurate to within 2 km.");
            }
            if (keys.Seal(NearMePolicy.Snap(reading!.Latitude, reading.Longitude)) is not { } sealedCell)
                return Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Near me isn't available right now.");
            var zones = await MarketEndpoints.ZonesAsync(db, cancellationToken);
            var market = zones.Where(z => z.Market.Open)
                .Select(z => (z.Market, Km: PlaceNames.DistanceKm(reading.Latitude, reading.Longitude, z.Centre.Latitude, z.Centre.Longitude), z.Area.RadiusKm))
                .Where(x => x.Km <= x.RadiusKm).OrderBy(x => x.Km).ThenBy(x => x.Market.Id, StringComparer.Ordinal).Select(x => x.Market).FirstOrDefault();
            var row = previous ?? db.MemberLocations.Add(new MemberLocation { AccountId = me.AccountId }).Entity;
            (row.MarketId, row.SealedCell, row.Accuracy, row.ObservedAt, row.AcceptedAt) =
                (market?.Id, sealedCell, NearMePolicy.Class(reading.AccuracyMetres), reading.TakenAt > now ? now : reading.TakenAt, now);
            await db.SaveChangesAsync(cancellationToken);
            foreach (var observer in observers) await observer.ObserveAsync(db, me, reading, now, cancellationToken);
            return Results.Ok(new NearMeLocationView(market?.Name, NearMePolicy.UsedUntil(row)));
        });

        // Stop sharing: the stored reading goes at once.
        api.MapDelete("/me/location", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            await ForgetAsync(accounts.Db!, current!.Profile!.AccountId, cancellationToken);
            return Results.NoContent();
        });
    }

    /// <summary>A stored reading that may be used now, with its cell opened.</summary>
    public sealed record Present(MemberLocation Location, (double Latitude, double Longitude) Cell);

    /// <summary>
    /// D346, #279: the given members' usable readings (NearMePolicy.Fresh: under two hours old, taken after the latest sign
    /// out everywhere), cells opened. A reading whose cell can't be opened is left out.
    /// </summary>
    public static async Task<Dictionary<Guid, Present>> PresenceAsync(SwoovrDbContext db, LocationKeys keys, IReadOnlyCollection<Guid> accounts,
        DateTimeOffset now, CancellationToken cancellationToken, string? onlyMarket = null)
    {
        var cutoff = now - NearMePolicy.FreshFor;
        var rows = await (from l in db.MemberLocations join a in db.Accounts on l.AccountId equals a.Id
                          where accounts.Contains(l.AccountId) && l.ObservedAt > cutoff && (onlyMarket == null || l.MarketId == onlyMarket)
                          select new { l, a.SessionsRevokedAt }).ToListAsync(cancellationToken);
        return rows.Where(x => NearMePolicy.Fresh(x.l, x.SessionsRevokedAt, now) && keys.Open(x.l.SealedCell) is not null)
            .ToDictionary(x => x.l.AccountId, x => new Present(x.l, keys.Open(x.l.SealedCell)!.Value));
    }

    /// <summary>
    /// D346, #279: members present in one market by a fresh reading who chose to appear in nearby searches (checked now, not
    /// when the reading was stored), other than the viewer. Account, mode, block and sanction rules are the caller's.
    /// </summary>
    public static async Task<Dictionary<Guid, Present>> AppearingInAsync(SwoovrDbContext db, LocationKeys keys, string marketId, Guid viewer,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - NearMePolicy.FreshFor;
        var ids = await (from l in db.MemberLocations join p in db.MemberProfiles on l.AccountId equals p.AccountId
                         where l.MarketId == marketId && l.ObservedAt > cutoff && p.AppearInNearby && l.AccountId != viewer
                         select l.AccountId).ToListAsync(cancellationToken);
        return await PresenceAsync(db, keys, ids, now, cancellationToken, marketId);
    }

    /// <summary>D346: the open market each given member is present in by a fresh reading (no keys needed: the market is plaintext).</summary>
    public static async Task<Dictionary<Guid, string>> PresentMarketsAsync(SwoovrDbContext db, IReadOnlyCollection<Guid> accounts, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var cutoff = now - NearMePolicy.FreshFor;
        var rows = await (from l in db.MemberLocations join a in db.Accounts on l.AccountId equals a.Id join m in db.Markets on l.MarketId equals m.Id
                          where accounts.Contains(l.AccountId) && l.ObservedAt > cutoff
                          select new { l, a.SessionsRevokedAt, m }).ToListAsync(cancellationToken);
        return rows.Where(x => x.m.Open && NearMePolicy.Fresh(x.l, x.SessionsRevokedAt, now)).ToDictionary(x => x.l.AccountId, x => x.m.Id);
    }

    /// <summary>Deletes a member's stored Near me reading now (Appear off, stop sharing, a sanction, a deletion request).</summary>
    public static Task<int> ForgetAsync(SwoovrDbContext db, Guid accountId, CancellationToken cancellationToken) =>
        db.MemberLocations.Where(l => l.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);

    /// <summary>
    /// Deletes readings that may no longer be used: two hours after they were taken, or taken before the member's latest
    /// "sign out everywhere". Readers check the same rule, so a row waiting for this purge is never used.
    /// </summary>
    public static Task<int> PurgeAsync(SwoovrDbContext db, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - NearMePolicy.FreshFor;
        return db.MemberLocations.Where(l => l.ObservedAt <= cutoff
                || db.Accounts.Any(a => a.Id == l.AccountId && a.SessionsRevokedAt != null && a.SessionsRevokedAt >= l.ObservedAt))
            .ExecuteDeleteAsync(cancellationToken);
    }
}

/// <summary>D348, #270: the Near me purge, every five minutes (the retention worker runs hourly). Logs counts only.</summary>
public sealed class NearMePurgeWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<NearMePurgeWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), clock, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var removed = await NearMeEndpoints.PurgeAsync(scope.ServiceProvider.GetRequiredService<SwoovrDbContext>(), clock.GetUtcNow(), stoppingToken);
                if (removed > 0) log.LogInformation("Near me purge: {Removed} expired readings deleted.", removed);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { log.LogError("Near me purge failed ({ErrorType}); it retries in five minutes.", error.GetType().Name); }
            await Task.Delay(NearMePolicy.PurgeEvery, clock, stoppingToken);
        }
    }
}
