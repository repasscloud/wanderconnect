using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public sealed record RegistryDisposalRun(int HistoriesDisposed,int ReceiptsExpired,bool FailedClosed=false);

/// <summary>Lawful inactive-history erasure with nonmatchable continuity; database guards independently constrain DELETE.</summary>
public sealed class RegistryDisposal(SwoovrDbContext db,EvidencePreservation evidence)
{
    public async Task<RegistryDisposalRun> RunAsync(DateTimeOffset now,CancellationToken token)
    {
        var disposed=0;var expired=0;Guid? after=null;
        if(await evidence.TryHeldAccountsAsync(token) is null)return new(0,0,true);
        while(true)
        {
            var query=db.BannedIdentifiers.Where(b=>b.Status!=BanStatus.Active);
            if(after is{}cursor)query=query.Where(b=>b.Id.CompareTo(cursor)>0);
            var page=await query.OrderBy(b=>b.Id).Select(b=>b.Id).Take(200).ToListAsync(token);
            if(page.Count==0)break;
            foreach(var id in page)
            {
                await using var authority=await PreservationLease.ExclusiveAsync(db,token);
                if(await evidence.TryHeldAccountsAsync(token) is null)return new(disposed,expired,true);
                var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);
                if(row is null)continue;await db.Entry(row).ReloadAsync(token);
                if(!BannedIdentifierPolicy.MayDisposeHistory(row,now,false,now))continue;
                // The same database predicate guards both this choice and every actual DELETE, including direct SQL.
                if(!await db.Database.SqlQuery<bool>($"SELECT ban_history_disposal_safe({id}) AS \"Value\"").SingleAsync(token))continue;
                var eventAt=await db.BanTransitions.Where(t=>t.BannedIdentifierId==id&&t.Action!="expired").Select(t=>(DateTimeOffset?)t.At).MaxAsync(token);
                var noteAt=await (from n in db.BanCaseNotes join t in db.BanTransitions on n.TransitionId equals t.Id
                    where t.BannedIdentifierId==id&&t.Action!="expired" select (DateTimeOffset?)n.At).MaxAsync(token);
                var latestHistory=new[]{eventAt,noteAt}.Max();
                await using var transaction=await db.Database.BeginTransactionAsync(token);
                var receipt=BannedIdentifierPolicy.Disposition(row,now,false,now,latestHistory);db.BanDispositions.Add(receipt);await db.SaveChangesAsync(token);
                var transitions=db.BanTransitions.Where(t=>t.BannedIdentifierId==id).Select(t=>t.Id);
                await db.BanCaseNotes.Where(n=>transitions.Contains(n.TransitionId)).ExecuteDeleteAsync(token);
                await db.BanTransitions.Where(t=>t.BannedIdentifierId==id).ExecuteDeleteAsync(token);
                await db.BannedIdentifiers.Where(b=>b.Id==id).ExecuteDeleteAsync(token);
                await transaction.CommitAsync(token);disposed++;
                db.Entry(row).State=EntityState.Detached;db.Entry(receipt).State=EntityState.Detached;
            }
            after=page[^1];
        }
        after=null;
        while(true)
        {
            var query=db.BanDispositions.AsQueryable();if(after is{}cursor)query=query.Where(d=>d.Id.CompareTo(cursor)>0);
            var page=await query.OrderBy(d=>d.Id).Select(d=>new{d.Id,d.CaseId}).Take(200).ToListAsync(token);
            if(page.Count==0)break;
            foreach(var row in page)
            {
                await using var authority=await PreservationLease.ExclusiveAsync(db,token);
                if(await evidence.TryHeldAccountsAsync(token) is null)return new(disposed,expired,true);
                if(await db.Sanctions.AnyAsync(s=>s.CaseId==row.CaseId&&s.Kind==SanctionKind.Termination&&s.LiftedAt==null,token))continue;
                if(!await db.Database.SqlQuery<bool>($"SELECT ban_case_disposal_safe({row.CaseId}) AS \"Value\"").SingleAsync(token))continue;
                if(await db.BanDispositionReviews.AnyAsync(r=>r.DispositionId==row.Id&&r.At.AddMonths(BannedIdentifierPolicy.HistoryMonths)>now,token))continue;
                await using var transaction=await db.Database.BeginTransactionAsync(token);
                await db.BanDispositionReviews.Where(r=>r.DispositionId==row.Id).ExecuteDeleteAsync(token);
                expired+=await db.BanDispositions.Where(d=>d.Id==row.Id).ExecuteDeleteAsync(token);
                await transaction.CommitAsync(token);
            }
            after=page[^1].Id;
        }
        return new(disposed,expired);
    }
}
