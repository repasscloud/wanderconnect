using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Accounts;

/// <summary>Inactive source expiry is separate from invitation effects and independent preserved evidence.</summary>
public sealed class BetaEnrolmentRetention(SwoovrDbContext db,EvidencePreservation evidence,TimeProvider clock)
{
    public async Task<bool> PurgeAsync(CancellationToken token)
    {
        var cutoff=clock.GetUtcNow().AddDays(-90);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        if(!await db.BetaEnrolments.AnyAsync(e=>e.RevokedAt<=cutoff||e.RevokedAt==null&&e.AccountId==null&&e.ActivatedAt==null&&e.CreatedAt<=cutoff,token))return true;
        var held=await evidence.TryHeldAccountsAsync(token);if(held is null)return false;
        if(await db.LegalHolds.AnyAsync(h=>h.RelatedAccountIds==null||h.RetentionCaseIds==null,token))return false;
        foreach(var related in await db.LegalHolds.Where(h=>h.Status!=LegalHoldStatus.Released).Select(h=>h.RelatedAccountIds!).ToListAsync(token))held.UnionWith(related);
        var copies=await db.EvidenceItems.Where(e=>e.AccountId!=null).Select(e=>e.AccountId!.Value).Distinct().ToListAsync(token);held.UnionWith(copies);
        // An unlinked address cannot safely establish historical account identity. Conservative hold/copy deferral.
        var deferUnlinked=held.Count!=0||await db.EvidenceItems.AnyAsync(token);
        await db.BetaEnrolments.Where(e=>(e.RevokedAt<=cutoff||e.RevokedAt==null&&e.AccountId==null&&e.ActivatedAt==null&&e.CreatedAt<=cutoff)
            &&(e.AccountId!=null&&!held.Contains(e.AccountId.Value)||e.AccountId==null&&!deferUnlinked)).ExecuteDeleteAsync(token);
        return true;
    }
}
