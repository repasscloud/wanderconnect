using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Analytics;
public sealed class BetaJourneyRetention(SwoovrDbContext db,EvidencePreservation evidence,TimeProvider clock)
{
    public async Task<bool> PurgeAsync(CancellationToken token)
    {
        var cutoff=DateOnly.FromDateTime(clock.GetUtcNow().AddDays(-89).UtcDateTime);
        await using var lease=await PreservationLease.ExclusiveAsync(db,token);
        if(!await db.Set<BetaJourneyDay>().AnyAsync(r=>r.Day<cutoff,token))return true;
        var held=await evidence.TryHeldAccountsAsync(token);if(held is null)return false;
        if(await db.LegalHolds.AnyAsync(h=>h.RelatedAccountIds==null||h.RetentionCaseIds==null,token))return false;
        foreach(var related in await db.LegalHolds.Where(h=>h.Status!=LegalHoldStatus.Released).Select(h=>h.RelatedAccountIds!).ToListAsync(token))held.UnionWith(related);
        var retained=await db.EvidenceItems.Where(e=>e.Kind=="beta-journey"&&e.AccountId!=null).Select(e=>e.AccountId!.Value).Distinct().ToListAsync(token);held.UnionWith(retained);
        await db.Set<BetaJourneyDay>().Where(r=>r.Day<cutoff&&!held.Contains(r.AccountId)).ExecuteDeleteAsync(token);
        return true;
    }
}
