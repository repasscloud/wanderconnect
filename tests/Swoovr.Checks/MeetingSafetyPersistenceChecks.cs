using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Swoovr.Api;
using Swoovr.Api.Accounts;
using Swoovr.Api.Safety;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;
static class MeetingSafetyPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(options);var sourceConnection=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(sourceConnection);await admin.OpenAsync();var name="swoovr_meeting_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(sourceConnection){Database=name,Pooling=false}.ConnectionString;
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options);await db.Database.MigrateAsync();
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:Swoovr",connection},{"ContactHistory:Keys:1",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))},{"ContactHistory:LookupKey",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))},{"MeetingSafety:PublicBaseUrl","http://localhost:5081/"}}).Build();
            var sink=new Sink();var clock=new Clock();using var services=new ServiceCollection().AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(new Development()).AddSingleton<TimeProvider>(clock).AddSingleton(db).AddSingleton<IEmailSender>(sink).AddLogging().AddScoped<Founders>().AddSwoovrProviders(config).BuildServiceProvider();
            using var scope=services.CreateScope();var store=scope.ServiceProvider.GetRequiredService<MeetingSafetyStore>();var schedule=scope.ServiceProvider.GetRequiredService<MeetingSafetySchedule>();var delivery=scope.ServiceProvider.GetRequiredService<MeetingSafetyDelivery>();
            var member=Guid.NewGuid();var other=Guid.NewGuid();var now=clock.Now;
            foreach(var id in new[]{member,other})db.Accounts.Add(new(){Id=id,Issuer="http://localhost/fictional-members",Subject=id.ToString(),Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});await db.SaveChangesAsync();
            var first=await store.AddContactAsync(member,new("Fictional trusted adult A","email","trusted-a@example.test","Fictional"),default);
            var second=await store.AddContactAsync(member,new("Fictional trusted adult B","email","trusted-b@example.test","Fictional"),default);
            check("trusted contacts start unconfirmed",first.Consent=="pending"&&second.Consent=="pending");
            check("unconfirmed contact cannot start safety monitoring",await Refused(()=>store.CreateAsync(member,new("Fictional",now.AddMinutes(1),now.AddHours(2),"Australia/Adelaide",null,false,null,[first.Id]),default)));
            await delivery.RunAsync(default);
            var firstBody=sink.Messages.Single(m=>m.To=="trusted-a@example.test").Text;var secondBody=sink.Messages.Single(m=>m.To=="trusted-b@example.test").Text;
            string Consent(string body)=>Regex.Match(body,@"/safety/contact/([A-Za-z0-9_-]{43})").Groups[1].Value;
            var firstConsent=Consent(firstBody);var secondConsent=Consent(secondBody);
            var stored=await db.TrustedContacts.AsNoTracking().SingleAsync(c=>c.Id==first.Id);
            check("invitation persists only token digest",firstConsent.Length==43&&stored.ConsentTokenHash!.SequenceEqual(MeetingSafetyPolicy.TokenHash(firstConsent))&&!Encoding.UTF8.GetString(stored.ConsentTokenHash!).Contains(firstConsent));
            check("destination is encrypted with a purpose-separated lookup",!Encoding.UTF8.GetString(stored.DestinationEncrypted).Contains("trusted-a@example.test")&&stored.DestinationLookupHash.Length==32);
            check("contact opt in succeeds once",(await store.ConsentAsync(firstConsent,"confirm",default)).Available);
            check("confirmation link is single use",!(await store.ConsentAsync(firstConsent,"confirm",default)).Available);
            check("second external contact independently opts in",(await store.ConsentAsync(secondConsent,"confirm",default)).Available);
            check("another member cannot select this member's contacts",await Refused(()=>store.CreateAsync(other,new("Fictional",now.AddMinutes(1),now.AddHours(2),"Australia/Adelaide",null,false,null,[first.Id]),default)));
            var plan=await store.CreateAsync(member,new("Fictional",now.AddMinutes(1),now.AddHours(2),"Australia/Adelaide","Fictional public cafe",false,"Private note: never shared",[first.Id,second.Id],[now.AddMinutes(2)]),default);
            check("future check-in cannot be acknowledged early",await Refused(()=>store.ActAsync(member,plan.Id,new(Guid.NewGuid(),"ok",plan.CheckIns.First().Id),default)));
            await delivery.RunAsync(default);
            var shared=sink.Messages.Where(m=>m.Text.Contains("/safety/meeting/")).ToList();
            string Link(EmailMessage message)=>Regex.Match(message.Text,@"/safety/meeting/([A-Za-z0-9_-]{43})").Groups[1].Value;
            var firstLink=Link(shared.Single(m=>m.To=="trusted-a@example.test"));var secondLink=Link(shared.Single(m=>m.To=="trusted-b@example.test"));
            check("shared tokens are independent random per-recipient capabilities",firstLink.Length==43&&secondLink.Length==43&&firstLink!=secondLink);
            check("shared summary omits unapproved place and private note",(await store.SharedAsync(firstLink,default)) is{VenueOrArea:null} summary&&summary.TimeZoneId=="Australia/Adelaide"&&!shared.Any(m=>m.Text.Contains("Private note")||m.Text.Contains("Fictional public cafe")));
            check("shared summary uses the chosen zone and visible offset",(await store.SharedAsync(firstLink,default))!.StartDisplay.Contains("Australia/Adelaide")&&(await store.SharedAsync(firstLink,default))!.StartDisplay.Contains(TimeZoneInfo.ConvertTime(now.AddMinutes(1),TimeZoneInfo.FindSystemTimeZoneById("Australia/Adelaide")).ToString("zzz")));
            var cancelled=await store.CreateAsync(member,new("Fictional",now.AddMinutes(1),now.AddHours(2),"UTC",null,false,null,[first.Id]),default);
            var cancelledView=await store.ActAsync(member,cancelled.Id,new(Guid.NewGuid(),"cancel"),default);await schedule.RunAsync(default);
            check("cancellation closes monitoring and suppresses the queued plan",cancelledView.Status=="cancelled"&&await db.MeetingSafetyOutbox.AnyAsync(o=>o.SessionId==cancelled.Id&&o.Kind=="planned"&&o.Status=="suppressed")&&!await db.MeetingSafetyOutbox.AnyAsync(o=>o.SessionId==cancelled.Id&&o.Kind=="missed"));
            clock.Now=now.AddMinutes(2);await schedule.RunAsync(default);await schedule.RunAsync(default);
            var early=plan.CheckIns.First();check("due check in queues one app prompt",await db.MeetingSafetyOutbox.CountAsync(o=>o.CheckInId==early.Id&&o.Kind=="prompt")==1);
            clock.Now=now.AddMinutes(31);await schedule.RunAsync(default);check("grace period does not escalate early",!await db.MeetingSafetyOutbox.AnyAsync(o=>o.Kind=="missed"));
            await store.ActAsync(member,plan.Id,new(Guid.NewGuid(),"help"),default);
            clock.Now=now.AddMinutes(32);await schedule.RunAsync(default);await schedule.RunAsync(default);
            check("missed check-in never downgrades an unresolved explicit help request",(await store.SessionViewAsync(await db.MeetingSafetySessions.SingleAsync(s=>s.Id==plan.Id),default)).Status=="help_requested");
            check("server escalation queues each selected contact once",await db.MeetingSafetyOutbox.CountAsync(o=>o.Kind=="missed")==2);
            await delivery.RunAsync(default);var count=sink.Messages.Count;await delivery.RunAsync(default);check("accepted escalation retries do not duplicate sends",sink.Messages.Count==count);
            check("missed check in is labelled unconfirmed rather than emergency proof",sink.Messages.Any(m=>m.Text.Contains("missed/unconfirmed")&&m.Text.Contains("not proof of an emergency")));
            clock.Now=now.AddMinutes(33);var response=await store.ActAsync(member,plan.Id,new(Guid.NewGuid(),"ok",early.Id),default);
            check("late member check in retains escalation history",response.CheckIns.First(c=>c.Id==early.Id) is{RespondedAt:not null,EscalatedAt:not null});
            var final=response.CheckIns.Last();var command=Guid.NewGuid();var extended=await store.ActAsync(member,plan.Id,new(command,"extend",final.Id),default);var repeated=await store.ActAsync(member,plan.Id,new(command,"extend",final.Id),default);
            check("extension command is idempotent and creates a new deadline",extended.FinalCheckInAt==now.AddHours(2).AddMinutes(30)&&repeated.CheckIns.Count==extended.CheckIns.Count);
            await store.RemoveContactAsync(member,first.Id,default);
            check("one contact revocation immediately invalidates only its links",await store.SharedAsync(firstLink,default)==null&&await store.SharedAsync(secondLink,default)!=null);
            var home=await store.ActAsync(member,plan.Id,new(Guid.NewGuid(),"home-safe"),default);
            check("home safe closes separately from ordinary record deletion",home.Status=="home_safe"&&home.ClosedAt==clock.Now&&home.PurgeAfter==clock.Now.AddDays(30));
            check("closure bounds existing shared pages to24 hours",(await store.SharedAsync(secondLink,default))!.LinkExpiresAt<=clock.Now.AddHours(24));
            var closedCount=await db.MeetingSafetyOutbox.CountAsync(o=>o.Kind=="closed");await store.ActAsync(member,plan.Id,new(Guid.NewGuid(),"home-safe"),default);
            check("repeated terminal action adds no duplicate notifications",await db.MeetingSafetyOutbox.CountAsync(o=>o.Kind=="closed")==closedCount);
            var lateRace=await store.CreateAsync(member,new("Fictional late race",clock.Now,clock.Now.AddMinutes(1),"UTC",null,false,null,[second.Id]),default);
            clock.Now=clock.Now.AddMinutes(31);await schedule.RunAsync(default);
            await store.ActAsync(member,lateRace.Id,new(Guid.NewGuid(),"ok",lateRace.CheckIns.Single().Id),default);await delivery.RunAsync(default);
            check("late acknowledgement suppresses a queued contact miss before provider effects",await db.MeetingSafetyOutbox.AnyAsync(o=>o.SessionId==lateRace.Id&&o.Kind=="missed"&&o.CheckInId==lateRace.CheckIns.Single().Id&&o.Status=="suppressed")&&!sink.Messages.Any(m=>m.Text.StartsWith("Fictional late race: A check-in")));
            await store.ActAsync(member,lateRace.Id,new(Guid.NewGuid(),"home-safe"),default);
            var afterHelp=await store.CreateAsync(member,new("Fictional after help",clock.Now,clock.Now.AddMinutes(1),"UTC",null,false,null,[second.Id]),default);
            var afterDismiss=await store.CreateAsync(member,new("Fictional after dismiss",clock.Now,clock.Now.AddMinutes(1),"UTC",null,false,null,[second.Id]),default);
            clock.Now=clock.Now.AddMinutes(1);
            foreach(var candidate in new[]{afterHelp,afterDismiss})await store.ActAsync(member,candidate.Id,new(Guid.NewGuid(),"ok",candidate.CheckIns.Single().Id),default);
            clock.Now=clock.Now.AddHours(2);await schedule.RunAsync(default);
            check("after-prompt race fixtures were genuinely queued",await db.MeetingSafetyOutbox.CountAsync(o=>(o.SessionId==afterHelp.Id||o.SessionId==afterDismiss.Id)&&o.Kind=="after"&&o.Status=="pending")==2);
            await store.ActAsync(member,afterHelp.Id,new(Guid.NewGuid(),"help"),default);await store.ActAsync(member,afterDismiss.Id,new(Guid.NewGuid(),"dismiss"),default);await delivery.RunAsync(default);
            check("queued after-prompt is suppressed after explicit help or dismissal",await db.MeetingSafetyOutbox.CountAsync(o=>(o.SessionId==afterHelp.Id||o.SessionId==afterDismiss.Id)&&o.Kind=="after"&&o.Status=="suppressed")==2);
            foreach(var candidate in new[]{afterHelp,afterDismiss})await store.ActAsync(member,candidate.Id,new(Guid.NewGuid(),"home-safe"),default);
            var expiryRace=await store.CreateAsync(member,new("Fictional expiry race",clock.Now,clock.Now.AddMinutes(1),"UTC",null,false,null,[second.Id]),default);
            clock.Now=expiryRace.FinalCheckInAt.AddHours(24);
            var expiredRace=await store.ActAsync(member,expiryRace.Id,new(Guid.NewGuid(),"extend",expiryRace.CheckIns.Single().Id),default);
            check("delayed scheduler cannot let an extension revive expired monitoring",expiredRace.Status=="expired"&&expiredRace.FinalCheckInAt==expiryRace.FinalCheckInAt&&expiredRace.ClosedAt==clock.Now);
            clock.Now=clock.Now.AddHours(25);check("expired shared token reveals no summary",await store.SharedAsync(secondLink,default)==null);
            var management=Regex.Matches(secondBody,@"/safety/contact/([A-Za-z0-9_-]{43})")[1].Groups[1].Value;
            check("contact can withdraw consent after a shared page expires",(await store.ConsentAsync(management,"withdraw",default)).Available);
            check("withdrawal does not reopen the expired shared summary",await store.SharedAsync(secondLink,default)==null);
            var third=await store.AddContactAsync(member,new("Fictional unknown outcome","email","trusted-c@example.test","Fictional"),default);sink.Outcome="unknown";await delivery.RunAsync(default);count=sink.Messages.Count;await delivery.RunAsync(default);
            check("uncertain provider acceptance is not blindly retried",sink.Messages.Count==count&&(await db.TrustedContacts.AsNoTracking().SingleAsync(c=>c.Id==third.Id)).InvitationStatus=="unknown");
            // Saturated contact budget must refuse before another provider side effect.
            var limited=await db.TrustedContacts.SingleAsync(c=>c.Id==third.Id);
            for(var n=0;n<10;n++)db.MeetingSafetyOutbox.Add(new(){Id=Guid.NewGuid(),MemberId=member,ContactId=limited.Id,Kind="invite",Status="accepted",DedupeKey="quota-fixture-"+n,CreatedAt=clock.Now,AttemptedAt=clock.Now,CompletedAt=clock.Now,NextAttemptAt=clock.Now});
            var capped=new MeetingSafetyOutbox{Id=Guid.NewGuid(),MemberId=member,ContactId=limited.Id,Kind="invite",Status="pending",DedupeKey="quota-refused-fixture",CreatedAt=clock.Now,NextAttemptAt=clock.Now};db.MeetingSafetyOutbox.Add(capped);await db.SaveChangesAsync();count=sink.Messages.Count;await delivery.RunAsync(default);
            check("contact send budget refuses before provider call and exposes rate limit",sink.Messages.Count==count&&(await db.MeetingSafetyOutbox.AsNoTracking().SingleAsync(o=>o.Id==capped.Id)).Status=="rate-limited");
            clock.Now=home.ClosedAt!.Value.AddDays(32);
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,PlacedAt=now,ReviewDue=now.AddDays(30),Reason="Fictional meeting retention",PlacedByRole=AccountRole.Legal,RelatedAccountIds=[member],RetentionCaseIds=[]};db.LegalHolds.Add(hold);await db.SaveChangesAsync();
            var retention=scope.ServiceProvider.GetRequiredService<MeetingSafetyRetention>();
            check("meeting ordinary records are held beyond their30-day purge date",await retention.RunAsync(clock.Now,default)&&await db.MeetingSafetySessions.AnyAsync(s=>s.Id==plan.Id));
            hold.Status=LegalHoldStatus.Released;hold.ReleasedAt=clock.Now.AddDays(-1);hold.ReleasedBy=other;hold.NoReviewRetentionConfirmedAt=clock.Now.AddDays(-1);hold.NoReviewRetentionConfirmedBy=other;await db.SaveChangesAsync();
            config["Evidence:HoldCheckUnavailable"]="true";
            check("unknown hold status refuses meeting record disposal",!await retention.RunAsync(clock.Now,default)&&await db.MeetingSafetySessions.AnyAsync(s=>s.Id==plan.Id));config["Evidence:HoldCheckUnavailable"]="false";
            var incidentCase=new ModerationCase{Id=Guid.NewGuid(),SubjectAccountId=other,Status=CaseStatus.Open,Queue=CaseQueue.Admin,CreatedAt=now,UpdatedAt=now};
            var incident=new Report{Id=Guid.NewGuid(),ReporterAccountId=member,SubjectAccountId=other,SubjectType=ReportSubject.Profile,SubjectRef=other.ToString(),CategoryId="other",CaseId=incidentCase.Id,CreatedAt=now,MeetingSafetySessionId=plan.Id};db.ModerationCases.Add(incidentCase);db.Reports.Add(incident);await db.SaveChangesAsync();
            check("linked incident provenance independently retains a meeting",await retention.RunAsync(clock.Now,default)&&await db.MeetingSafetySessions.AnyAsync(s=>s.Id==plan.Id));
            // Remove only this owned synthetic report/case fixture; this does not assert lawful case disposal.
            db.Reports.Remove(incident);db.ModerationCases.Remove(incidentCase);await db.SaveChangesAsync();
            check("released eligible meeting records expire after their independent schedule",await retention.RunAsync(clock.Now,default)&&!await db.MeetingSafetySessions.AnyAsync(s=>s.Id==plan.Id));
            check("meeting purge also removes old recipient capabilities",!await db.MeetingSafetyLinks.AnyAsync());
            check("outbox persists no bodies or bearer-token fields",db.Model.FindEntityType(typeof(MeetingSafetyOutbox))!.GetProperties().All(p=>!p.Name.Contains("Token")&&!p.Name.Contains("Body")&&!p.Name.Contains("Destination")));
            check("no other-participant identity or coordinates are part of shared contract",typeof(SharedMeetingSafetyView).GetProperties().All(p=>!p.Name.Contains("Latitude")&&!p.Name.Contains("Longitude")&&!p.Name.Contains("Participant")&&!p.Name.Contains("Photo")&&!p.Name.Contains("Message")));
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    static async Task<bool> Refused(Func<Task<MeetingSafetySessionView>> action){try{await action();return false;}catch(MeetingSafetyRefusal){return true;}}
    sealed class Sink:IEmailSender
    {
        public List<EmailMessage> Messages{get;}=[];public string Outcome="accepted";
        public Task<bool> SendAsync(EmailMessage message,CancellationToken token){Messages.Add(message);return Task.FromResult(Outcome=="accepted");}
        public Task<EmailOutcome> SendOutcomeAsync(EmailMessage message,CancellationToken token){Messages.Add(message);return Task.FromResult(new EmailOutcome(Outcome));}
    }
    sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    sealed class Development:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=Directory.GetCurrentDirectory();public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
