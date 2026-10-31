using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Notifications;
using Swoovr.Infrastructure.Email;
using Swoovr.Api.Moderation;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>Actual PostgreSQL test SOURCE ONLY. Root creates/migrates a dedicated owned DB first.</summary>
static class ReportOutcomePersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options);
        if (!db.Database.GetDbConnection().Database.StartsWith("swoovr_check_", StringComparison.Ordinal))
            throw new InvalidOperationException("Report outcome tests require their owned throwaway database.");
        var now = DateTimeOffset.Parse("2026-10-30T00:00:00Z");
        var reporter = Guid.NewGuid(); var subject = Guid.NewGuid(); var staff = Guid.NewGuid(); var caseId = Guid.NewGuid(); var reportId = Guid.NewGuid();
        await using (var authority = await PreservationLease.ExclusiveAsync(db, default))
        await using (var caseAuthority = await PreservationLease.CaseAsync(db, caseId, default))
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            foreach (var id in new[] { reporter, subject, staff }) db.Accounts.Add(new Account
            {
                Id = id, Subject = id.ToString(), Issuer = "https://synthetic.example.test/realm", Realm = id == staff ? AccountRealm.Staff : AccountRealm.Members,
                Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now
            });
            db.ModerationCases.Add(new() { Id = caseId, SubjectAccountId = subject, Status = CaseStatus.Open, Queue = CaseQueue.Support, CreatedAt = now, UpdatedAt = now });
            db.Reports.Add(new() { Id = reportId, CaseId = caseId, ReporterAccountId = reporter, SubjectAccountId = subject, SubjectType = ReportSubject.Profile, SubjectRef = subject.ToString(), CategoryId = "other", CreatedAt = now, SupportReference = SupportReference.New() });
            await db.SaveChangesAsync();
            check("new report initialises silent durable baseline", await ReportOutcomeCapture.InTransactionAsync(db, caseId, now, default) == 0);
            await transaction.CommitAsync();
        }
        await using (var restart = new SwoovrDbContext(options))
        await using (var authority = await PreservationLease.ExclusiveAsync(restart, default))
        await using (var caseAuthority = await PreservationLease.CaseAsync(restart, caseId, default))
        await using (var transaction = await restart.Database.BeginTransactionAsync())
        {
            var review = await restart.ModerationCases.SingleAsync(item => item.Id == caseId);
            review.Status = CaseStatus.Closed; review.ClosedAt = now.AddMinutes(1); review.UpdatedAt = now.AddMinutes(1);
            await restart.SaveChangesAsync();
            check("source closure queues exactly once in same transaction", await ReportOutcomeCapture.InTransactionAsync(restart, caseId, now.AddMinutes(1), default) == 1);
            check("repeat capture before commit queues nothing", await ReportOutcomeCapture.InTransactionAsync(restart, caseId, now.AddMinutes(1), default) == 0);
            await transaction.CommitAsync();
        }
        await using (var restart = new SwoovrDbContext(options))
        await using (var authority = await PreservationLease.ExclusiveAsync(restart, default))
        await using (var caseAuthority = await PreservationLease.CaseAsync(restart, caseId, default))
        await using (var transaction = await restart.Database.BeginTransactionAsync())
        {
            check("worker restart does not duplicate closed result", await ReportOutcomeCapture.InTransactionAsync(restart, caseId, now.AddMinutes(2), default) == 0);
            var review = await restart.ModerationCases.SingleAsync(item => item.Id == caseId);
            review.UpdatedAt = now.AddMinutes(2); await restart.SaveChangesAsync();
            check("internal note/view clock change causes no notice", await ReportOutcomeCapture.InTransactionAsync(restart, caseId, now.AddMinutes(2), default) == 0);
            restart.Sanctions.Add(new() { Id = Guid.NewGuid(), AccountId = subject, CaseId = caseId, Kind = SanctionKind.Warning, Reason = "Fictional private reason never projected", StartsAt = now.AddMinutes(3), CreatedBy = staff, CreatedByRole = AccountRole.Admin });
            await restart.SaveChangesAsync();
            check("real source sanction change queues next revision", await ReportOutcomeCapture.InTransactionAsync(restart, caseId, now.AddMinutes(3), default) == 1);
            await transaction.RollbackAsync();
        }
        await using var verify = new SwoovrDbContext(options);
        check("rollback cannot advance checkpoint without notice or vice versa", await verify.Set<ReportOutcomeState>().Where(item => item.ReportId == reportId).Select(item => item.Revision).SingleAsync() == 1
            && await verify.Notifications.CountAsync(item => item.AccountId == reporter && item.Kind == NotificationKind.ReportOutcome) == 1);
        var notice = await verify.Notifications.SingleAsync(item => item.AccountId == reporter && item.Kind == NotificationKind.ReportOutcome);
        check("persisted notification has only own report pointer and no actor/mode", notice.SubjectId == reportId && notice.ActorAccountId is null && notice.ModeId is null);
        // Two independent producers start together. The existing preservation→case→transaction order serializes
        // reading/lifting the same source; only the winner applies a lift and captures a new revision.
        var sanctionId=Guid.NewGuid();
        await using(var initial=new SwoovrDbContext(options))
        await using(var authority=await PreservationLease.ExclusiveAsync(initial,default))
        await using(var mutex=await PreservationLease.CaseAsync(initial,caseId,default))
        await using(var transaction=await initial.Database.BeginTransactionAsync())
        {
            initial.Sanctions.Add(new(){Id=sanctionId,CaseId=caseId,AccountId=subject,Kind=SanctionKind.Warning,
                Reason="Fictional private concurrent source reason",StartsAt=now.AddMinutes(3),CreatedBy=staff,CreatedByRole=AccountRole.Admin});
            await initial.SaveChangesAsync();
            check("concurrent fixture commits its actual initial source decision",await ReportOutcomeCapture.InTransactionAsync(initial,caseId,now.AddMinutes(3),default)==1);
            await transaction.CommitAsync();
        }
        var start=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(int Lifts,int Notices)> Producer()
        {
            await start.Task;
            await using var writer=new SwoovrDbContext(options);
            await using var authority=await PreservationLease.ExclusiveAsync(writer,default);
            await using var mutex=await PreservationLease.CaseAsync(writer,caseId,default);
            await using var transaction=await writer.Database.BeginTransactionAsync();
            var sourceSanction=await writer.Sanctions.SingleAsync(item=>item.Id==sanctionId);
            var lifted=0;
            if(sourceSanction.LiftedAt is null)
            {
                sourceSanction.LiftedAt=now.AddMinutes(4);sourceSanction.LiftedBy=staff;lifted=1;
                await writer.SaveChangesAsync();
            }
            var notices=await ReportOutcomeCapture.InTransactionAsync(writer,caseId,now.AddMinutes(4),default);
            await transaction.CommitAsync();return(lifted,notices);
        }
        var producerA=Producer();var producerB=Producer();start.SetResult(true);
        var races=await Task.WhenAll(producerA,producerB);
        await using(var observed=new SwoovrDbContext(options))
        {
            check("concurrent producers apply one source lift and create one new notice",races.Sum(r=>r.Lifts)==1&&races.Sum(r=>r.Notices)==1
                &&await observed.Sanctions.CountAsync(item=>item.Id==sanctionId&&item.LiftedAt==now.AddMinutes(4))==1);
            check("concurrent capture advances exact revision once",await observed.Set<ReportOutcomeState>().Where(item=>item.ReportId==reportId).Select(item=>item.Revision).SingleAsync()==3
                &&await observed.Notifications.CountAsync(item=>item.AccountId==reporter&&item.Kind==NotificationKind.ReportOutcome)==3);
        }

        var otherReporter=Guid.NewGuid();var otherSubject=Guid.NewGuid();var foreignCase=Guid.NewGuid();var foreignReport=Guid.NewGuid();var uncheckedReport=Guid.NewGuid();
        await using(var fixture=new SwoovrDbContext(options))
        {
            foreach(var id in new[]{otherReporter,otherSubject})fixture.Accounts.Add(new(){Id=id,Subject=id.ToString(),Issuer="https://synthetic.example.test/outcome-private",Realm=AccountRealm.Members,
                Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});
            fixture.ModerationCases.Add(new(){Id=foreignCase,SubjectAccountId=otherSubject,Status=CaseStatus.Open,Queue=CaseQueue.Support,CreatedAt=now,UpdatedAt=now});
            fixture.Reports.Add(new(){Id=foreignReport,CaseId=foreignCase,ReporterAccountId=otherReporter,SubjectAccountId=otherSubject,SubjectType=ReportSubject.Profile,
                SubjectRef=otherSubject.ToString(),CategoryId="other",CreatedAt=now,SupportReference=SupportReference.New()});
            fixture.Reports.Add(new(){Id=uncheckedReport,CaseId=foreignCase,ReporterAccountId=otherReporter,SubjectAccountId=otherSubject,SubjectType=ReportSubject.Profile,
                SubjectRef=otherSubject.ToString(),CategoryId="other",CreatedAt=now,SupportReference=SupportReference.New()});
            await fixture.SaveChangesAsync();
        }
        async Task<bool> RejectProvenance(Guid sourceCase,Guid sourceAccount)
        {
            await using var invalid=new SwoovrDbContext(options);
            invalid.Set<ReportOutcomeState>().Add(new(){ReportId=uncheckedReport,SourceCaseId=sourceCase,SourceAccountId=sourceAccount,Revision=0});
            try{await invalid.SaveChangesAsync();return false;}
            catch(DbUpdateException error)when(error.InnerException is PostgresException {SqlState:PostgresErrorCodes.ForeignKeyViolation}){return true;}
        }
        check("composite report checkpoint FK rejects a different reporter",await RejectProvenance(foreignCase,reporter));
        check("composite report checkpoint FK rejects a different source case",await RejectProvenance(caseId,otherReporter));
        var foreignNotice=Guid.NewGuid();var wrongRecipient=Guid.NewGuid();var actorLeak=Guid.NewGuid();var modeLeak=Guid.NewGuid();var missingSource=Guid.NewGuid();
        await using(var fixture=new SwoovrDbContext(options))
        {
            Notification Outcome(Guid id,Guid recipient,Guid report,Guid? actor=null,string? mode=null)=>new(){Id=id,AccountId=recipient,Kind=NotificationKind.ReportOutcome,
                SubjectId=report,ActorAccountId=actor,ModeId=mode,CreatedAt=now,UpdatedAt=now};
            // Polymorphic notification ids have no Report FK. Deliberately malformed owned rows verify read-side defence.
            fixture.Notifications.AddRange(Outcome(foreignNotice,otherReporter,foreignReport),Outcome(wrongRecipient,subject,reportId),
                Outcome(actorLeak,reporter,reportId,staff),Outcome(modeLeak,reporter,reportId,null,"friends"),Outcome(missingSource,reporter,Guid.NewGuid()));
            fixture.Sanctions.Add(new(){Id=Guid.NewGuid(),AccountId=reporter,Kind=SanctionKind.Suspension,Reason="Fictional reporter restriction",
                StartsAt=now,EndsAt=now.AddDays(1),CreatedBy=staff,CreatedByRole=AccountRole.Admin});
            await fixture.SaveChangesAsync();
            var ordinary=await NotificationEndpoints.CentreAsync(fixture,reporter,false,now.AddMinutes(5),default);
            var barred=await NotificationEndpoints.CentreAsync(fixture,reporter,true,now.AddMinutes(5),default);
            var foreign=await NotificationEndpoints.CentreAsync(fixture,otherReporter,false,now.AddMinutes(5),default);
            var reportedSubject=await NotificationEndpoints.CentreAsync(fixture,subject,false,now.AddMinutes(5),default);
            var hidden=new[]{wrongRecipient,actorLeak,modeLeak,missingSource,foreignNotice};
            check("centre projects only recipient own report updates and rejects actor/mode/foreign/missing sources",ordinary.Items.Count==3
                &&ordinary.Items.All(item=>item.Kind=="reportOutcome"&&item.Link=="/me/notices"&&!hidden.Contains(item.Id)
                    &&item.Title=="There is an update to a report you submitted. Open Notices and reports to review it."));
            check("barred active member retains own private report updates",Enforcement.For(await fixture.Sanctions.Where(item=>item.AccountId==reporter).ToListAsync(),now.AddMinutes(5)).Barred
                &&barred.Items.Select(item=>item.Id).Order().SequenceEqual(ordinary.Items.Select(item=>item.Id).Order()));
            check("reported member cannot read another reporter's update and second reporter sees only own",reportedSubject.Items.Count==0&&foreign.Items.Count==1&&foreign.Items[0].Id==foreignNotice);
            check("centre wording exposes no case identity, staff identity or internal reason",ordinary.Items.All(item=>!item.Title.Contains(caseId.ToString())&&!item.Title.Contains(staff.ToString())&&!item.Title.Contains("Fictional private")));
            check("centre only outcome flow creates no dispatch or provider attempt",!await fixture.NotificationDeliveries.AnyAsync(delivery=>fixture.Notifications.Any(item=>item.Id==delivery.NotificationId
                &&item.Kind==NotificationKind.ReportOutcome&&(item.AccountId==reporter||item.AccountId==otherReporter))));
        }

        // Inactive reporter still advances its private checkpoint with source decisions, without resurrectable notices.
        var inactiveSource=Guid.NewGuid();
        foreach(var status in new[]{AccountStatus.PendingDeletion,AccountStatus.Deleted})
        {
            await using var writer=new SwoovrDbContext(options);
            await using var authority=await PreservationLease.ExclusiveAsync(writer,default);
            await using var mutex=await PreservationLease.CaseAsync(writer,caseId,default);
            await using var transaction=await writer.Database.BeginTransactionAsync();
            await writer.Accounts.Where(account=>account.Id==reporter).ExecuteUpdateAsync(set=>set.SetProperty(account=>account.Status,status));
            if(status==AccountStatus.PendingDeletion)
                writer.Sanctions.Add(new(){Id=inactiveSource,AccountId=subject,CaseId=caseId,Kind=SanctionKind.Warning,Reason="Fictional source while reporter inactive",StartsAt=now.AddMinutes(6),CreatedBy=staff,CreatedByRole=AccountRole.Admin});
            else
            {
                var sourceSanction=await writer.Sanctions.SingleAsync(item=>item.Id==inactiveSource);sourceSanction.LiftedAt=now.AddMinutes(7);sourceSanction.LiftedBy=staff;
            }
            await writer.SaveChangesAsync();
            check("inactive reporter source update advances without queueing "+status,await ReportOutcomeCapture.InTransactionAsync(writer,caseId,now.AddMinutes(7),default)==0);
            await transaction.CommitAsync();
            await using var observer=new SwoovrDbContext(options);
            check("inactive reporter centre is empty "+status,(await NotificationEndpoints.CentreAsync(observer,reporter,true,now.AddMinutes(7),default)).Items.Count==0);
        }
        await using(var writer=new SwoovrDbContext(options))
        await using(var authority=await PreservationLease.ExclusiveAsync(writer,default))
        await using(var mutex=await PreservationLease.CaseAsync(writer,caseId,default))
        await using(var transaction=await writer.Database.BeginTransactionAsync())
        {
            check("both inactive source decisions advanced exact checkpoint without new notice",await writer.Set<ReportOutcomeState>().Where(item=>item.ReportId==reportId).Select(item=>item.Revision).SingleAsync()==5
                &&await writer.Notifications.CountAsync(item=>item.AccountId==reporter&&item.Kind==NotificationKind.ReportOutcome&&item.DedupeKey!=null)==3);
            await writer.Accounts.Where(account=>account.Id==reporter).ExecuteUpdateAsync(set=>set.SetProperty(account=>account.Status,AccountStatus.Active));
            check("reactivating unchanged reporter cannot backfill suppressed source notifications",await ReportOutcomeCapture.InTransactionAsync(writer,caseId,now.AddMinutes(8),default)==0);
            await transaction.CommitAsync();
        }

        // A separate old, unheld case exercises the actual guarded content+record expiry path.
        var expiryCase=Guid.NewGuid();var expiryReport=Guid.NewGuid();var expiryReporter=Guid.NewGuid();var expirySubject=Guid.NewGuid();var old=now.AddYears(-4);
        await using(var expiry=new SwoovrDbContext(options))
        await using(var authority=await PreservationLease.ExclusiveAsync(expiry,default))
        await using(var mutex=await PreservationLease.CaseAsync(expiry,expiryCase,default))
        await using(var transaction=await expiry.Database.BeginTransactionAsync())
        {
            foreach(var id in new[]{expiryReporter,expirySubject})expiry.Accounts.Add(new(){Id=id,Subject=id.ToString(),Issuer="https://synthetic.example.test/outcome-expiry",Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=old,LastSeenAt=old});
            expiry.ModerationCases.Add(new(){Id=expiryCase,SubjectAccountId=expirySubject,Status=CaseStatus.Open,Queue=CaseQueue.Support,CreatedAt=old,UpdatedAt=old});
            expiry.Reports.Add(new(){Id=expiryReport,CaseId=expiryCase,ReporterAccountId=expiryReporter,SubjectAccountId=expirySubject,SubjectType=ReportSubject.Profile,SubjectRef=expirySubject.ToString(),CategoryId="other",CreatedAt=old,SupportReference=SupportReference.New()});
            await expiry.SaveChangesAsync();await ReportOutcomeCapture.InTransactionAsync(expiry,expiryCase,old,default);
            var review=await expiry.ModerationCases.SingleAsync(item=>item.Id==expiryCase);review.Status=CaseStatus.Closed;review.ClosedAt=old.AddHours(1);review.ClosedBy=staff;review.UpdatedAt=old.AddHours(1);await expiry.SaveChangesAsync();
            check("old expiry fixture has one actual source-backed outcome notification",await ReportOutcomeCapture.InTransactionAsync(expiry,expiryCase,old.AddHours(1),default)==1);
            await transaction.CommitAsync();
        }
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["Evidence:Keys:1"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["ContactHistory:Keys:1"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)),
            ["ContactHistory:LookupKey"]=Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
        }).Build();
        var services=new ServiceCollection();services.AddLogging();services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new Development());services.AddSingleton<TimeProvider>(TimeProvider.System);services.AddSingleton<IEmailSender>(new Sink());
        services.AddScoped(_=>new SwoovrDbContext(options));services.AddScoped<Founders>();services.AddScoped<ContactHistoryKeys>();services.AddScoped<EvidenceKeys>();services.AddScoped<EvidencePreservation>();services.AddScoped<SafetyRetention>();
        await using(var provider=services.BuildServiceProvider())
        await using(var scope=provider.CreateAsyncScope())
        {
            var retention=scope.ServiceProvider.GetRequiredService<SafetyRetention>();await retention.PurgeAsync(now,default,onlyCase:expiryCase);
            var expired=scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
            check("actual safe case record expiry removes source report and checkpoint together",!await expired.ModerationCases.AnyAsync(item=>item.Id==expiryCase)
                &&!await expired.Reports.AnyAsync(item=>item.Id==expiryReport)&&!await expired.Set<ReportOutcomeState>().AnyAsync(item=>item.ReportId==expiryReport));
            check("actual safe expiry leaves no orphan report outcome notification",!await expired.Notifications.AnyAsync(item=>item.Kind==NotificationKind.ReportOutcome&&item.SubjectId==expiryReport)
                &&(await NotificationEndpoints.CentreAsync(expired,expiryReporter,false,now,default)).Items.Count==0);
        }
        // Dedicated fixture DB is destroyed by the parent runner, never by this test source.
    }
    private sealed class Development:IHostEnvironment
    {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Sink:IEmailSender
    {public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new Exception("Report outcome expiry has no external delivery.");}
}
