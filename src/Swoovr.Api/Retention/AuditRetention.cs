using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>Expire only a verified oldest chain prefix; never splice or rewrite surviving access events.</summary>
public static class AuditRetention
{
    public const long ChainLock=0x5357_4F4F_5652_4C47;
    public static async Task<int> RunAsync(SwoovrDbContext db,DateTimeOffset now,CancellationToken token)
    {
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        if(await db.LegalHolds.AnyAsync(token))return 0;
        var prefix=await db.Database.SqlQuery<int>($"SELECT swoovr_expire_access_prefix({now}) AS \"Value\"").SingleAsync(token);
        await db.Database.SqlQuery<int>($"SELECT swoovr_expire_operational_metadata({now}) AS \"Value\"").SingleAsync(token);
        return prefix;
    }
    public static async Task<(List<SensitiveAccess> Entries,bool Intact)> ReadAsync(SwoovrDbContext db,int take,CancellationToken token)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({ChainLock})",token);
        var entries=(await db.SensitiveAccessLog.AsNoTracking().OrderByDescending(e=>e.Sequence).Take(take).ToListAsync(token)).OrderBy(e=>e.Sequence).ToList();
        var intact=await IntactAsync(db,entries,token);
        await transaction.CommitAsync(token);return(entries,intact);
    }
    public static async Task<byte[]> TailAsync(SwoovrDbContext db,CancellationToken token)=>
        await db.Database.SqlQuery<byte[]>($"SELECT hash AS \"Value\" FROM sensitive_access_checkpoint WHERE id=1").SingleAsync(token);
    public static async Task<bool> IntactAsync(SwoovrDbContext db,IReadOnlyList<SensitiveAccess> entries,CancellationToken token)
    {
        var before=entries.Count==0?await TailAsync(db,token):
            await db.SensitiveAccessLog.Where(e=>e.Sequence<entries[0].Sequence).OrderByDescending(e=>e.Sequence).Select(e=>e.Hash).FirstOrDefaultAsync(token)??await TailAsync(db,token);
        return StaffAccessPolicy.ChainIntact(entries,before);
    }
}
