using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Notifications;
/// <summary>DEFAULT60day grouped expiry. Retain only a non-member version disposition against accidental reuse.</summary>
public sealed class PolicyNoticeRetention(SwoovrDbContext db,EvidencePreservation evidence,TimeProvider clock)
{
    public async Task<int?> PurgeAsync(CancellationToken token)
    {
        var now=clock.GetUtcNow();
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        var due=await db.Set<PolicyNoticePublication>().AsNoTracking().Where(p=>p.DisposedAt==null&&(p.PurgeAfter??p.StagedAt.AddDays(60))<=now).Select(p=>p.Id).ToListAsync(token);
        if(due.Count==0)return 0;
        var held=await evidence.TryHeldAccountsAsync(token);if(held is null)return null;
        if(await db.LegalHolds.AnyAsync(h=>h.RelatedAccountIds==null||h.RetentionCaseIds==null,token))return null;
        foreach(var related in await db.LegalHolds.Where(h=>h.Status!=LegalHoldStatus.Released).Select(h=>h.RelatedAccountIds!).ToListAsync(token))held.UnionWith(related);
        var disposed=0;
        foreach(var id in due)
        {
            var members=await db.Set<PolicyNoticeRecipient>().Where(r=>r.PublicationId==id).Select(r=>r.AccountId).ToListAsync(token);
            var prefix=id+"/";
            if(members.Any(held.Contains)||await db.EvidenceItems.AnyAsync(e=>e.Kind=="policy-notice"&&e.RecordId.StartsWith(prefix),token))continue;
            await using var transaction=await db.Database.BeginTransactionAsync(token);
            await db.Notifications.Where(n=>n.Kind==NotificationKind.PolicyChange&&n.SubjectId==id).ExecuteDeleteAsync(token);
            await db.Set<PolicyNoticeRecipient>().Where(r=>r.PublicationId==id).ExecuteDeleteAsync(token);
            await db.Set<PolicyNoticePublication>().Where(p=>p.Id==id&&p.DisposedAt==null).ExecuteUpdateAsync(set=>set
                .SetProperty(p=>p.DisposedAt,now).SetProperty(p=>p.StagedBy,(Guid?)null).SetProperty(p=>p.PublishedBy,(Guid?)null)
                .SetProperty(p=>p.Title,"Expired safety update").SetProperty(p=>p.Link,"/safety"),token);
            await transaction.CommitAsync(token);disposed++;
        }
        return disposed;
    }
}
