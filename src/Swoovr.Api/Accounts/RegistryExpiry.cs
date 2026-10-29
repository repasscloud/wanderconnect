using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>Append-only status reconciliation. Matching enforces time independently; this job destroys no evidence.</summary>
public sealed class RegistryExpiry(SwoovrDbContext db)
{
    public async Task<int> RunAsync(DateTimeOffset now,CancellationToken token)
    {
        var changed=0;Guid? after=null;
        while(true)
        {
            var due=db.BannedIdentifiers.Where(b=>b.Status==BanStatus.Active&&b.BanExpiresAt!=null&&b.BanExpiresAt<=now);
            if(after is{}cursor)due=due.Where(b=>b.Id.CompareTo(cursor)>0);
            var page=await due.OrderBy(b=>b.Id).Select(b=>b.Id).Take(200).ToListAsync(token);
            if(page.Count==0)return changed;
            foreach(var id in page)
            {
                await using var authority=await PreservationLease.ExclusiveAsync(db,token);
                var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);
                if(row is null)continue;await db.Entry(row).ReloadAsync(token);
                if(row.Status!=BanStatus.Active||row.BanExpiresAt is null||row.BanExpiresAt>now)continue;
                if(!await db.ModerationCases.AnyAsync(c=>c.Id==row.CaseId,token))throw new InvalidOperationException("Expiry source case is unavailable.");
                var result=BannedIdentifierPolicy.Expire(row,now);
                db.BanTransitions.Add(result.Transition);db.BanCaseNotes.Add(result.Note);
                // EF SaveChanges transaction commits status, immutable event and associated note together.
                await db.SaveChangesAsync(token);changed++;
                // Bound this sweep's tracked state without clearing unrelated coordinator entities.
                db.Entry(row).State=EntityState.Detached;
                db.Entry(result.Transition).State=EntityState.Detached;
                db.Entry(result.Note).State=EntityState.Detached;
            }
            after=page[^1];
        }
    }
}
