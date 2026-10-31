using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Moderation;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

// Actual queue/processor/review services inside the existing disposable PostgreSQL harness.
// All accounts/text are fictional. These checks do not claim authentication, classifier accuracy or legal approval.
static class TextHarmRuntimeChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        var clock=new Clock();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Evidence:Keys:1"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["ContactHistory:Keys:1"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["ContactHistory:LookupKey"]=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        }).Build();
        await using var provider=Provider(options,config,clock);
        await using var scope=provider.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
        var queue=scope.ServiceProvider.GetRequiredService<TextHarmQueue>();
        var processor=scope.ServiceProvider.GetRequiredService<TextHarmProcessor>();
        var reviews=scope.ServiceProvider.GetRequiredService<TextHarmCandidateReviewService>();
        var detector=scope.ServiceProvider.GetRequiredService<RecordingDetector>();
        var keys=scope.ServiceProvider.GetRequiredService<EvidenceKeys>();

        async Task<Guid> Member()
        {
            var id=Guid.NewGuid();
            db.Accounts.Add(new(){Id=id,Issuer="https://issuer.example.test/runtime",Subject="fictional-"+id,Realm=AccountRealm.Members,
                Status=AccountStatus.Active,CreatedAt=clock.Now.AddDays(-1),LastSeenAt=clock.Now});
            db.MemberProfiles.Add(new(){AccountId=id,PublicId=Guid.NewGuid(),DisplayName="Fictional runtime adult",AgeStatus=AgeStatus.Attested});
            await db.SaveChangesAsync();return id;
        }
        async Task<Guid> Staff(AccountRole role)
        {
            var id=Guid.NewGuid();
            db.Accounts.Add(new(){Id=id,Issuer="https://issuer.example.test/runtime-staff",Subject="fictional-"+id,Realm=AccountRealm.Staff,
                Status=AccountStatus.Active,CreatedAt=clock.Now.AddDays(-1),LastSeenAt=clock.Now});
            db.RoleGrants.Add(new(){Id=Guid.NewGuid(),AccountId=id,Role=role,GrantedAt=clock.Now});await db.SaveChangesAsync();return id;
        }
        async Task<Guid> ProfileIntent(Guid member,string text,string? mode=null)
        {
            var source=mode is null?TextHarmSource.ProfileAbout:TextHarmSource.ModeAbout;
            var revision=queue.Enqueue(member,source,member,mode,text,clock.Now);
            if(mode is null)
            {
                var profile=await db.MemberProfiles.SingleAsync(p=>p.AccountId==member);profile.About=text;profile.TextRevision=revision;
            }
            else
            {
                var row=await db.MemberModes.SingleOrDefaultAsync(m=>m.AccountId==member&&m.ModeId==mode);
                if(row is null){row=new(){AccountId=member,ModeId=mode,Enabled=true,Visible=true,UpdatedAt=clock.Now};db.MemberModes.Add(row);}
                row.About=text;row.TextRevision=revision;row.UpdatedAt=clock.Now;
            }
            await db.SaveChangesAsync();
            return await db.Set<TextHarmIntent>().Where(i=>i.AccountId==member&&i.Revision==revision&&i.Source==source).Select(i=>i.Id).SingleAsync();
        }
        async Task<Guid> Conversation(Guid sender,Guid peer)
        {
            var pair=ConnectionRequest.Pair(sender,peer);var id=Guid.NewGuid();
            db.Conversations.Add(new(){Id=id,PairLow=pair.Low,PairHigh=pair.High,CreatedAt=clock.Now.AddDays(-1),LastMessageAt=clock.Now});
            await db.SaveChangesAsync();return id;
        }
        Message Message(Guid sender,Guid conversation,string text,DateTimeOffset at,Guid? id=null)=>new()
        {Id=id??Guid.NewGuid(),ConversationId=conversation,SenderAccountId=sender,Kind=MessageKind.Text,Body=text,SentAt=at};
        async Task<Guid> MessageIntent(Message message)
        {
            db.Messages.Add(message);queue.Enqueue(message.SenderAccountId,TextHarmSource.Message,message.Id,null,message.Body,clock.Now);
            await db.SaveChangesAsync();return await db.Set<TextHarmIntent>().Where(i=>i.SourceId==message.Id).Select(i=>i.Id).SingleAsync();
        }
        async Task<bool> ReadRefused(Guid actor,Guid caseId,Guid id,int status)
        {try{await reviews.ReadAsync(actor,caseId,id,default);return false;}catch(TextHarmReviewRefusal refusal){return refusal.Status==status;}}
        async Task<bool> MarkRefused(Guid actor,Guid caseId,Guid id,string outcome,Guid? sanction,Guid? removal,int status)
        {try{await reviews.MarkAsync(actor,caseId,id,outcome,sanction,removal,default);return false;}catch(TextHarmReviewRefusal refusal){return refusal.Status==status;}}
        var admin=await Staff(AccountRole.Admin);var support=await Staff(AccountRole.Support);var legal=await Staff(AccountRole.Legal);
        var subjectStaff=await Staff(AccountRole.Admin);var historicalReporterStaff=await Staff(AccountRole.Admin);
        var subject=await Member();var reporter=await Member();
        const string quotation="Fictional scam-awareness lesson quotes send me money as a warning, not a request.";
        var intent=await ProfileIntent(subject,quotation);
        var result=await processor.ProcessAsync(intent,default);
        check("actual processor creates a human-review candidate for the bounded combination",result.Result=="created"&&result.ReviewId!=null&&result.CaseId!=null&&!result.Urgent);
        var reviewId=result.ReviewId!.Value;var caseId=result.CaseId!.Value;
        var candidate=await db.Set<TextHarmReview>().AsNoTracking().SingleAsync(r=>r.Id==reviewId);
        var snapshot=JsonSerializer.Deserialize<TextHarmSnapshot>(EvidenceSeal.Open(keys.Encryption,candidate.SealedEvidence!))!;
        check("candidate evidence is sealed with exact committed source provenance",snapshot.Text==quotation&&snapshot.SourceId==subject&&snapshot.Revision.ToString("D")==candidate.SourceRevision
            &&snapshot.Detector==BoundedTextHarmDetector.Detector&&!Encoding.UTF8.GetString(candidate.SealedEvidence!).Contains(quotation));
        check("ordinary candidate creates support case without fabricated report or automatic sanction/notice",await db.ModerationCases.AnyAsync(c=>c.Id==caseId&&c.Queue==CaseQueue.Support&&c.Priority==ReportPriority.Normal)
            &&!await db.Reports.AnyAsync(r=>r.CaseId==caseId)&&!await db.Sanctions.AnyAsync(s=>s.AccountId==subject)&&!await db.MemberNotices.AnyAsync(n=>n.AccountId==subject));
        var replay=await processor.ProcessAsync(intent,default);
        check("same durable intent replay deduplicates candidate and case",replay.Result=="already-recorded"&&replay.ReviewId==reviewId&&replay.CaseId==caseId
            &&await db.Set<TextHarmReview>().CountAsync(r=>r.IntentId==intent)==1&&await db.ModerationCases.CountAsync(c=>c.SubjectAccountId==subject)==1);
        check("completed intent clears its private text after candidate capture",await db.Set<TextHarmIntent>().AnyAsync(i=>i.Id==intent&&i.ProcessedAt!=null&&i.SealedText==null&&i.Result=="candidate"));

        var staleSubject=await Member();var stale=await ProfileIntent(staleSubject,"Fictional send me money earlier revision");
        var replaced=await db.MemberProfiles.SingleAsync(p=>p.AccountId==staleSubject);replaced.About="Fictional harmless replacement";replaced.TextRevision=Guid.NewGuid();await db.SaveChangesAsync();
        check("replaced core revision is skipped rather than assessing new text under old authority",(await processor.ProcessAsync(stale,default)).Result=="stale"&&!await db.Set<TextHarmReview>().AnyAsync(r=>r.SubjectAccountId==staleSubject));
        var modeSubject=await Member();var modeIntent=await ProfileIntent(modeSubject,"Fictional send me money friends-only source","friends");
        var otherMode=new MemberMode{AccountId=modeSubject,ModeId="dating",Enabled=true,Visible=true,About="Fictional unrelated dating source",TextRevision=Guid.NewGuid(),UpdatedAt=clock.Now};
        db.MemberModes.Add(otherMode);await db.SaveChangesAsync();
        var modeResult=await processor.ProcessAsync(modeIntent,default);
        check("unrelated mode revision cannot stale the friends candidate",modeResult.Result=="created"&&await db.Set<TextHarmReview>().AnyAsync(r=>r.Id==modeResult.ReviewId&&r.ModeId=="friends")
            &&await db.MemberModes.AnyAsync(m=>m.AccountId==modeSubject&&m.ModeId=="dating"&&m.About==otherMode.About));
        var staleMode=await ProfileIntent(modeSubject,"Fictional send me money old friends source","friends");
        var friends=await db.MemberModes.SingleAsync(m=>m.AccountId==modeSubject&&m.ModeId=="friends");friends.TextRevision=Guid.NewGuid();await db.SaveChangesAsync();
        check("same-mode revision replacement is stale even when body matches",(await processor.ProcessAsync(staleMode,default)).Result=="stale");
        var unsentSubject=await Member();var unsentPeer=await Member();var unsentConversation=await Conversation(unsentSubject,unsentPeer);
        var unsent=Message(unsentSubject,unsentConversation,"Fictional send me money unsent message",clock.Now);
        var unsentIntent=await MessageIntent(unsent);unsent.Body="";unsent.DeletedAt=clock.Now;await db.SaveChangesAsync();
        check("member-unsent source is skipped before candidate creation",(await processor.ProcessAsync(unsentIntent,default)).Result=="stale"&&!await db.Set<TextHarmReview>().AnyAsync(r=>r.SubjectAccountId==unsentSubject));

        // Actual server history: no supplied client counts. UUID tie order, future/old rows and current count are checked.
        var repeatedSubject=await Member();var peers=new[]{await Member(),await Member(),await Member(),await Member()};
        var conversations=new[]{await Conversation(repeatedSubject,peers[0]),await Conversation(repeatedSubject,peers[1]),await Conversation(repeatedSubject,peers[2]),await Conversation(repeatedSubject,peers[3])};
        const string repeatedText="Fictional repeated invitation to the public reading group";
        var prefix=Guid.NewGuid().ToString("N")[..24];
        var tieBefore=Guid.ParseExact(prefix+"00000001","N");var currentId=Guid.ParseExact(prefix+"00000002","N");var tieAfter=Guid.ParseExact(prefix+"00000003","N");
        db.Messages.AddRange(Message(repeatedSubject,conversations[0],repeatedText,clock.Now.AddMinutes(-30)),
            Message(repeatedSubject,conversations[1],repeatedText.ToUpperInvariant(),clock.Now.AddMinutes(-20)),
            Message(repeatedSubject,conversations[0],"  "+repeatedText+"  ",clock.Now.AddMinutes(-10)),
            Message(repeatedSubject,conversations[1],repeatedText,clock.Now,tieBefore),
            Message(repeatedSubject,conversations[3],repeatedText,clock.Now,tieAfter),
            Message(repeatedSubject,conversations[3],repeatedText,clock.Now.AddSeconds(1)),
            Message(repeatedSubject,conversations[3],repeatedText,clock.Now.AddHours(-1).AddSeconds(-1)));
        await db.SaveChangesAsync();
        var repeatedIntent=await MessageIntent(Message(repeatedSubject,conversations[2],repeatedText,clock.Now,currentId));
        var repeatedResult=await processor.ProcessAsync(repeatedIntent,default);
        var history=detector.Inputs.Last(i=>i.AccountId==repeatedSubject).History;
        check("PostgreSQL history includes current exactly once and excludes newer/tie-after/old rows",history.Complete&&history.SameTextMessagesLastHour==4&&history.DistinctRecipientsLastHour==3);
        check("real normalized repeated-history threshold produces a candidate",repeatedResult.Result=="created"&&await db.Set<TextHarmReview>().AnyAsync(r=>r.Id==repeatedResult.ReviewId&&r.Rules.Contains("repetitive-message-threshold")));
        // Own-initiative messages have no Reporter. Their actual peer is nevertheless personally implicated,
        // and the restricted candidate's retained related-account provenance must survive source deletion.
        var peerStaff=await Staff(AccountRole.Admin);
        db.StaffMemberLinks.Add(new(){StaffAccountId=peerStaff,MemberAccountId=peers[2],LinkedBy=admin,LinkedAt=clock.Now});
        await db.SaveChangesAsync();
        var peerReview=repeatedResult.ReviewId!.Value;var peerCase=repeatedResult.CaseId!.Value;
        var peerCandidate=await db.Set<TextHarmReview>().AsNoTracking().SingleAsync(r=>r.Id==peerReview);
        check("own-initiative message retains exactly subject and actual current peer without fabricated reporter",peerCandidate.RelatedAccountIds.Length==2
            &&peerCandidate.RelatedAccountIds.Contains(repeatedSubject)&&peerCandidate.RelatedAccountIds.Contains(peers[2])
            &&!peerCandidate.RelatedAccountIds.Contains(peers[0])&&!await db.Reports.AnyAsync(r=>r.CaseId==peerCase));
        check("actual peer-associated staff cannot read or mark an own-initiative message candidate",await ReadRefused(peerStaff,peerCase,peerReview,403)
            &&await MarkRefused(peerStaff,peerCase,peerReview,"reviewed-no-action",null,null,403));
        await db.Messages.Where(m=>m.Id==currentId).ExecuteDeleteAsync();
        var retainedPeople=await SafetyRetention.CaseAccountsAsync(db,peerCase,default);
        check("candidate retains implicated peer provenance after original message source is gone",!await db.Messages.AnyAsync(m=>m.Id==currentId)
            &&retainedPeople.Contains(repeatedSubject)&&retainedPeople.Contains(peers[2]));
        check("source deletion cannot restore personally implicated peer staff review authority",await ReadRefused(peerStaff,peerCase,peerReview,403)
            &&await MarkRefused(peerStaff,peerCase,peerReview,"reviewed-no-action",null,null,403));
        var cappedSubject=await Member();var cappedPeer=await Member();var cappedConversation=await Conversation(cappedSubject,cappedPeer);
        for(var n=0;n<501;n++)db.Messages.Add(Message(cappedSubject,cappedConversation,"Fictional bounded-history row "+n,clock.Now.AddMinutes(-30).AddMilliseconds(n)));
        await db.SaveChangesAsync();
        var cappedIntent=await MessageIntent(Message(cappedSubject,cappedConversation,"Fictional current message with no direct trigger",clock.Now));
        var cappedResult=await processor.ProcessAsync(cappedIntent,default);var cappedHistory=detector.Inputs.Last(i=>i.AccountId==cappedSubject).History;
        check("501 actual prior records mark history incomplete instead of inventing zero-risk complete counts",!cappedHistory.Complete&&cappedHistory.SameTextMessagesLastHour==0&&cappedHistory.DistinctRecipientsLastHour==0&&cappedResult.Result=="no-signal");

        var unavailableSubject=await Member();var unavailableIntent=await ProfileIntent(unavailableSubject,"Fictional send me money pending keys");
        var noKeyConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()).Build();
        await using(var noKeys=Provider(options,noKeyConfig,clock))
        await using(var noKeyScope=noKeys.CreateAsyncScope())
        {
            var refused=false;try{await noKeyScope.ServiceProvider.GetRequiredService<TextHarmProcessor>().ProcessAsync(unavailableIntent,default);}catch(InvalidOperationException){refused=true;}
            await using var observed=new SwoovrDbContext(options);
            check("missing decryption authority leaves the durable ciphertext pending",refused&&await observed.Set<TextHarmIntent>().AnyAsync(i=>i.Id==unavailableIntent&&i.ProcessedAt==null&&i.Result==null&&i.SealedText!=null)
                &&!await observed.Set<TextHarmReview>().AnyAsync(r=>r.IntentId==unavailableIntent));
        }

        // Add a distinct fictional member-report fixture AFTER proving the own-initiative candidate fabricated none.
        var reportId=Guid.NewGuid();var category=await db.ReportCategories.Where(c=>!c.Serious&&c.Priority!=ReportPriority.Urgent).Select(c=>c.Id).FirstAsync();
        db.Reports.Add(new(){Id=reportId,CaseId=caseId,ReporterAccountId=reporter,SubjectAccountId=subject,SubjectType=ReportSubject.Profile,SubjectRef=subject.ToString(),CategoryId=category,CreatedAt=clock.Now});
        db.StaffMemberLinks.Add(new(){StaffAccountId=subjectStaff,MemberAccountId=subject,LinkedBy=admin,LinkedAt=clock.Now});
        // Existing immutable staff-link audit is the actual retained association consulted by ExcludedAsync.
        Audit.Record(db,clock.Now,admin,"account","staff-link.set","account",historicalReporterStaff.ToString(),reporter.ToString());await db.SaveChangesAsync();
        check("ordinary member and Legal role cannot decrypt detector candidate",await ReadRefused(reporter,caseId,reviewId,403)&&await ReadRefused(legal,caseId,reviewId,403));
        check("subject-associated staff cannot read or mark candidate",await ReadRefused(subjectStaff,caseId,reviewId,403)&&await MarkRefused(subjectStaff,caseId,reviewId,"reviewed-no-action",null,null,403));
        check("retained reporter association refuses review without any current link",!await db.StaffMemberLinks.AnyAsync(l=>l.StaffAccountId==historicalReporterStaff)
            &&await ReadRefused(historicalReporterStaff,caseId,reviewId,403)&&await MarkRefused(historicalReporterStaff,caseId,reviewId,"reviewed-no-action",null,null,403));
        var opened=await reviews.ReadAsync(support,caseId,reviewId,default);
        check("authorized normal-case support decrypts only through audited read",opened.Text==quotation&&await db.AuditEntries.AnyAsync(a=>a.ActorAccountId==support&&a.Action=="text-harm.evidence-read"&&a.TargetId==reviewId.ToString()));
        check("candidate metadata and audit contain no copied source text",!(await reviews.ListAsync(admin,caseId,default)).Any(x=>x.Outcome!=null)
            &&!await db.AuditEntries.AnyAsync(a=>a.Detail!=null&&a.Detail.Contains(quotation)));
        var dismissed=await reviews.MarkAsync(support,caseId,reviewId,"reviewed-no-action",null,null,default);
        var dismissedReplay=await reviews.MarkAsync(support,caseId,reviewId,"reviewed-no-action",null,null,default);
        check("legitimate quoted phrase can be reviewed with no action and no automatic member notice",!dismissed.AlreadyApplied&&dismissedReplay.AlreadyApplied
            &&dismissed.Outcome=="reviewed-no-action"&&!await db.Sanctions.AnyAsync(s=>s.AccountId==subject)&&!await db.MemberNotices.AnyAsync(n=>n.AccountId==subject));

        var actionSubject=await Member();const string actionText="Fictional send me money text chosen for independent human content review";
        var actionIntent=await ProfileIntent(actionSubject,actionText);var actionCandidate=await processor.ProcessAsync(actionIntent,default);
        var actionCase=actionCandidate.CaseId!.Value;var actionReview=actionCandidate.ReviewId!.Value;
        check("actioned state refuses absent or made-up action references",await MarkRefused(admin,actionCase,actionReview,"actioned",null,null,400)
            &&await MarkRefused(admin,actionCase,actionReview,"actioned",Guid.NewGuid(),null,409));
        var earlier=new Sanction{Id=Guid.NewGuid(),CaseId=actionCase,AccountId=actionSubject,Kind=SanctionKind.Warning,Reason="Fictional earlier decision",
            CreatedBy=admin,CreatedByRole=AccountRole.Admin,StartsAt=clock.Now.AddSeconds(-1)};db.Sanctions.Add(earlier);await db.SaveChangesAsync();
        check("pre-candidate same-case decision cannot count as new human action",await MarkRefused(admin,actionCase,actionReview,"actioned",earlier.Id,null,409));
        var foreignAction=new Sanction{Id=Guid.NewGuid(),CaseId=caseId,AccountId=subject,Kind=SanctionKind.Warning,Reason="Fictional other-case decision",CreatedBy=admin,CreatedByRole=AccountRole.Admin,StartsAt=clock.Now.AddSeconds(1)};
        db.Sanctions.Add(foreignAction);await db.SaveChangesAsync();
        check("post-candidate foreign-case action cannot mark another candidate actioned",await MarkRefused(admin,actionCase,actionReview,"actioned",foreignAction.Id,null,409));
        var actionReport=Guid.NewGuid();db.Reports.Add(new(){Id=actionReport,CaseId=actionCase,ReporterAccountId=reporter,SubjectAccountId=actionSubject,
            SubjectType=ReportSubject.Profile,SubjectRef=actionSubject.ToString(),CategoryId=category,CreatedAt=clock.Now});await db.SaveChangesAsync();
        clock.Now=clock.Now.AddSeconds(2);
        var removeRequest=new ContentRemovalRequest(Guid.NewGuid(),actionReport,"about",null,null,ContentRemovalPolicy.Hash(actionText),"Fictional independent human removal after reviewing current source");
        await scope.ServiceProvider.GetRequiredService<ContentRemovalService>().RemoveAsync(admin,actionCase,removeRequest,clock.Now,default);
        var actioned=await reviews.MarkAsync(admin,actionCase,actionReview,"actioned",null,removeRequest.CommandId,default);
        check("actual post-candidate same-case content removal permits actioned review",actioned.Outcome=="actioned"&&!actioned.AlreadyApplied
            &&await db.MemberProfiles.AnyAsync(p=>p.AccountId==actionSubject&&p.About==null)&&await db.AuditEntries.AnyAsync(a=>a.Action=="text-harm.reviewed"&&a.TargetId==actionReview.ToString()));

        var threatSubject=await Member();var threatPeer=await Member();var threatConversation=await Conversation(threatSubject,threatPeer);
        var threatIntent=await MessageIntent(Message(threatSubject,threatConversation,"Fictional novel quotation: I will kill you is a line the narrator rejects.",clock.Now));
        var threat=await processor.ProcessAsync(threatIntent,default);
        check("direct phrase creates urgent candidate without treating quotation as a verdict",threat.Urgent&&threat.Result=="created"
            &&await db.ModerationCases.AnyAsync(c=>c.Id==threat.CaseId&&c.Queue==CaseQueue.Admin&&c.Priority==ReportPriority.Urgent)
            &&!await db.Reports.AnyAsync(r=>r.CaseId==threat.CaseId)&&!await db.Sanctions.AnyAsync(s=>s.AccountId==threatSubject)&&!await db.MemberNotices.AnyAsync(n=>n.AccountId==threatSubject));
        var hold=await db.LegalHolds.AsNoTracking().SingleAsync(h=>h.CaseId==threat.CaseId&&h.Source=="text-harm-threat");
        check("actual threat processor creates provisional preservation with both conversation participants",hold.Status==LegalHoldStatus.Provisional
            &&hold.ReviewDue<=clock.Now.AddDays(30)&&await db.LegalHoldScopes.AnyAsync(s=>s.HoldId==hold.Id&&s.Kind==HoldScopeKind.Conversation&&s.TargetId==threatConversation)
            &&await db.LegalHoldScopes.CountAsync(s=>s.HoldId==hold.Id&&s.Kind==HoldScopeKind.Account&&(s.TargetId==threatSubject||s.TargetId==threatPeer))==2);
        check("support cannot decrypt promoted urgent candidate",await ReadRefused(support,threat.CaseId!.Value,threat.ReviewId!.Value,403));
        var threatDismissed=await reviews.MarkAsync(admin,threat.CaseId!.Value,threat.ReviewId!.Value,"reviewed-no-action",null,null,default);
        check("human no-action review never silently releases an independent provisional hold",threatDismissed.Outcome=="reviewed-no-action"
            &&await db.LegalHolds.AnyAsync(h=>h.Id==hold.Id&&h.Status==LegalHoldStatus.Provisional));

        // Own backdated sources only: real retention service and independently held copies, no SQL clock override.
        var present=clock.Now;clock.Now=present.AddMonths(-14);
        var retainedSubject=await Member();var retainedIntent=await ProfileIntent(retainedSubject,"Fictional send me money historical review");
        var retainedResult=await processor.ProcessAsync(retainedIntent,default);var retainedCase=retainedResult.CaseId!.Value;
        await reviews.MarkAsync(admin,retainedCase,retainedResult.ReviewId!.Value,"reviewed-no-action",null,null,default);
        var retainedCaseRow=await db.ModerationCases.SingleAsync(c=>c.Id==retainedCase);retainedCaseRow.Status=CaseStatus.Closed;retainedCaseRow.ClosedAt=clock.Now;await db.SaveChangesAsync();
        var preservation=scope.ServiceProvider.GetRequiredService<EvidencePreservation>();
        var historicalHold=await LegalHoldEndpoints.PlaceAsync(db,preservation,AccountRole.Legal,legal,"Fictional historical review hold",[retainedSubject],[],null,null,null,null,retainedCase,"staff",clock.Now,default);
        var retention=scope.ServiceProvider.GetRequiredService<SafetyRetention>();
        var receiptRetention=new TextHarmRetention(db,preservation);
        clock.Now=present;
        config["Evidence:HoldCheckUnavailable"]="true";
        check("inspection receipt expiry fails closed when hold authority is unavailable",await receiptRetention.PurgeAsync(present,default)==null&&await db.Set<TextHarmIntent>().AnyAsync(i=>i.Id==retainedIntent));
        config["Evidence:HoldCheckUnavailable"]="false";
        await retention.PurgeAsync(present,default,onlyCase:retainedCase);await receiptRetention.PurgeAsync(present,default);
        check("live hold preserves independent candidate and completed receipt",await db.Set<TextHarmReview>().AnyAsync(r=>r.Id==retainedResult.ReviewId&&r.SealedEvidence!=null)
            &&await db.Set<TextHarmIntent>().AnyAsync(i=>i.Id==retainedIntent));
        check("later hold captures independent candidate evidence",await db.EvidenceItems.AnyAsync(e=>e.HoldId==historicalHold.Id&&e.Kind=="text-harm-review"&&e.RecordId==retainedResult.ReviewId.ToString()));
        historicalHold.Status=LegalHoldStatus.Released;historicalHold.ReleasedAt=present.AddMonths(-14).AddDays(1);historicalHold.ReleasedBy=legal;await db.SaveChangesAsync();
        await retention.PurgeAsync(present,default,onlyCase:retainedCase);
        check("released due copies and candidate content expire on independent clocks",await db.Set<TextHarmReview>().AnyAsync(r=>r.Id==retainedResult.ReviewId&&r.SealedEvidence==null&&r.EvidenceDisposedAt!=null)
            &&!await db.EvidenceItems.AnyAsync(e=>e.HoldId==historicalHold.Id)&&await db.ModerationCases.AnyAsync(c=>c.Id==retainedCase));
        await receiptRetention.PurgeAsync(present,default);
        check("completed text-free receipt expires after its30day window",!await db.Set<TextHarmIntent>().AnyAsync(i=>i.Id==retainedIntent));
    }
    private static ServiceProvider Provider(DbContextOptions<SwoovrDbContext> options,IConfiguration configuration,Clock clock)
    {
        var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new Development());services.AddSingleton<TimeProvider>(clock);services.AddSingleton<IEmailSender>(new Sink());
        services.AddScoped(_=>new SwoovrDbContext(options));services.AddScoped<Founders>();services.AddScoped<ContactHistoryKeys>();services.AddScoped<EvidenceKeys>();
        services.AddScoped<EvidencePreservation>();services.AddScoped<SafetyRetention>();services.AddScoped<ContentRemovalService>();services.AddScoped<ModerationTransparencyRecorder>();
        services.AddSingleton(new TextHarmRules(true));services.AddSingleton<RecordingDetector>();services.AddSingleton<ITextHarmDetector>(p=>p.GetRequiredService<RecordingDetector>());
        services.AddScoped<ModerationInspectionRecorder>();services.AddScoped<ModerationTransparencyRecorder>();services.AddScoped<TextHarmQueue>();services.AddScoped<TextHarmProcessor>();services.AddScoped<TextHarmCandidateReviewService>();return services.BuildServiceProvider();
    }
    private sealed class RecordingDetector:ITextHarmDetector
    {
        private readonly BoundedTextHarmDetector inner=new(new(true));public List<TextHarmInput> Inputs{get;}=[];
        public ValueTask<TextHarmAssessment> DetectAsync(TextHarmInput input,CancellationToken token){Inputs.Add(input);return inner.DetectAsync(input,token);}
    }
    private sealed class Clock:TimeProvider
    {public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Development:IHostEnvironment
    {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Sink:IEmailSender
    {public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new Exception("Runtime inspection must not send external notifications.");}
}
