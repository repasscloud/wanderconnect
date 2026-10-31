using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Safety;
public sealed class MeetingSafetyRefusal(int status,string code):Exception(code)
{public int Status{get;}=status;public string Code{get;}=code;}
public sealed class MeetingSafetyStore(SwoovrDbContext db,ContactHistoryKeys keys,TimeProvider clock,IVerificationMessageSender sms,IHostEnvironment environment)
{
    public static void Refuse(string code,int status=400)=>throw new MeetingSafetyRefusal(status,code);
    public static bool Text(string? value,int max,bool required=false)=>value is null?!required:value.Length<=max&&(!required||!string.IsNullOrWhiteSpace(value))&&!value.Any(c=>char.IsControl(c)&&c!='\n');
    public async Task<bool> LiveAsync(Guid member,CancellationToken token)
    {
        if(!await db.Accounts.AnyAsync(a=>a.Id==member&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,token))return false;
        return !Enforcement.For(await db.Sanctions.Where(s=>s.AccountId==member&&s.LiftedAt==null).ToListAsync(token),clock.GetUtcNow()).Barred;
    }
    public string Destination(TrustedContact contact)=>ContactHistoryPolicy.Decrypt(keys.Encryption,contact.DestinationEncrypted);
    public byte[] Encrypt(string value)
    {
        if(keys.CurrentVersion is not{}version)throw new MeetingSafetyRefusal(503,"meeting-safety-keys-unavailable");
        return ContactHistoryPolicy.Encrypt(version,keys.Encryption[version],value);
    }
    public async Task<TrustedContactView> AddContactAsync(Guid member,TrustedContactAdd request,CancellationToken token)
    {
        if(!Text(request.DisplayName,80,true)||!Text(request.PreferredFirstName,50,true)||request.Destination is null||MeetingSafetyPolicy.Destination(request.Channel,request.Destination.Trim()) is not{}destination)throw new MeetingSafetyRefusal(400,"invalid-trusted-contact");
        if(request.Channel=="sms"&&!environment.IsDevelopment()&&sms.Kind!="twilio")throw new MeetingSafetyRefusal(503,"meeting-safety-sms-unavailable");
        if(keys.Lookup is null)throw new MeetingSafetyRefusal(503,"meeting-safety-keys-unavailable");
        var hash=HMACSHA256.HashData(keys.Lookup,Encoding.UTF8.GetBytes("swoovr:meeting-contact|"+request.Channel+"|"+destination));
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,member,token);
        await using var preservation=await PreservationLease.SharedAsync(db,token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x4D534346},{System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(hash)})",token);
        if(!await LiveAsync(member,token))throw new MeetingSafetyRefusal(403,"meeting-safety-unavailable");
        var now=clock.GetUtcNow();var day=now.AddDays(-1);
        if(await db.TrustedContacts.CountAsync(c=>c.MemberId==member&&(c.Consent==TrustedContactConsent.Pending||c.Consent==TrustedContactConsent.Confirmed),token)>=3)throw new MeetingSafetyRefusal(409,"trusted-contact-limit");
        if(await db.TrustedContacts.AnyAsync(c=>c.MemberId==member&&c.DestinationLookupHash==hash&&((c.Consent==TrustedContactConsent.Pending||c.Consent==TrustedContactConsent.Confirmed)||((c.Consent==TrustedContactConsent.Declined||c.Consent==TrustedContactConsent.Withdrawn)&&c.WithdrawnAt>now.AddDays(-30))),token)
            ||await (from o in db.MeetingSafetyOutbox join c in db.TrustedContacts on o.ContactId equals (Guid?)c.Id where o.Kind=="invite"&&c.DestinationLookupHash==hash&&o.CreatedAt>day select o.Id).CountAsync(token)>=3
            ||await db.MeetingSafetyOutbox.CountAsync(o=>o.MemberId==member&&o.Kind=="invite"&&o.CreatedAt>day,token)>=6)throw new MeetingSafetyRefusal(429,"trusted-contact-invitation-unavailable");
        var contact=new TrustedContact{Id=Guid.NewGuid(),MemberId=member,DisplayName=request.DisplayName.Trim(),PreferredFirstName=request.PreferredFirstName.Trim(),Channel=request.Channel,DestinationEncrypted=Encrypt(destination),DestinationLookupHash=hash,CreatedAt=now};
        db.TrustedContacts.Add(contact);Queue(member,contact.Id,null,null,"invite","invite:"+contact.Id,now);
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return ContactView(contact);
    }
    public async Task RevokeAccountAsync(Guid member,DateTimeOffset now,CancellationToken token)
    {
        foreach(var contact in await db.TrustedContacts.Where(c=>c.MemberId==member&&(c.Consent==TrustedContactConsent.Pending||c.Consent==TrustedContactConsent.Confirmed)).ToListAsync(token))await WithdrawAsync(contact,TrustedContactConsent.Removed,now,token);
        foreach(var session in await db.MeetingSafetySessions.Where(s=>s.MemberId==member&&s.ClosedAt==null).ToListAsync(token))
        {await CloseAsync(session,MeetingSafetyStatus.Revoked,now,token);Event(session,"account-monitoring-revoked",now);}
    }
    public async Task RemoveContactAsync(Guid member,Guid id,CancellationToken token)
    {
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,member,token);await using var preservation=await PreservationLease.SharedAsync(db,token);
        var contact=await db.TrustedContacts.SingleOrDefaultAsync(c=>c.Id==id&&c.MemberId==member,token)??throw new MeetingSafetyRefusal(404,"trusted-contact-unavailable");
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        await WithdrawAsync(contact,TrustedContactConsent.Removed,clock.GetUtcNow(),token);await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
    }
    public async Task ResendAsync(Guid member,Guid id,CancellationToken token)
    {
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,member,token);await using var preservation=await PreservationLease.SharedAsync(db,token);
        var contact=await db.TrustedContacts.SingleOrDefaultAsync(c=>c.Id==id&&c.MemberId==member,token)??throw new MeetingSafetyRefusal(404,"trusted-contact-unavailable");
        if(contact.Consent!=TrustedContactConsent.Pending||!await LiveAsync(member,token))throw new MeetingSafetyRefusal(409,"trusted-contact-invitation-unavailable");
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x4D534346},{System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(contact.DestinationLookupHash)})",token);
        var now=clock.GetUtcNow();
        if(await(from o in db.MeetingSafetyOutbox join c in db.TrustedContacts on o.ContactId equals (Guid?)c.Id where o.Kind=="invite"&&c.DestinationLookupHash==contact.DestinationLookupHash&&o.CreatedAt>now.AddDays(-1) select o.Id).CountAsync(token)>=3)throw new MeetingSafetyRefusal(429,"trusted-contact-invitation-unavailable");
        var prior=await db.MeetingSafetyOutbox.Where(o=>o.ContactId==id&&o.Kind=="invite").OrderByDescending(o=>o.CreatedAt).FirstAsync(token);
        if(prior.CreatedAt>now.AddHours(-1)||prior.Status is "pending" or "attempting"||await db.MeetingSafetyOutbox.CountAsync(o=>o.MemberId==member&&o.Kind=="invite"&&o.CreatedAt>now.AddDays(-1),token)>=6)throw new MeetingSafetyRefusal(429,"trusted-contact-invitation-unavailable");
        Queue(member,id,null,null,"invite","invite:"+id+":"+Guid.NewGuid(),now);contact.InvitationStatus="pending";await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
    }
    private async Task<TrustedContact?> ContactCapabilityAsync(byte[] hash,bool managementOnly,CancellationToken token)
    {
        var contact=await db.TrustedContacts.AsNoTracking().SingleOrDefaultAsync(c=>c.ManagementTokenHash==hash||!managementOnly&&c.ConsentTokenHash==hash,token);
        if(contact is not null)return contact;
        var now=clock.GetUtcNow();
        return await(from link in db.MeetingSafetyLinks join recipient in db.MeetingSafetyRecipients on link.RecipientId equals recipient.Id join c in db.TrustedContacts on recipient.TrustedContactId equals c.Id
            where link.TokenHash==hash&&link.ExpiresAt>now&&link.RevokedAt==null&&recipient.RevokedAt==null&&c.Consent==TrustedContactConsent.Confirmed&&c.ConfirmedAt!=null select c).AsNoTracking().SingleOrDefaultAsync(token);
    }
    public async Task<MeetingSafetyConsentView> ConsentStatusAsync(string raw,CancellationToken token)
    {
        if(!MeetingSafetyPolicy.TokenShape(raw))return new(false,"unavailable");var hash=MeetingSafetyPolicy.TokenHash(raw);var now=clock.GetUtcNow();
        var contact=await ContactCapabilityAsync(hash,false,token);
        if(contact is null||!await LiveAsync(contact.MemberId,token)||contact.Consent is TrustedContactConsent.Declined or TrustedContactConsent.Withdrawn or TrustedContactConsent.Removed)return new(false,"unavailable");
        if(contact.Consent==TrustedContactConsent.Confirmed||contact.ManagementTokenHash is{}management&&CryptographicOperations.FixedTimeEquals(management,hash))return new(true,"manage");
        return contact.Consent==TrustedContactConsent.Pending&&contact.ConsentRespondedAt==null&&contact.ConsentTokenExpiresAt>now?new(true,"pending"):new(false,"unavailable");
    }
    public async Task<MeetingSafetyConsentView> ConsentAsync(string raw,string action,CancellationToken token)
    {
        if(!MeetingSafetyPolicy.TokenShape(raw)||action is not("confirm" or "decline" or "withdraw"))return new(false,"unavailable");
        var hash=MeetingSafetyPolicy.TokenHash(raw);
        var found=action=="withdraw"?await ContactCapabilityAsync(hash,true,token):await db.TrustedContacts.AsNoTracking().SingleOrDefaultAsync(c=>c.ConsentTokenHash==hash,token);
        if(found is null)return new(false,"unavailable");
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,found.MemberId,token);await using var preservation=await PreservationLease.SharedAsync(db,token);
        var contact=await db.TrustedContacts.SingleAsync(c=>c.Id==found.Id,token);var now=clock.GetUtcNow();
        if(!await LiveAsync(found.MemberId,token))return new(false,"unavailable");
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        if(action=="withdraw")
        {
            if(await ContactCapabilityAsync(hash,true,token) is null||contact.Consent is TrustedContactConsent.Withdrawn or TrustedContactConsent.Removed or TrustedContactConsent.Declined or TrustedContactConsent.Expired)return new(false,"unavailable");
            await WithdrawAsync(contact,TrustedContactConsent.Withdrawn,now,token);
        }
        else
        {
            if(contact.Consent!=TrustedContactConsent.Pending||contact.ConsentRespondedAt!=null||contact.ConsentTokenExpiresAt==null||contact.ConsentTokenExpiresAt<=now||contact.ConsentTokenHash is null||!CryptographicOperations.FixedTimeEquals(contact.ConsentTokenHash,hash))return new(false,"unavailable");
            contact.ConsentRespondedAt=now;contact.ConsentTokenHash=null;
            if(action=="confirm"){contact.Consent=TrustedContactConsent.Confirmed;contact.ConfirmedAt=now;}
            else await WithdrawAsync(contact,TrustedContactConsent.Declined,now,token);
        }
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return new(true,contact.Consent.ToString().ToLowerInvariant());
    }
    public async Task WithdrawAsync(TrustedContact contact,TrustedContactConsent state,DateTimeOffset now,CancellationToken token)
    {
        contact.Consent=state;contact.WithdrawnAt=now;contact.PurgeAfter=now.AddDays(30);contact.ConsentTokenHash=null;contact.ManagementTokenHash=null;
        var ids=db.MeetingSafetyRecipients.Where(r=>r.TrustedContactId==contact.Id).Select(r=>r.Id);
        await db.MeetingSafetyLinks.Where(l=>ids.Contains(l.RecipientId)&&l.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(l=>l.RevokedAt,now),token);
        await db.MeetingSafetyRecipients.Where(r=>r.TrustedContactId==contact.Id&&r.RevokedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.RevokedAt,now),token);
        await db.MeetingSafetyOutbox.Where(o=>o.ContactId==contact.Id&&o.Status=="pending").ExecuteUpdateAsync(s=>s.SetProperty(o=>o.Status,"suppressed").SetProperty(o=>o.CompletedAt,now),token);
    }
    public async Task<MeetingSafetySessionView> CreateAsync(Guid member,MeetingSafetyCreate request,CancellationToken token)
    {
        var now=clock.GetUtcNow();
        if(!Text(request.PreferredFirstName,50,true)||!Text(request.VenueOrArea,160)||!Text(request.MemberNote,500)||request.Contacts is not{Count:>=1 and<=3}
            ||request.Contacts.Distinct().Count()!=request.Contacts.Count||request.AdditionalCheckIns is{Count:>10}||!MeetingSafetyPolicy.ValidWindow(request.StartsAt,request.FinalCheckInAt,request.TimeZoneId,now))throw new MeetingSafetyRefusal(400,"invalid-meeting-safety-plan");
        var times=(request.AdditionalCheckIns??[]).Append(request.FinalCheckInAt).Distinct().Order().ToList();
        if(times.Any(t=>t.Offset!=TimeSpan.Zero||t<request.StartsAt||t<now||t>request.FinalCheckInAt))throw new MeetingSafetyRefusal(400,"invalid-meeting-safety-check-in");
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,member,token);await using var preservation=await PreservationLease.SharedAsync(db,token);
        if(!await LiveAsync(member,token))throw new MeetingSafetyRefusal(403,"meeting-safety-unavailable");
        if(await db.MeetingSafetySessions.CountAsync(s=>s.MemberId==member&&s.ClosedAt==null,token)>=5||await db.MeetingSafetySessions.CountAsync(s=>s.MemberId==member&&s.CreatedAt>now.AddDays(-1),token)>=10)throw new MeetingSafetyRefusal(429,"meeting-safety-plan-limit");
        var contacts=await db.TrustedContacts.Where(c=>c.MemberId==member&&request.Contacts.Contains(c.Id)&&c.Consent==TrustedContactConsent.Confirmed&&c.ConfirmedAt!=null).ToListAsync(token);
        if(contacts.Count!=request.Contacts.Count)throw new MeetingSafetyRefusal(409,"trusted-contact-confirmation-required");
        var session=new MeetingSafetySession{Id=Guid.NewGuid(),MemberId=member,PreferredFirstName=request.PreferredFirstName.Trim(),StartsAt=request.StartsAt,FinalCheckInAt=request.FinalCheckInAt,TimeZoneId=request.TimeZoneId,VenueOrArea=request.VenueOrArea?.Trim(),SharePlace=request.SharePlace,MemberNote=request.MemberNote?.Trim(),Status=request.StartsAt<=now?MeetingSafetyStatus.Active:MeetingSafetyStatus.Planned,CreatedAt=now,UpdatedAt=now};
        db.MeetingSafetySessions.Add(session);
        foreach(var time in times)db.MeetingSafetyCheckIns.Add(new(){Id=Guid.NewGuid(),SessionId=session.Id,DueAt=time,GraceEndsAt=time.AddMinutes(30)});
        foreach(var contact in contacts)
        {
            db.MeetingSafetyRecipients.Add(new(){Id=Guid.NewGuid(),SessionId=session.Id,TrustedContactId=contact.Id,CreatedAt=now});
            Queue(member,contact.Id,session.Id,null,"planned","planned:"+session.Id+":"+contact.Id,now);
        }
        Event(session,"planned",now);await db.SaveChangesAsync(token);return await SessionViewAsync(session,token);
    }
    public async Task<MeetingSafetySessionView> ActAsync(Guid member,Guid id,MeetingSafetyAction request,CancellationToken token)
    {
        if(request.CommandId==Guid.Empty||request.Action is not("ok" or "extend" or "home-safe" or "end" or "cancel" or "help" or "dismiss" or "feedback")||!Text(request.Feedback,1000))throw new MeetingSafetyRefusal(400,"invalid-meeting-safety-action");
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,member,token);await using var preservation=await PreservationLease.SharedAsync(db,token);
        var session=await db.MeetingSafetySessions.SingleOrDefaultAsync(s=>s.Id==id&&s.MemberId==member,token)??throw new MeetingSafetyRefusal(404,"meeting-safety-unavailable");
        if(!await LiveAsync(member,token))throw new MeetingSafetyRefusal(403,"meeting-safety-unavailable");
        var expiresNow=clock.GetUtcNow();
        if(session.ClosedAt==null&&expiresNow>=session.FinalCheckInAt.AddHours(24))
        {
            await using var expiry=await db.Database.BeginTransactionAsync(token);
            await CloseAsync(session,MeetingSafetyStatus.Expired,expiresNow,token);Event(session,"expired",expiresNow);
            await db.SaveChangesAsync(token);await expiry.CommitAsync(token);
        }
        if(await db.MeetingSafetyEvents.AnyAsync(e=>e.SessionId==id&&e.CommandId==request.CommandId,token))return await SessionViewAsync(session,token);
        var now=clock.GetUtcNow();await using var transaction=await db.Database.BeginTransactionAsync(token);
        if(request.Action is "feedback" or "dismiss")
        {session.AfterPromptDismissedAt=now;if(request.Action=="feedback")session.PrivateFeedback=request.Feedback?.Trim();}
        else
        {
            if(MeetingSafetyPolicy.Closed(session.Status))return await SessionViewAsync(session,token);
            switch(request.Action)
            {
                case "ok":case "extend":
                    var item=await db.MeetingSafetyCheckIns.SingleOrDefaultAsync(c=>c.Id==request.CheckInId&&c.SessionId==id,token)??throw new MeetingSafetyRefusal(404,"meeting-safety-check-in-unavailable");
                    if(request.Action=="ok"&&item.DueAt>now)throw new MeetingSafetyRefusal(409,"meeting-safety-check-in-not-due");
                    if(item.RespondedAt is not null)return await SessionViewAsync(session,token);
                    item.RespondedAt=now;item.Response=request.Action;
                    if(request.Action=="extend")
                    {
                        var due=(item.DueAt>now?item.DueAt:now).AddMinutes(30);
                        if(due>session.StartsAt.AddDays(30))throw new MeetingSafetyRefusal(409,"meeting-safety-extension-limit");
                        if(!await db.MeetingSafetyCheckIns.AnyAsync(c=>c.SessionId==id&&c.DueAt==due,token))db.MeetingSafetyCheckIns.Add(new(){Id=Guid.NewGuid(),SessionId=id,DueAt=due,GraceEndsAt=due.AddMinutes(30)});
                        if(due>session.FinalCheckInAt)session.FinalCheckInAt=due;
                        var links=db.MeetingSafetyLinks.Where(l=>db.MeetingSafetyRecipients.Any(r=>r.Id==l.RecipientId&&r.SessionId==id)&&l.RevokedAt==null&&l.ExpiresAt>now);
                        foreach(var link in await links.ToListAsync(token))link.ExpiresAt=MeetingSafetyPolicy.TokenExpiry(session,link.CreatedAt);
                    }
                    session.Status=await db.MeetingSafetyCheckIns.AnyAsync(c=>c.SessionId==id&&c.Id!=item.Id&&c.RespondedAt==null&&c.EscalatedAt!=null,token)?MeetingSafetyStatus.MissedCheckIn:MeetingSafetyStatus.Active;break;
                case "home-safe":await CloseAsync(session,MeetingSafetyStatus.HomeSafe,now,token);break;
                case "end":await CloseAsync(session,MeetingSafetyStatus.Ended,now,token);break;
                case "cancel":await CloseAsync(session,MeetingSafetyStatus.Cancelled,now,token);break;
                case "help":if(await db.MeetingSafetyEvents.AnyAsync(e=>e.SessionId==id&&e.Kind=="help"&&e.At>now.AddMinutes(-5),token))return await SessionViewAsync(session,token);session.Status=MeetingSafetyStatus.HelpRequested;await QueueContactsAsync(session,"help",request.CommandId.ToString(),now,token);break;
            }
        }
        session.UpdatedAt=now;Event(session,request.Action,now,request.CheckInId,request.CommandId);
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return await SessionViewAsync(session,token);
    }
    public async Task CloseAsync(MeetingSafetySession session,MeetingSafetyStatus status,DateTimeOffset now,CancellationToken token)
    {
        if(session.ClosedAt!=null)return;
        session.Status=status;session.ClosedAt=now;session.PurgeAfter=now.AddDays(30);session.UpdatedAt=now;
        await db.MeetingSafetyCheckIns.Where(c=>c.SessionId==session.Id&&c.RespondedAt==null).ExecuteUpdateAsync(s=>s.SetProperty(c=>c.RespondedAt,now).SetProperty(c=>c.Response,"closed"),token);
        foreach(var link in await db.MeetingSafetyLinks.Where(l=>db.MeetingSafetyRecipients.Any(r=>r.Id==l.RecipientId&&r.SessionId==session.Id)).ToListAsync(token))
        {
            if(status==MeetingSafetyStatus.Revoked)link.RevokedAt??=now;
            else if(link.ExpiresAt>now.AddHours(24))link.ExpiresAt=now.AddHours(24);
        }
        await db.MeetingSafetyOutbox.Where(o=>o.SessionId==session.Id&&(o.Status=="pending"||o.Status=="retry")).ExecuteUpdateAsync(s=>s.SetProperty(o=>o.Status,"suppressed").SetProperty(o=>o.CompletedAt,now),token);
        if(status!=MeetingSafetyStatus.Revoked)await QueueContactsAsync(session,"closed",status.ToString(),now,token);
    }
    public void Event(MeetingSafetySession session,string kind,DateTimeOffset now,Guid? checkIn=null,Guid? command=null)=>db.MeetingSafetyEvents.Add(new(){SessionId=session.Id,Kind=kind,At=now,CheckInId=checkIn,CommandId=command});
    public void Queue(Guid member,Guid? contact,Guid? session,Guid? checkIn,string kind,string dedupe,DateTimeOffset now)=>db.MeetingSafetyOutbox.Add(new(){Id=Guid.NewGuid(),MemberId=member,ContactId=contact,SessionId=session,CheckInId=checkIn,Kind=kind,DedupeKey=dedupe,CreatedAt=now,NextAttemptAt=now});
    public async Task QueueContactsAsync(MeetingSafetySession session,string kind,string key,DateTimeOffset now,CancellationToken token,Guid? checkIn=null)
    {
        foreach(var contact in await db.MeetingSafetyRecipients.Where(r=>r.SessionId==session.Id&&r.RevokedAt==null).Select(r=>r.TrustedContactId).ToListAsync(token))
        {var dedupe=kind+":"+session.Id+":"+key+":"+contact;if(!await db.MeetingSafetyOutbox.AnyAsync(o=>o.DedupeKey==dedupe,token))Queue(session.MemberId,contact,session.Id,checkIn,kind,dedupe,now);}
    }
    public TrustedContactView ContactView(TrustedContact contact)
    {
        var destination=Destination(contact);var hint=contact.Channel=="email"?destination[..1]+"•••"+destination[destination.IndexOf('@')..]:"•••"+destination[^3..];
        return new(contact.Id,contact.DisplayName,contact.Channel,hint,contact.Consent.ToString().ToLowerInvariant(),contact.InvitationStatus,contact.CreatedAt);
    }
    public async Task<MeetingSafetySessionView> SessionViewAsync(MeetingSafetySession session,CancellationToken token)
    {
        var contacts=await(from r in db.MeetingSafetyRecipients join c in db.TrustedContacts on r.TrustedContactId equals c.Id where r.SessionId==session.Id select new{r,c}).ToListAsync(token);
        var checks=await db.MeetingSafetyCheckIns.Where(c=>c.SessionId==session.Id).OrderBy(c=>c.DueAt).ToListAsync(token);
        var prompt=MeetingSafetyPolicy.AfterPrompt(session,clock.GetUtcNow());
        return new(session.Id,session.PreferredFirstName,session.StartsAt,session.FinalCheckInAt,session.TimeZoneId,session.VenueOrArea,session.SharePlace,session.MemberNote,MeetingSafetyPolicy.State(session.Status),session.ClosedAt,session.PurgeAfter,session.UpdatedAt,prompt,session.PrivateFeedback,
            checks.Select(c=>new MeetingSafetyCheckInView(c.Id,c.DueAt,c.GraceEndsAt,c.RespondedAt,c.Response,c.EscalatedAt)).ToList(),contacts.Select(x=>new MeetingSafetyRecipientView(x.c.Id,x.c.DisplayName,x.c.Consent.ToString().ToLowerInvariant(),x.r.NotificationStatus,x.r.RevokedAt!=null)).ToList());
    }
    public async Task<object> ExportAsync(Guid member,CancellationToken token)
    {
        var contacts=await db.TrustedContacts.AsNoTracking().Where(c=>c.MemberId==member).OrderBy(c=>c.CreatedAt).ToListAsync(token);
        var sessions=await db.MeetingSafetySessions.AsNoTracking().Where(s=>s.MemberId==member).OrderBy(s=>s.CreatedAt).ToListAsync(token);
        var views=new List<MeetingSafetySessionView>();foreach(var session in sessions)views.Add(await SessionViewAsync(session,token));
        return new{trustedContacts=contacts.Select(c=>new{c.DisplayName,c.Channel,destination=Destination(c),c.Consent,c.CreatedAt,c.ConfirmedAt,c.WithdrawnAt}),sessions=views};
    }
    public async Task<SharedMeetingSafetyView?> SharedAsync(string raw,CancellationToken token)
    {
        if(!MeetingSafetyPolicy.TokenShape(raw))return null;var hash=MeetingSafetyPolicy.TokenHash(raw);
        var member=await(from l in db.MeetingSafetyLinks join r in db.MeetingSafetyRecipients on l.RecipientId equals r.Id join targetSession in db.MeetingSafetySessions on r.SessionId equals targetSession.Id where l.TokenHash==hash select(Guid?)targetSession.MemberId).SingleOrDefaultAsync(token);
        if(member is null)return null;
        await using var authority=await AccountLifecycleLease.AcquireAsync(db,member.Value,token);
        var now=clock.GetUtcNow();
        var row=await(from link in db.MeetingSafetyLinks join recipient in db.MeetingSafetyRecipients on link.RecipientId equals recipient.Id join contact in db.TrustedContacts on recipient.TrustedContactId equals contact.Id join session in db.MeetingSafetySessions on recipient.SessionId equals session.Id
            where link.TokenHash==hash&&link.ExpiresAt>now&&link.RevokedAt==null&&recipient.RevokedAt==null&&contact.Consent==TrustedContactConsent.Confirmed&&contact.ConfirmedAt!=null&&contact.MemberId==session.MemberId&&session.Status!=MeetingSafetyStatus.Revoked select new{link,session}).AsNoTracking().SingleOrDefaultAsync(token);
        if(row is null||!await LiveAsync(row.session.MemberId,token))return null;
        var s=row.session;var next=await db.MeetingSafetyCheckIns.Where(c=>c.SessionId==s.Id&&c.RespondedAt==null).OrderBy(c=>c.DueAt).Select(c=>(DateTimeOffset?)c.DueAt).FirstOrDefaultAsync(token);
        string Display(DateTimeOffset at)=>TimeZoneInfo.ConvertTime(at,TimeZoneInfo.FindSystemTimeZoneById(s.TimeZoneId)).ToString("ddd d MMM yyyy HH:mm zzz",CultureInfo.InvariantCulture)+" ("+s.TimeZoneId+")";
        return new(s.PreferredFirstName,s.StartsAt,s.FinalCheckInAt,next,s.TimeZoneId,s.SharePlace?s.VenueOrArea:null,MeetingSafetyPolicy.State(s.Status),s.UpdatedAt,row.link.ExpiresAt,Display(s.StartsAt),Display(next??s.FinalCheckInAt));
    }
}
