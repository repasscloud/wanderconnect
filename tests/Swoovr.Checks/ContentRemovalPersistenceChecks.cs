using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Moderation;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

// Actual content-removal service checks inside the existing disposable PostgreSQL harness.
// Regressions: source-loss-before-evidence, own/reporter conflict bypass, stale-field removal, duplicate notices.
static class ContentRemovalPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        { ["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)37,32).ToArray()) }).Build();
        var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IHostEnvironment>(new Development());services.AddSingleton<TimeProvider>(TimeProvider.System);services.AddScoped<Swoovr.Api.Retention.SafetyRetention>();services.AddSingleton<IConfiguration>(config);
        services.AddSingleton<IEmailSender>(new Sink());services.AddScoped(_=>new SwoovrDbContext(options));
        services.AddScoped<Founders>();services.AddScoped<ContactHistoryKeys>();services.AddScoped<EvidenceKeys>();
        services.AddScoped<EvidencePreservation>();services.AddScoped<ContentRemovalService>();
        await using var provider=services.BuildServiceProvider();await using var scope=provider.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();var service=scope.ServiceProvider.GetRequiredService<ContentRemovalService>();
        var now=DateTimeOffset.UtcNow.AddMonths(-14);var subject=Guid.NewGuid();var actor=Guid.NewGuid();var reporter=Guid.NewGuid();var review=Guid.NewGuid();var report=Guid.NewGuid();
        Account Account(Guid id,AccountRealm realm)=>new(){Id=id,Issuer="https://issuer.example.test/"+realm,Subject="fictional-"+id,
            Realm=realm,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now,IsRootAdmin=id==actor};
        db.Accounts.AddRange(Account(subject,AccountRealm.Members),Account(reporter,AccountRealm.Members),Account(actor,AccountRealm.Staff));
        db.MemberProfiles.Add(new(){AccountId=subject,PublicId=Guid.NewGuid(),DisplayName="Fictional Content Subject",About="Fictional original profile text",AgeStatus=AgeStatus.Attested});
        db.ModerationCases.Add(new(){Id=review,SubjectAccountId=subject,Status=CaseStatus.Open,Queue=CaseQueue.Admin,CreatedAt=now,UpdatedAt=now});
        var category=await db.ReportCategories.Where(c=>!c.Serious&&c.Priority!=ReportPriority.Urgent).Select(c=>c.Id).FirstAsync();
        db.Reports.Add(new(){Id=report,CaseId=review,ReporterAccountId=reporter,SubjectAccountId=subject,SubjectType=ReportSubject.Profile,
            SubjectRef="fictional-profile",CategoryId=category,CreatedAt=now});await db.SaveChangesAsync();
        ContentRemovalRequest Request(string hash)=>new(Guid.NewGuid(),report,"about",null,null,hash,"Fictional policy review explanation");
        async Task<bool> Refused(Guid staff,ContentRemovalRequest request,int status)
        {try{await service.RemoveAsync(staff,review,request,now,CancellationToken.None);return false;}catch(ContentRemovalRefusal error){return error.Status==status;}}
        var expected=ContentRemovalPolicy.Hash("Fictional original profile text");
        check("ordinary member cannot remove content",await Refused(reporter,Request(expected),403));
        db.StaffMemberLinks.Add(new(){StaffAccountId=actor,MemberAccountId=subject,LinkedBy=actor,LinkedAt=now});await db.SaveChangesAsync();
        check("staff cannot remove its personally associated member content",await Refused(actor,Request(expected),403));
        await db.StaffMemberLinks.Where(l=>l.StaffAccountId==actor).ExecuteDeleteAsync();
        foreach(var item in db.StaffMemberLinks.Local.ToList())db.Entry(item).State=EntityState.Detached;
        db.StaffMemberLinks.Add(new(){StaffAccountId=actor,MemberAccountId=reporter,LinkedBy=actor,LinkedAt=now});await db.SaveChangesAsync();
        check("reporter conflict refuses independent subject removal",await Refused(actor,Request(expected),403));
        await db.StaffMemberLinks.Where(l=>l.StaffAccountId==actor).ExecuteDeleteAsync();
        foreach(var item in db.StaffMemberLinks.Local.ToList())db.Entry(item).State=EntityState.Detached;
        check("stale reviewed profile source is retained",await Refused(actor,Request(ContentRemovalPolicy.Hash("Fictional earlier version")),409)
            &&await db.MemberProfiles.AnyAsync(p=>p.AccountId==subject&&p.About=="Fictional original profile text"));
        check("case source preview returns exact current authorised text and fingerprint",(await service.SourcesAsync(actor,review,default)).Any(x=>x.ReportId==report&&x.Field=="about"&&x.Text=="Fictional original profile text"&&x.Sha256==expected));
        var command=Request(expected);var first=await service.RemoveAsync(actor,review,command,now,CancellationToken.None);
        var again=await service.RemoveAsync(actor,review,command,now.AddSeconds(1),CancellationToken.None);
        check("success clears only targeted core field",await db.MemberProfiles.AnyAsync(p=>p.AccountId==subject&&p.About==null&&p.DisplayName=="Fictional Content Subject"));
        check("retry is idempotent",!first.AlreadyApplied&&again.AlreadyApplied&&await db.Set<ContentRemoval>().CountAsync(r=>r.Id==command.CommandId)==1);
        var retained=await db.Set<ContentRemoval>().SingleAsync(r=>r.Id==command.CommandId);
        var opened=EvidenceSeal.Open(scope.ServiceProvider.GetRequiredService<EvidenceKeys>().Encryption,retained.SealedSnapshot!);
        check("encrypted independent evidence retains exact removed text",opened.Contains("Fictional original profile text")
            &&!Encoding.UTF8.GetString(retained.SealedSnapshot!).Contains("Fictional original profile text"));
        check("generic audit never copies content or staff reason",await db.AuditEntries.AnyAsync(a=>a.Action=="content.removed"&&a.TargetId==command.CommandId.ToString())
            &&!await db.AuditEntries.AnyAsync(a=>a.Detail!=null&&(a.Detail.Contains("Fictional original profile text")||a.Detail.Contains(command.Reason))));
        check("standing notice is committed once",await db.MemberNotices.CountAsync(n=>n.AccountId==subject&&n.Kind==NoticeKind.ContentRemoved)==1);
        check("case evidence pointer contains no original text",await db.CaseEvidence.AnyAsync(e=>e.CaseId==review&&e.Kind=="content-removal")
            &&!await db.CaseEvidence.AnyAsync(e=>e.CaseId==review&&e.Kind=="content-removal"&&e.Snapshot.Contains("Fictional original profile text")));
        check("command cannot be reused for another field",await Refused(actor,command with{Field="mode-about",ModeId="friends"},409));
        check("case-scoped retained evidence opens independently",(await service.EvidenceAsync(actor,review,command.CommandId,default)).Text=="Fictional original profile text");
        var mode=new MemberMode{AccountId=subject,ModeId="friends",Enabled=true,Visible=true,About="Fictional mode text",ShareCoreAbout=true,UpdatedAt=now};db.MemberModes.Add(mode);await db.SaveChangesAsync();
        var modeRequest=new ContentRemovalRequest(Guid.NewGuid(),report,"mode-about","friends",null,ContentRemovalPolicy.Hash(mode.About!),"Fictional mode review");
        await service.RemoveAsync(actor,review,modeRequest,now,default);
        check("per-mode removal disables core fallback without disabling mode",await db.MemberModes.AsNoTracking().AnyAsync(m=>m.AccountId==subject&&m.ModeId=="friends"&&m.About==null&&m.ShareCoreAbout==false&&m.Enabled==true&&m.Visible==true));
        // Report membership is mandatory for a single message; source existence alone is insufficient.
        var conversation=Guid.NewGuid();var message=Guid.NewGuid();
        var pair=ConnectionRequest.Pair(subject,reporter);
        db.Conversations.Add(new(){Id=conversation,PairLow=pair.Low,PairHigh=pair.High,CreatedAt=now,LastMessageAt=now});
        db.Messages.Add(new(){Id=message,ConversationId=conversation,SenderAccountId=subject,Kind=MessageKind.Text,Body="Fictional reported message",SentAt=now});
        await db.SaveChangesAsync();
        var messageCommand=new ContentRemovalRequest(Guid.NewGuid(),report,"message",null,message,ContentRemovalPolicy.Hash("Fictional reported message"),"Fictional message review");
        check("unreported message cannot be removed by existing case authority",await Refused(actor,messageCommand,409));
        var conversationReport=await db.Reports.SingleAsync(r=>r.Id==report);conversationReport.SubjectType=ReportSubject.Conversation;conversationReport.SubjectRef=conversation.ToString();
        db.CaseEvidence.Add(new(){Id=Guid.NewGuid(),CaseId=review,ReportId=report,Kind="conversation",MediaIds=[],CapturedAt=now,
            Snapshot=$"{{\"messages\":[{{\"id\":\"{message}\"}}]}}"});await db.SaveChangesAsync();
        for(var n=0;n<120;n++)db.Messages.Add(new(){Id=Guid.NewGuid(),ConversationId=conversation,SenderAccountId=subject,Kind=MessageKind.Text,Body="Fictional newer message "+n,SentAt=now.AddMinutes(n+1)});
        await db.SaveChangesAsync();
        check("reported older message remains reviewable beyond the latest conversation page",(await service.SourcesAsync(actor,review,default)).Any(x=>x.MessageId==message));
        await service.RemoveAsync(actor,review,messageCommand,now,CancellationToken.None);
        check("reported message gets typed staff marker without a sender-delete assertion",await db.Messages.AnyAsync(m=>m.Id==message&&m.Body==""
            &&m.ModerationRemovedAt!=null&&m.ModerationRemovalId==messageCommand.CommandId&&m.DeletedAt==null));
        var marker=await db.Messages.AsNoTracking().SingleAsync(m=>m.Id==message);
        var view=Swoovr.Api.Connections.MessagingEndpoints.View(marker,reporter,now);
        check("both-member projection distinguishes staff removal from member unsend",view.Kind=="removed-by-moderation"&&view.Text=="Removed by Swoovr."&&!view.CanDelete&&!view.Deleted);
        check("removed message no longer appears as a live content source",!(await service.SourcesAsync(actor,review,default)).Any(x=>x.MessageId==message));

        await db.Messages.Where(m=>m.Id==message).ExecuteDeleteAsync();
        check("removed source deletion cannot erase independent case evidence",(await service.EvidenceAsync(actor,review,messageCommand.CommandId,default)).Text=="Fictional reported message");

        var closed=await db.ModerationCases.SingleAsync(c=>c.Id==review);closed.Status=CaseStatus.Closed;closed.ClosedAt=DateTimeOffset.UtcNow.AddMonths(-13);closed.ClosedBy=actor;await db.SaveChangesAsync();
        var hold=new LegalHold{Id=Guid.NewGuid(),CaseId=review,Status=LegalHoldStatus.Active,PlacedAt=now,ReviewDue=DateTimeOffset.UtcNow.AddDays(30),PlacedByRole=AccountRole.Legal,Reason="Fictional content preservation",RelatedAccountIds=[subject,reporter],RetentionCaseIds=[review]};db.LegalHolds.Add(hold);await db.SaveChangesAsync();
        db.LegalHoldScopes.AddRange(new LegalHoldScope{HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=subject},new LegalHoldScope{HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=reporter});await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<EvidencePreservation>().CaptureAsync(hold,actor,now,default);
        check("a later hold captures independent removed evidence after the message source disappeared",await db.EvidenceItems.AnyAsync(e=>e.HoldId==hold.Id&&e.Kind=="content-removal"&&e.RecordId==messageCommand.CommandId.ToString()));
        var retention=new Swoovr.Api.Retention.SafetyRetention(db,scope.ServiceProvider.GetRequiredService<EvidencePreservation>());
        await retention.PurgeAsync(DateTimeOffset.UtcNow,default,onlyCase:review);
        check("active hold preserves expired original content through the actual retention service",await db.Set<ContentRemoval>().AsNoTracking().AnyAsync(r=>r.Id==command.CommandId&&r.SealedSnapshot!=null));
        hold.Status=LegalHoldStatus.Released;hold.ReleasedAt=DateTimeOffset.UtcNow.AddMonths(-13);hold.ReleasedBy=actor;hold.ReleaseReason="Fictional lawful release";hold.NoReviewRetentionConfirmedAt=hold.ReleasedAt;hold.NoReviewRetentionConfirmedBy=actor;await db.SaveChangesAsync();
        await retention.PurgeAsync(DateTimeOffset.UtcNow,default,onlyCase:review);
        check("actual retention erases expired encrypted content and its fingerprint atomically",await db.Set<ContentRemoval>().AsNoTracking().AllAsync(r=>r.CaseId!=review||r.SealedSnapshot==null&&r.SnapshotHash.Length==0&&r.EvidenceDisposedAt!=null));
        check("content disposal keeps minimal decision records on their later clock",await db.Set<ContentRemoval>().CountAsync(r=>r.CaseId==review)==3&&await db.ModerationCases.AnyAsync(c=>c.Id==review));

    }
    private sealed class Development:IHostEnvironment
    {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Sink:IEmailSender
    {public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new Exception("No external notice delivery belongs in the removal transaction.");}
}
