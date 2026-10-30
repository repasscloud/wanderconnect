using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Safety;
public sealed class MeetingSafetyRetention(SwoovrDbContext db,EvidencePreservation evidence,SafetyRetention safety)
{
    public async Task<bool> RunAsync(DateTimeOffset now,CancellationToken token)
    {
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        if(await evidence.TryHeldAccountsAsync(token) is null)return false;
        var protectedMembers=new HashSet<Guid>();
        foreach(var hold in await db.LegalHolds.AsNoTracking().ToListAsync(token))
        {
            if(hold.RelatedAccountIds is null||hold.RetentionCaseIds is null)return false;
            var accounts=await SafetyRetention.HoldAccountsAsync(db,hold,token);var window=await safety.ForHoldAsync(hold,token);
            if(window.Until is null||window.Until>now)protectedMembers.UnionWith(accounts);
        }
        // Linked incident/review provenance keeps the original session until its independent report lifecycle ends.
        var sessions=await db.MeetingSafetySessions.Where(s=>s.PurgeAfter<=now&&!protectedMembers.Contains(s.MemberId)&&!db.Reports.Any(r=>r.MeetingSafetySessionId==s.Id)).OrderBy(s=>s.PurgeAfter).Select(s=>s.Id).Take(200).ToListAsync(token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        await db.MeetingSafetyOutbox.Where(o=>o.SessionId!=null&&sessions.Contains(o.SessionId.Value)).ExecuteDeleteAsync(token);
        await db.MeetingSafetySessions.Where(s=>sessions.Contains(s.Id)).ExecuteDeleteAsync(token);
        var contacts=await db.TrustedContacts.Where(c=>c.PurgeAfter<=now&&!protectedMembers.Contains(c.MemberId)&&!db.MeetingSafetyRecipients.Any(r=>r.TrustedContactId==c.Id)).OrderBy(c=>c.PurgeAfter).Select(c=>c.Id).Take(200).ToListAsync(token);
        await db.MeetingSafetyOutbox.Where(o=>o.ContactId!=null&&contacts.Contains(o.ContactId.Value)).ExecuteDeleteAsync(token);
        await db.TrustedContacts.Where(c=>contacts.Contains(c.Id)).ExecuteDeleteAsync(token);
        // Preserve at least the full rolling quota window, and retain incident/hold provenance.
        var disposableRecipients=from r in db.MeetingSafetyRecipients join s in db.MeetingSafetySessions on r.SessionId equals s.Id
            where !protectedMembers.Contains(s.MemberId)&&!db.Reports.Any(report=>report.MeetingSafetySessionId==s.Id) select r.Id;
        await db.Set<MeetingSafetyContactChallenge>().Where(c=>c.CreatedAt<=now.AddDays(-1)&&c.ExpiresAt<=now&&disposableRecipients.Contains(c.RecipientId)).ExecuteDeleteAsync(token);
        await db.Set<MeetingSafetyContactReadGrant>().Where(g=>g.ExpiresAt<=now.AddDays(-1)&&disposableRecipients.Contains(g.RecipientId)).ExecuteDeleteAsync(token);
        await db.MeetingSafetyOutbox.Where(o=>o.SessionId==null&&o.CompletedAt<=now.AddDays(-30)&&!protectedMembers.Contains(o.MemberId)).ExecuteDeleteAsync(token);
        if(sessions.Count+contacts.Count>0)Swoovr.Api.Accounts.Audit.Record(db,now,null,"system","meeting-safety.records-expired","retention",null,(sessions.Count+contacts.Count).ToString(System.Globalization.CultureInfo.InvariantCulture));
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return true;
    }
}
