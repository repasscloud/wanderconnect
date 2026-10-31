using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Safety;
public sealed class MeetingSafetySchedule(SwoovrDbContext db,MeetingSafetyStore store,TimeProvider clock)
{
    public async Task RunAsync(CancellationToken token)
    {
        var now=clock.GetUtcNow();Guid? after=null;
        while(true)
        {
            var query=db.MeetingSafetySessions.Where(s=>s.ClosedAt==null||s.AfterPromptDismissedAt==null&&s.FinalCheckInAt<=now.AddHours(-2)&&s.PurgeAfter>now);
            if(after is{}cursor)query=query.Where(s=>s.Id.CompareTo(cursor)>0);
            var ids=await query.OrderBy(s=>s.Id).Select(s=>new{s.Id,s.MemberId}).Take(200).ToListAsync(token);if(ids.Count==0)break;
            foreach(var row in ids)
            {
                await using var owner=await AccountLifecycleLease.TryAcquireAsync(db,row.MemberId,token);if(owner is null)continue;
                await using var preservation=await PreservationLease.SharedAsync(db,token);
                db.ChangeTracker.Clear();var session=await db.MeetingSafetySessions.SingleOrDefaultAsync(s=>s.Id==row.Id,token);if(session is null)continue;
                await using var transaction=await db.Database.BeginTransactionAsync(token);
                if(session.ClosedAt==null)
                {
                    var contacts=await(from r in db.MeetingSafetyRecipients join c in db.TrustedContacts on r.TrustedContactId equals c.Id where r.SessionId==session.Id&&r.RevokedAt==null&&c.Consent==TrustedContactConsent.Confirmed&&c.ConfirmedAt!=null select r.Id).AnyAsync(token);
                    if(!contacts||!await store.LiveAsync(session.MemberId,token))
                    {await store.CloseAsync(session,MeetingSafetyStatus.Revoked,now,token);store.Event(session,"monitoring-revoked",now);}
                    else
                    {
                        if(session.Status==MeetingSafetyStatus.Planned&&session.StartsAt<=now){session.Status=MeetingSafetyStatus.Active;session.UpdatedAt=now;store.Event(session,"started",now);}
                        foreach(var check in await db.MeetingSafetyCheckIns.Where(c=>c.SessionId==session.Id&&c.RespondedAt==null&&c.DueAt<=now).OrderBy(c=>c.DueAt).ToListAsync(token))
                        {
                            await PromptAsync(session,check,"prompt",now,token);
                            if(now>=check.GraceEndsAt.AddMinutes(-5)&&now<check.GraceEndsAt)await PromptAsync(session,check,"reminder",now,token);
                            if(MeetingSafetyPolicy.Missed(check,now))
                            {
                                check.EscalatedAt=now;if(session.Status!=MeetingSafetyStatus.HelpRequested)session.Status=MeetingSafetyStatus.MissedCheckIn;session.UpdatedAt=now;
                                store.Event(session,"missed-check-in",now,check.Id);await store.QueueContactsAsync(session,"missed",check.Id.ToString(),now,token,check.Id);
                            }
                            else if(check.EscalatedAt==null&&session.Status is not(MeetingSafetyStatus.HelpRequested or MeetingSafetyStatus.MissedCheckIn)){session.Status=MeetingSafetyStatus.CheckInDue;session.UpdatedAt=now;}
                        }
                        if(now>=session.FinalCheckInAt.AddHours(24)){await store.CloseAsync(session,MeetingSafetyStatus.Expired,now,token);store.Event(session,"expired",now);}
                    }
                }
                if(MeetingSafetyPolicy.AfterPrompt(session,now))
                {
                    var key="after:"+session.Id;if(!await db.MeetingSafetyOutbox.AnyAsync(o=>o.DedupeKey==key,token))store.Queue(session.MemberId,null,session.Id,null,"after",key,now);
                }
                await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
            }
            after=ids[^1].Id;
        }
        foreach(var member in await db.TrustedContacts.Where(c=>c.Consent==TrustedContactConsent.Pending||c.Consent==TrustedContactConsent.Confirmed).Select(c=>c.MemberId).Distinct().ToListAsync(token))
        {
            await using var owner=await AccountLifecycleLease.TryAcquireAsync(db,member,token);if(owner is null)continue;
            if(await store.LiveAsync(member,token))continue;
            await using var preservation=await PreservationLease.SharedAsync(db,token);
            await using var transaction=await db.Database.BeginTransactionAsync(token);await store.RevokeAccountAsync(member,now,token);await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        }
        // Unanswered invitations expire independently of session closure and never imply the contact declined.
        foreach(var row in await db.TrustedContacts.AsNoTracking().Where(c=>c.Consent==TrustedContactConsent.Pending&&c.ConsentTokenExpiresAt<=now).Select(c=>new{c.Id,c.MemberId}).Take(200).ToListAsync(token))
        {
            await using var owner=await AccountLifecycleLease.TryAcquireAsync(db,row.MemberId,token);if(owner is null)continue;
            var contact=await db.TrustedContacts.SingleAsync(c=>c.Id==row.Id,token);if(contact.Consent!=TrustedContactConsent.Pending||contact.ConsentTokenExpiresAt>now)continue;
            contact.Consent=TrustedContactConsent.Expired;contact.PurgeAfter=contact.ConsentTokenExpiresAt!.Value.AddDays(30);contact.ConsentTokenHash=null;contact.ManagementTokenHash=null;await db.SaveChangesAsync(token);
        }
    }
    private async Task PromptAsync(MeetingSafetySession session,MeetingSafetyCheckIn check,string kind,DateTimeOffset now,CancellationToken token)
    {var key=kind+":"+check.Id;if(!await db.MeetingSafetyOutbox.AnyAsync(o=>o.DedupeKey==key,token))store.Queue(session.MemberId,null,session.Id,check.Id,kind,key,now);}
}
/// <summary>Scheduling never waits on delivery. Separate scopes and bounded loops keep deadlines server-owned.</summary>
public sealed class MeetingSafetyWorker(IServiceScopeFactory scopes,ILogger<MeetingSafetyWorker> log):BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken token)=>Task.WhenAll(Loop(true,token),Loop(false,token));
    private async Task Loop(bool scheduling,CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            try
            {
                using var scope=scopes.CreateScope();
                if(scheduling)await scope.ServiceProvider.GetRequiredService<MeetingSafetySchedule>().RunAsync(token);
                else await scope.ServiceProvider.GetRequiredService<MeetingSafetyDelivery>().RunAsync(token);
            }
            catch(Exception error)when(!token.IsCancellationRequested){log.LogWarning("Meeting safety {Phase} failed ({Type}); no delivery claim.",scheduling?"schedule":"delivery",error.GetType().Name);}
            await Task.Delay(scheduling?TimeSpan.FromSeconds(15):TimeSpan.FromSeconds(2),token);
        }
    }
}
