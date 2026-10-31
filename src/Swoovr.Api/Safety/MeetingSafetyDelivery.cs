using System.Buffers.Binary;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Notifications;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Messaging;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Safety;
public sealed class MeetingSafetyDelivery(SwoovrDbContext db,MeetingSafetyStore store,IEmailSender email,IVerificationMessageSender sms,Notifier notifier,IConfiguration config,IHostEnvironment environment,TimeProvider clock)
{
    public static Uri PublicBase(IConfiguration config,IHostEnvironment environment)
    {
        if(!Uri.TryCreate(config["MeetingSafety:PublicBaseUrl"],UriKind.Absolute,out var value)||value.UserInfo!=""||value.Query!=""||value.Fragment!=""||value.AbsolutePath!="/"
            ||(environment.IsDevelopment()?!value.IsLoopback||value.Scheme is not("http" or "https"):value.Scheme!="https"||!value.IsDefaultPort||!(value.Host=="swoovr.app"||value.Host.EndsWith(".swoovr.app",StringComparison.Ordinal))))throw new InvalidOperationException("Meeting safety requires its explicit trusted public origin.");
        return value;
    }
    public async Task RunAsync(CancellationToken token)
    {
        var now=clock.GetUtcNow();
        var items=await db.MeetingSafetyOutbox.AsNoTracking().Where(o=>(o.Status=="pending"||o.Status=="retry"||o.Status=="attempting")&&o.NextAttemptAt<=now).OrderBy(o=>o.NextAttemptAt).Select(o=>new{o.Id,o.MemberId}).Take(100).ToListAsync(token);
        foreach(var item in items)
        {
            await using var owner=await AccountLifecycleLease.TryAcquireAsync(db,item.MemberId,token);if(owner is null)continue;
            await using var preservation=await PreservationLease.SharedAsync(db,token);db.ChangeTracker.Clear();
            var row=await db.MeetingSafetyOutbox.SingleOrDefaultAsync(o=>o.Id==item.Id,token);if(row is null||row.NextAttemptAt>now)continue;
            if(row.Status=="attempting")
            {row.Status="unknown";row.CompletedAt=now;await StatusAsync(row,token);continue;}
            if(row.Status is not("pending" or "retry"))continue;
            if(!await store.LiveAsync(row.MemberId,token)){row.Status="suppressed";row.CompletedAt=now;await StatusAsync(row,token);continue;}
            if(row.ContactId is null)
            {
                var session=await db.MeetingSafetySessions.AsNoTracking().SingleOrDefaultAsync(s=>s.Id==row.SessionId,token);
                if(session is null||session.MemberId!=row.MemberId||(row.Kind=="after"?!MeetingSafetyPolicy.AfterPrompt(session,now):session.ClosedAt!=null)||row.CheckInId!=null&&await db.MeetingSafetyCheckIns.AnyAsync(c=>c.Id==row.CheckInId&&c.RespondedAt!=null,token)){row.Status="suppressed";row.CompletedAt=now;await db.SaveChangesAsync(token);continue;}
                await notifier.NotifyAsync(row.MemberId,NotificationKind.MeetingSafety,null,null,row.SessionId,now,token,dedupe:"meeting-safety:"+row.Id);
                row.Status=await db.Notifications.AnyAsync(n=>n.DedupeKey=="meeting-safety:"+row.Id,token)?"centre-recorded":"failed";row.CompletedAt=now;await db.SaveChangesAsync(token);continue;
            }
            var contact=await db.TrustedContacts.SingleOrDefaultAsync(c=>c.Id==row.ContactId&&c.MemberId==row.MemberId,token);
            if(contact is null||row.Kind=="invite"&&contact.Consent!=TrustedContactConsent.Pending||row.Kind!="invite"&&(contact.Consent!=TrustedContactConsent.Confirmed||contact.ConfirmedAt==null))
            {row.Status="suppressed";row.CompletedAt=now;await StatusAsync(row,token);continue;}
            string body;
            await using(var transaction=await db.Database.BeginTransactionAsync(token))
            {
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x4D534346}, {BinaryPrimitives.ReadInt32BigEndian(contact.DestinationLookupHash)})",token);
                var sent=db.MeetingSafetyOutbox.Where(o=>o.AttemptedAt!=null);
                var destinationSent=from o in sent join c in db.TrustedContacts on o.ContactId equals c.Id where c.DestinationLookupHash==contact.DestinationLookupHash select o;
                if(await sent.CountAsync(o=>o.ContactId==contact.Id&&o.AttemptedAt>now.AddHours(-1),token)>=10
                    ||await sent.CountAsync(o=>o.ContactId==contact.Id&&o.AttemptedAt>now.AddDays(-1),token)>=40
                    ||await destinationSent.CountAsync(o=>o.AttemptedAt>now.AddHours(-1),token)>=20
                    ||await destinationSent.CountAsync(o=>o.AttemptedAt>now.AddDays(-1),token)>=100)
                {row.Status="rate-limited";row.CompletedAt=now;await StatusAsync(row,token);await transaction.CommitAsync(token);continue;}
                var origin=PublicBase(config,environment);
                if(row.Kind=="invite")
                {
                    var confirmation=MeetingSafetyPolicy.Token();var management=MeetingSafetyPolicy.Token();contact.ConsentTokenHash=MeetingSafetyPolicy.TokenHash(confirmation);contact.ConsentTokenExpiresAt=now.AddDays(7);contact.ManagementTokenHash=MeetingSafetyPolicy.TokenHash(management);
                    body=$"{contact.PreferredFirstName} has invited you to be a Swoovr trusted contact. Only accept if you know this person and agree to receive voluntary meeting-safety updates. This is not marketing. Swoovr does not monitor emergencies or dispatch responders. Alerts are not guaranteed to arrive.\n\nConfirm or decline: {new Uri(origin,"safety/contact/"+confirmation)}\nWithdraw/unsubscribe: {new Uri(origin,"safety/contact/"+management)}";
                }
                else
                {
                    var recipient=await db.MeetingSafetyRecipients.SingleOrDefaultAsync(r=>r.SessionId==row.SessionId&&r.TrustedContactId==contact.Id&&r.RevokedAt==null,token);
                    var session=await db.MeetingSafetySessions.SingleOrDefaultAsync(s=>s.Id==row.SessionId,token);
                    if(recipient is null||session is null||session.MemberId!=row.MemberId||session.Status==MeetingSafetyStatus.Revoked||session.ClosedAt!=null&&row.Kind!="closed")
                    {row.Status="suppressed";row.CompletedAt=now;await StatusAsync(row,token);await transaction.CommitAsync(token);continue;}
                    if(row.Kind=="missed"&&(row.CheckInId is null||!await db.MeetingSafetyCheckIns.AnyAsync(c=>c.Id==row.CheckInId&&c.SessionId==session.Id&&c.RespondedAt==null&&c.EscalatedAt!=null,token)))
                    {row.Status="suppressed";row.CompletedAt=now;await StatusAsync(row,token);await transaction.CommitAsync(token);continue;}
                    var expires=MeetingSafetyPolicy.TokenExpiry(session,now);if(expires<=now){row.Status="suppressed";row.CompletedAt=now;await StatusAsync(row,token);await transaction.CommitAsync(token);continue;}
                    var raw=MeetingSafetyPolicy.Token();db.MeetingSafetyLinks.Add(new(){Id=Guid.NewGuid(),RecipientId=recipient.Id,TokenHash=MeetingSafetyPolicy.TokenHash(raw),CreatedAt=now,ExpiresAt=expires});
                    var status=row.Kind switch{"missed"=>"A check-in is missed/unconfirmed. This is not proof of an emergency.","help"=>"The member requested help. Contact them directly using your usual contact details.","closed"=>session.Status==MeetingSafetyStatus.HomeSafe?"The member marked home safe; monitoring has ended.":"Monitoring has ended without an assertion of safety.",_=>"The member has shared a voluntary meeting safety plan."};
                    body=$"{session.PreferredFirstName}: {status}\nMember-provided, not independently verified. Verify with a short-lived code before viewing meeting times or a shared place.\n\nRead-only meeting status: {new Uri(origin,"safety/meeting/"+raw)}\nSwoovr does not monitor emergencies or dispatch responders. Alerts are not guaranteed to arrive. If needed, use local emergency services. Never treat a missing alert as proof of safety.\nWithdraw/unsubscribe: {new Uri(origin,"safety/contact/"+raw)}";
                    recipient.NotificationStatus="attempting";
                }
                row.Status="attempting";row.Attempts++;row.AttemptedAt=now;row.NextAttemptAt=now;await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
            }
            EmailOutcome outcome;
            try
            {
                var destination=store.Destination(contact);
                outcome=contact.Channel=="email"?await email.SendOutcomeAsync(new EmailMessage(destination,"Swoovr meeting safety",body),token):new(await sms.SendNoticeAsync(destination,body,token)?sms.Kind=="simulated"?"simulated":"accepted":"unknown");
            }
            catch(Exception error)when(error is HttpRequestException or IOException or OperationCanceledException){outcome=new("unknown");}
            row.Status=outcome.Status switch{"accepted"=>environment.IsDevelopment()?"local-sink-accepted":"accepted","simulated"=>"simulated","retry" when row.Attempts<3=>"retry","retry"=>"rejected","rejected"=>"rejected",_=>"unknown"};
            row.CompletedAt=row.Status=="retry"?null:clock.GetUtcNow();row.NextAttemptAt=clock.GetUtcNow().AddMinutes(Math.Min(60,Math.Pow(2,row.Attempts)));
            await StatusAsync(row,CancellationToken.None);
        }
    }
    private async Task StatusAsync(MeetingSafetyOutbox row,CancellationToken token)
    {
        if(row.ContactId is{}contact)
        {
            if(row.Kind=="invite")await db.TrustedContacts.Where(c=>c.Id==contact).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.InvitationStatus,row.Status),token);
            else await db.MeetingSafetyRecipients.Where(r=>r.SessionId==row.SessionId&&r.TrustedContactId==contact).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.NotificationStatus,row.Status),token);
        }
        await db.SaveChangesAsync(token);
    }
}
