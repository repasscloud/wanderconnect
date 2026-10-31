using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

public sealed record OperationalRetentionRun(int HoldsDisposed,int ReviewRequired,bool FailedClosed=false);

/// <summary>Expire a released operational group atomically, preserving the entire custody chain until its lawful end.</summary>
public sealed class OperationalRetention(SwoovrDbContext db,EvidencePreservation evidence,SafetyRetention safety)
{
    public async Task<OperationalRetentionRun> RunAsync(DateTimeOffset now,CancellationToken token)
    {
        var disposed=0;var review=0;Guid? after=null;
        if(await evidence.TryHeldAccountsAsync(token) is null)return new(0,0,true);
        while(true)
        {
            var query=db.LegalHolds.Where(h=>h.Status==LegalHoldStatus.Released&&h.ReleasedAt!=null);
            if(after is{}cursor)query=query.Where(h=>h.Id.CompareTo(cursor)>0);
            var ids=await query.OrderBy(h=>h.Id).Select(h=>h.Id).Take(200).ToListAsync(token);if(ids.Count==0)break;
            foreach(var id in ids)
            {
                await using var authority=await PreservationLease.ExclusiveAsync(db,token);
                await using var mutex=await PreservationLease.HoldAsync(db,id,token);
                var held=await evidence.TryHeldAccountsAsync(token);if(held is null)return new(disposed,review,true);
                var hold=await db.LegalHolds.SingleOrDefaultAsync(h=>h.Id==id,token);if(hold is null)continue;await db.Entry(hold).ReloadAsync(token);
                if(hold.Status!=LegalHoldStatus.Released||hold.ReleasedAt is null)continue;
                if(hold.RelatedAccountIds is null||hold.RetentionCaseIds is null||hold.ReleasedBy is null||hold.ReleasedBy==Guid.Empty
                    ||hold.PlacedAt==DateTimeOffset.MinValue||hold.ReleasedAt<hold.PlacedAt
                    ||hold.NoReviewRetentionConfirmedAt is not null&&(hold.NoReviewRetentionConfirmedBy is null||hold.NoReviewRetentionConfirmedBy==Guid.Empty))
                {review++;continue;}
                var accounts=await SafetyRetention.HoldAccountsAsync(db,hold,token);
                if(accounts.Any(held.Contains)||await db.EvidenceItems.AnyAsync(e=>e.HoldId==id,token))continue;
                var obligations=db.LegalHolds.Where(h=>h.Id!=id&&(h.Status!=LegalHoldStatus.Released||db.EvidenceItems.Any(e=>e.HoldId==h.Id)));
                if(await obligations.AnyAsync(h=>h.RelatedAccountIds==null,token)){review++;continue;}
                var covered=false;
                foreach(var account in accounts)
                    if(await obligations.AnyAsync(h=>h.RelatedAccountIds!=null&&h.RelatedAccountIds.Contains(account),token)){covered=true;break;}
                if(covered)continue;
                if(await db.MediaItems.AnyAsync(m=>m.HoldId==id&&m.ObjectKey!=null,token))continue;
                var content=await safety.ForHoldAsync(hold,token);if(content.Until is null){review++;continue;}
                DateTimeOffset until=content.Until.Value;
                var known=true;
                foreach(var caseId in hold.RetentionCaseIds.Concat(hold.CaseId is{}own?[own]:Array.Empty<Guid>()).Distinct())
                {
                    var item=await db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==caseId,token);
                    if(item is null){known=false;break;}
                    var date=(await safety.CaseWindowAsync(item,token)).RecordsUntil;
                    if(date is null){known=false;break;}if(date>until)until=date.Value;
                }
                if(!known){review++;continue;}
                var exports=db.EvidenceExports.Where(e=>e.HoldId==id);var exportIds=exports.Select(e=>e.Id);
                var police=hold.LeCaseId!=null||await exports.AnyAsync(e=>e.LeCaseId!=null,token)||await db.Disclosures.AnyAsync(d=>d.HoldId==id,token);
                var latest=new List<DateTimeOffset?>{hold.PlacedAt,hold.ReleasedAt,hold.ConfirmedAt,hold.NoReviewRetentionConfirmedAt,
                    await db.CustodyEvents.Where(e=>e.HoldId==id).MaxAsync(e=>(DateTimeOffset?)e.At,token),
                    await exports.MaxAsync(e=>(DateTimeOffset?)e.CreatedAt,token),await exports.MaxAsync(e=>e.DownloadedAt,token),
                    await db.ExportApprovals.Where(e=>e.HoldId==id).MaxAsync(e=>(DateTimeOffset?)e.RequestedAt,token),
                    await db.ExportApprovals.Where(e=>e.HoldId==id).MaxAsync(e=>e.ApprovedAt,token),await db.ExportApprovals.Where(e=>e.HoldId==id).MaxAsync(e=>e.UsedAt,token),
                    await db.Disclosures.Where(e=>e.HoldId==id).MaxAsync(e=>(DateTimeOffset?)e.DisclosedAt,token),
                    await db.EvidenceRedactions.Where(e=>exportIds.Contains(e.ExportId)).MaxAsync(e=>(DateTimeOffset?)e.At,token),
                    await db.LeTransfers.Where(e=>exportIds.Contains(e.ExportId)).MaxAsync(e=>(DateTimeOffset?)e.SentAt,token),
                    await db.LeTransfers.Where(e=>exportIds.Contains(e.ExportId)).MaxAsync(e=>e.ReceiptAt,token)};
                var operationalUntil=latest.Max()!.Value.AddMonths(police?84:24);if(operationalUntil>until)until=operationalUntil;
                if(now<until)continue;
                await using var transaction=await db.Database.BeginTransactionAsync(token);
                await db.LeTransfers.Where(e=>exportIds.Contains(e.ExportId)).ExecuteDeleteAsync(token);
                await db.EvidenceRedactions.Where(e=>exportIds.Contains(e.ExportId)).ExecuteDeleteAsync(token);
                await db.ExportApprovals.Where(e=>e.HoldId==id).ExecuteDeleteAsync(token);
                await exports.ExecuteDeleteAsync(token);await db.Disclosures.Where(e=>e.HoldId==id).ExecuteDeleteAsync(token);
                await db.CustodyEvents.Where(e=>e.HoldId==id).ExecuteDeleteAsync(token);
                await db.LegalHolds.Where(h=>h.Id==id).ExecuteDeleteAsync(token);
                Audit.Record(db,now,null,"retention","hold.operational-records-expired","legal-hold",id.ToString());
                await db.SaveChangesAsync(token);await transaction.CommitAsync(token);disposed++;
                db.Entry(hold).State=EntityState.Detached;
            }
            after=ids[^1];
        }
        return new(disposed,review);
    }
}
