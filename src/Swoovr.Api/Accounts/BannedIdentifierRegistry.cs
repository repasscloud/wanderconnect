using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public sealed class IdentifierCheckException(int status, string code, string title) : Exception("Identifier check refused.")
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public string Title { get; } = title;
}

/// <summary>Authenticated, version-aware matching. Callers keep shared preservation authority through identifier use.</summary>
public sealed class BannedIdentifierRegistry(SwoovrDbContext db, BannedIdentifierKeys keys, Evidence.EvidencePreservation evidence)
{
    public const int CallerPerHour = 20, AddressPerHour = 1000;
    public static IdentifierCheckException Unavailable() => new(503, ProblemCodes.IdentifierCheckUnavailable, "We can't check this identifier right now. Please try again later.");
    public static IdentifierCheckException Restricted(BannedIdentifierType type) => new(403, ProblemCodes.IdentifierRestricted,
        type == BannedIdentifierType.Email
            ? "This email address cannot currently be used with Swoovr. If you believe this is incorrect, please contact Swoovr Support for assistance."
            : "This mobile number cannot currently be used with Swoovr. If you believe this is incorrect, please contact Swoovr Support for assistance.");

    public async Task RequireAllowedAsync(BannedIdentifierType type, string input, string caller, string? address, Guid? account,
        DateTimeOffset now, CancellationToken token)
    {
        if (!keys.Configured || keys.CurrentVersion is not { } current) throw Unavailable();
        if (BannedIdentifierPolicy.Normalise(type,input) is not { } value) throw new ArgumentException("Invalid identifier.", nameof(input));
        await using var barrier = await PreservationLease.SharedAsync(db, token);
        // Unknown keys for active restrictions cannot mean "no match". Expired/inactive rows do not need matching keys.
        var versions = await db.BannedIdentifiers.Where(b => b.IdentifierType == type && b.Status == BanStatus.Active
            && (b.BanExpiresAt == null || b.BanExpiresAt > now)).Select(b => b.KeyVersion).Distinct().ToListAsync(token);
        if (versions.Any(v => !keys.HasVersion(v))) throw Unavailable();
        var attempt = await RecordAttemptAsync(type,caller,address,account,current,now,token);
        var matches = new List<Guid>();
        foreach (var version in versions)
        {
            var hash = keys.Fingerprint(type,value,version);
            matches.AddRange(await db.BannedIdentifiers.Where(b => b.IdentifierType == type && b.KeyVersion == version && b.IdentifierHash == hash
                && b.Status == BanStatus.Active && (b.BanExpiresAt == null || b.BanExpiresAt > now)).Select(b => b.Id).ToListAsync(token));
        }
        if (matches.Count == 0) return;
        await db.BannedIdentifiers.Where(b => matches.Contains(b.Id)).ExecuteUpdateAsync(u => u.SetProperty(b => b.LastMatchedAt,now)
            .SetProperty(b => b.MatchCount,b => b.MatchCount + 1),token);
        await db.BanCheckAttempts.Where(a => a.Id == attempt)
            .ExecuteUpdateAsync(u => u.SetProperty(a => a.MatchedBanIds,matches.ToArray()),token);
        Audit.Record(db,now,account,"account","identifier.refused",detail:type.ToString());
        await db.SaveChangesAsync(token);
        throw Restricted(type);
    }

    public async Task<int?> SweepAttemptsAsync(DateTimeOffset now,CancellationToken token)
    {
        await using var barrier = await PreservationLease.SharedAsync(db,token);
        var held = await evidence.TryHeldAccountsAsync(token);
        if (held is null) return null;
        var live = await db.LegalHolds.Where(h => h.Status != LegalHoldStatus.Released).ToListAsync(token);
        if (live.Any(h => h.RelatedAccountIds is null || h.RetentionCaseIds is null)) return null;
        foreach (var hold in live) foreach (var account in hold.RelatedAccountIds!) held.Add(account);
        var cases = live.SelectMany(h => (h.RetentionCaseIds ?? []).Concat(h.CaseId is { } id ? [id] : [])).Distinct().ToArray();
        var protectedBans = await (from b in db.BannedIdentifiers join c in db.ModerationCases on b.CaseId equals c.Id
            where cases.Contains(c.Id) || held.Contains(c.SubjectAccountId) select b.Id).ToArrayAsync(token);
        var cutoff = now.AddDays(-1);
        return await db.BanCheckAttempts.Where(a => a.At < cutoff && (a.AccountId == null || !held.Contains(a.AccountId.Value))
            && !a.MatchedBanIds.Any(id => protectedBans.Contains(id))).ExecuteDeleteAsync(token);
    }

    private async Task<long> RecordAttemptAsync(BannedIdentifierType type,string caller,string? address,Guid? account,int current,DateTimeOffset now,CancellationToken token)
    {
        // Locks use opaque authenticated caller/address digests only in advisory-lock memory; retained quota keys use HMAC.
        var gates = new[] { "caller\0" + caller, address is null ? null : "address\0" + address }.Where(s => s is not null)
            .Select(s => new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(s!)).AsSpan(0,16))).OrderBy(AccountLifecycleLease.Key).ToArray();
        var leases = new List<PreservationLease>();
        try
        {
            foreach (var gate in gates) leases.Add(await PreservationLease.BanQuotaAsync(db,gate,token));
            var after = now.AddHours(-1);var callerCount = 0;var addressCount = 0;
            foreach (var version in keys.Versions)
            {
                var hash = keys.QuotaHash("caller",caller,version);
                callerCount += await db.BanCheckAttempts.CountAsync(a => a.CallerHash == hash && a.KeyVersion == version && a.IdentifierType == type && a.At > after,token);
                if (address is not null)
                {
                    var ip = keys.QuotaHash("address",address,version);
                    addressCount += await db.BanCheckAttempts.CountAsync(a => a.AddressHash == ip && a.KeyVersion == version && a.At > after,token);
                }
            }
            if (callerCount >= CallerPerHour || addressCount >= AddressPerHour)
                throw new IdentifierCheckException(429,ProblemCodes.TooManyRequests,"Too many identifier checks. Try again in an hour.");
            var attempt = new BanCheckAttempt { CallerHash = keys.QuotaHash("caller",caller,current),
                AddressHash = address is null ? null : keys.QuotaHash("address",address,current),KeyVersion = current,
                IdentifierType = type,AccountId = account,At = now };
            db.BanCheckAttempts.Add(attempt);
            await db.SaveChangesAsync(token);
            return attempt.Id;
        }
        finally { foreach (var lease in leases.AsEnumerable().Reverse()) await lease.DisposeAsync(); }
    }
}

/// <summary>Neutral problem details even when resolution occurs in member-write middleware or the session endpoint.</summary>
public sealed class IdentifierCheckMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (IdentifierCheckException failure) when (!context.Response.HasStarted)
        {
            context.Response.Headers["Cache-Control"] = "no-store";
            if (failure.Status == 429) context.Response.Headers["Retry-After"] = "3600";
            await AccountEndpoints.Problem(failure.Status,failure.Code,failure.Title).ExecuteAsync(context);
        }
    }
}
