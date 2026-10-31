using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Infrastructure.Email;
using Npgsql;
using Swoovr.Api.Notifications;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

/// <summary>Owned PostgreSQL fixture only. Staged source confirmation is synthetic, never actual legal approval.</summary>
static class PolicyNoticePersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(options);
        var sourceConnection=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(sourceConnection);await admin.OpenAsync();
        var name="swoovr_policy_notices_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(sourceConnection){Database=name,Pooling=false}.ConnectionString;
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options);
            await db.Database.MigrateAsync();
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()).Build();
            var clock=new Clock();var publisher=new PolicyNoticePublisher(db,config,clock);
            var first=Guid.NewGuid();var second=Guid.NewGuid();var staff=Guid.NewGuid();
            Account Account(Guid id,AccountRealm realm)=>new(){Id=id,Issuer="http://localhost/fictional-policy-test",Subject=id.ToString(),Realm=realm,Status=AccountStatus.Active,CreatedAt=clock.Now,LastSeenAt=clock.Now};
            var operatorAccount=Account(staff,AccountRealm.Staff);operatorAccount.IsRootAdmin=true;
            db.AddRange(Account(first,AccountRealm.Members),Account(second,AccountRealm.Members),operatorAccount);
            db.NotificationSettings.Add(new(){AccountId=second,Push=false,Email=false,PrivatePreviews=true,UpdatedAt=clock.Now});await db.SaveChangesAsync();
            var id=await publisher.StageAsync("fictional-2026-10-30.1","Fictional public safety guidance update","/safety/advice",staff,default);
            check("disabled publication creates no member notice",!await publisher.PublishAsync(id,staff,true,default)&&await publisher.RunBatchAsync(id,default)==0&&!await db.Notifications.AnyAsync());
            config["PolicyNotices:Enabled"]="true";
            check("source publication must be explicitly confirmed",!await publisher.PublishAsync(id,staff,false,default)&&!await db.Set<PolicyNoticeRecipient>().AnyAsync());
            check("operator activation snapshots current members",await publisher.PublishAsync(id,staff,true,default)&&await db.Set<PolicyNoticeRecipient>().CountAsync()==2);
            check("activation alone performs no outbound or centre effect",!await db.Notifications.AnyAsync()&&!await db.NotificationDeliveries.AnyAsync());
            clock.Now=clock.Now.AddSeconds(1);var late=Guid.NewGuid();db.Accounts.Add(Account(late,AccountRealm.Members));await db.SaveChangesAsync();
            var restarted=new PolicyNoticePublisher(db,config,clock);
            check("same version restaging is idempotent",await restarted.StageAsync("fictional-2026-10-30.1","Fictional public safety guidance update","/safety/advice",staff,default)==id);
            check("restart and repeated activation never enroll new members",await restarted.PublishAsync(id,staff,true,default)&&await db.Set<PolicyNoticeRecipient>().CountAsync()==2);
            check("bounded batch records each eligible member once",await restarted.RunBatchAsync(id,default)==2&&await db.Notifications.CountAsync()==2);
            check("repeated batch cannot duplicate member centre notices",await restarted.RunBatchAsync(id,default)==0&&await db.Notifications.CountAsync()==2);
            check("members registered after activation receive no historical broadcast",!await db.Notifications.AnyAsync(n=>n.AccountId==late));
            check("all centre rows reference publication only and have bounded dedupe",await db.Notifications.AllAsync(n=>n.SubjectId==id&&n.ActorAccountId==null&&n.ModeId==null&&n.Count==1&&n.DedupeKey==PolicyNoticePolicy.Dedupe(id)));
            check("centre only publication respects push and email off without external attempts",!await db.NotificationDeliveries.AnyAsync());
            var firstCentre=await NotificationEndpoints.CentreAsync(db,first,false,clock.Now,default);
            var secondCentre=await NotificationEndpoints.CentreAsync(db,second,false,clock.Now,default);
            check("centre policy projection is member private and source allowlisted",firstCentre.Items.Count==1&&secondCentre.Items.Count==1
                &&firstCentre.Items[0].Id!=secondCentre.Items[0].Id&&firstCentre.Items[0].Link=="/safety/advice"&&firstCentre.Items[0].Kind=="policyChange");
            var rejected=false;try{await publisher.StageAsync("fictional-2026-10-30.1","Changed metadata","/safety",staff,default);}catch(InvalidOperationException){rejected=true;}
            check("reusing a version with different metadata is refused",rejected);
            rejected=false;try{await publisher.StageAsync("fictional-draft","Draft legal policy","/privacy",staff,default);}catch(InvalidOperationException){rejected=true;}
            check("draft legal documents cannot become publication sources",rejected);
            var next=await publisher.StageAsync("fictional-2026-10-30.2","Fictional next safety guidance update","/safety/advice",staff,default);
            await publisher.PublishAsync(next,staff,true,default);
            await db.Accounts.Where(a=>a.Id==first).ExecuteUpdateAsync(set=>set.SetProperty(a=>a.Status,AccountStatus.PendingDeletion));
            await publisher.RunBatchAsync(next,default);
            check("recipient state is rechecked before centre delivery",!await db.Notifications.AnyAsync(n=>n.SubjectId==next&&n.AccountId==first)
                &&await db.Set<PolicyNoticeRecipient>().AnyAsync(r=>r.PublicationId==next&&r.AccountId==first&&r.Status=="suppressed"));
            // Two actual producer replicas race the same durable queue; only source completion commits a centre row.
            var third=await publisher.StageAsync("fictional-concurrent","Fictional concurrent safety update","/safety",staff,default);await publisher.PublishAsync(third,staff,true,default);
            async Task<int> Replica(){await using var other=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options);return await new PolicyNoticePublisher(other,config,clock).RunBatchAsync(third,default);}
            var runs=await Task.WhenAll(Replica(),Replica());
            check("concurrent notice replicas complete each target once",runs.Sum()==2&&await db.Notifications.CountAsync(n=>n.SubjectId==third)==2);
            var failed=await publisher.StageAsync("fictional-rollback","Fictional rollback safety update","/safety",staff,default);await publisher.PublishAsync(failed,staff,true,default);
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fixture_policy_completion_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.status<>'queued' THEN RAISE EXCEPTION 'Fictional policy completion rollback';END IF;RETURN NEW;END $$; CREATE TRIGGER fixture_policy_completion_failure BEFORE UPDATE ON policy_notice_recipients FOR EACH ROW EXECUTE FUNCTION fixture_policy_completion_failure();");
            var rollback=false;try{await publisher.RunBatchAsync(failed,default);}catch(PostgresException e)when(e.SqlState=="P0001"){rollback=true;}
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fixture_policy_completion_failure ON policy_notice_recipients; DROP FUNCTION fixture_policy_completion_failure();");
            check("notice source checkpoint failure rolls centre insertion back",rollback&&!await db.Notifications.AnyAsync(n=>n.SubjectId==failed)&&await db.Set<PolicyNoticeRecipient>().Where(r=>r.PublicationId==failed).AllAsync(r=>r.Status=="queued"));
            await publisher.RunBatchAsync(failed,default);
            check("notice queue resumes after transaction failure",await db.Set<PolicyNoticeRecipient>().Where(r=>r.PublicationId==failed).AllAsync(r=>r.Status!="queued"));
            var noRewrite=false;try{await db.Set<PolicyNoticePublication>().Where(p=>p.Id==id).ExecuteUpdateAsync(set=>set.SetProperty(p=>p.Title,"Fictional changed source"));}catch(PostgresException e)when(e.SqlState=="23514"){noRewrite=true;}
            check("database freezes staged public source metadata",noRewrite);
            var premature=false;try{await db.Set<PolicyNoticeRecipient>().Where(r=>r.PublicationId==id).ExecuteDeleteAsync();}catch(PostgresException e)when(e.SqlState=="23514"){premature=true;}
            check("database prevents premature recipient group disposal",premature);
            // A historical fictional source permits testing actual database time gates without bypassing guards.
            clock.Now=DateTimeOffset.UtcNow.AddDays(-70);var historicMember=Guid.NewGuid();db.Accounts.Add(Account(historicMember,AccountRealm.Members));await db.SaveChangesAsync();
            var historical=await publisher.StageAsync("fictional-expiry","Fictional historic public guidance","/safety",staff,default);await publisher.PublishAsync(historical,staff,true,default);await publisher.RunBatchAsync(historical,default);
            var abandoned=await publisher.StageAsync("fictional-abandoned","Fictional never-published staging","/safety",staff,default);
            clock.Now=DateTimeOffset.UtcNow;
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<TimeProvider>(clock).AddSingleton<IHostEnvironment>(new Development()).AddLogging().BuildServiceProvider();
            var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);
            var evidence=new EvidencePreservation(services,new EvidenceKeys(config,founders),new ContactHistoryKeys(config,founders));
            var purge=new PolicyNoticeRetention(db,evidence,clock);
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional policy receipt hold",PlacedBy=staff,PlacedByRole=AccountRole.Legal,PlacedAt=clock.Now,ReviewDue=clock.Now.AddDays(1),RelatedAccountIds=[historicMember],RetentionCaseIds=[]};
            db.LegalHolds.Add(hold);db.LegalHoldScopes.Add(new(){HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=historicMember});await db.SaveChangesAsync();
            check("expiry preserves a held recipient group while expiring unused staging",await purge.PurgeAsync(default)==1&&await db.Set<PolicyNoticeRecipient>().AnyAsync(r=>r.PublicationId==historical)&&await db.Set<PolicyNoticePublication>().AnyAsync(p=>p.Id==abandoned&&p.DisposedAt!=null&&p.StagedBy==null));
            hold.Status=LegalHoldStatus.Released;hold.ReleasedAt=clock.Now;hold.ReleasedBy=staff;hold.ReleaseReason="Fictional release";hold.RelatedAccountIds=null;await db.SaveChangesAsync();
            check("unresolved released hold prevents policy group expiry",await purge.PurgeAsync(default)==null);
            hold.RelatedAccountIds=[historicMember];await db.SaveChangesAsync();
            check("policy recipient centre and operator metadata expire atomically",await purge.PurgeAsync(default)==1&&!await db.Set<PolicyNoticeRecipient>().AnyAsync(r=>r.PublicationId==historical)&&!await db.Notifications.AnyAsync(n=>n.SubjectId==historical)&&await db.Set<PolicyNoticePublication>().AnyAsync(p=>p.Id==historical&&p.DisposedAt!=null&&p.StagedBy==null&&p.PublishedBy==null));
            var reuse=false;try{await publisher.StageAsync("fictional-expiry","Fictional historic public guidance","/safety",staff,default);}catch(InvalidOperationException){reuse=true;}
            check("non-member version disposition prevents disposed version reuse",reuse&&!await publisher.PublishAsync(historical,staff,true,default)&&await publisher.RunBatchAsync(historical,default)==0);

        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Development:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new InvalidOperationException("No external delivery in policy notice fixture.");}
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=new(2026,10,30,0,0,0,TimeSpan.Zero);public override DateTimeOffset GetUtcNow()=>Now;}
}
