using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Moderation;
/// <summary>Completed inspection receipts contain no source text. Thirty-day DEFAULT, independent held copies survive.</summary>
public sealed class TextHarmRetention(SwoovrDbContext db,EvidencePreservation evidence)
{
    public async Task<int?> PurgeAsync(DateTimeOffset now,CancellationToken token)
    {
        var cutoff=now.AddDays(-30);var removed=0;
        var ids=await db.Set<TextHarmIntent>().AsNoTracking().Where(i=>i.ProcessedAt!=null&&i.ProcessedAt<cutoff).Select(i=>i.Id).ToListAsync(token);
        foreach(var id in ids)
        {
            var owner=await db.Set<TextHarmIntent>().Where(i=>i.Id==id).Select(i=>(Guid?)i.AccountId).SingleOrDefaultAsync(token);
            if(owner is null)continue;
            await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,owner.Value,token);
            await using var barrier=await PreservationLease.SharedAsync(db,token);
            var held=await evidence.TryHeldAccountsAsync(token);if(held is null)return null;
            if(held.Contains(owner.Value)||await db.EvidenceItems.AnyAsync(e=>e.Kind=="text-harm-intent"&&e.RecordId==id.ToString(),token))continue;
            removed+=await db.Set<TextHarmIntent>().Where(i=>i.Id==id&&i.ProcessedAt<cutoff&&i.SealedText==null).ExecuteDeleteAsync(token);
        }
        return removed;
    }
}
