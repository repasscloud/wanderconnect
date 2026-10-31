using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Analytics;
using Swoovr.Api.Evidence;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class BetaAnalyticsPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        check("beta privacy suppresses positive cells below5",BetaScorecardPrivacy.Suppress([(1,null),(5,10)]));
        check("beta privacy protects small complementary ratio cells",BetaScorecardPrivacy.Suppress([(9,10)]));
        check("beta privacy permits zero and sufficiently sized ratio cells",!BetaScorecardPrivacy.Suppress([(0,null),(5,10),(10,10)]));
        check("beta zero denominator is unavailable, not a success rate",BetaScorecardPrivacy.Percentage(0,0)==null&&BetaScorecardPrivacy.Percentage(5,10)==50);
        await using var source=new SwoovrDbContext(sourceOptions);var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();var name="swoovr_beta_analytics_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            await Swoovr.Infrastructure.Places.GeoNamesImport.ImportAsync(db,Path.Combine(AppContext.BaseDirectory,"geonames"));
            var clock=new Clock();var env=new Host();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)42,32).ToArray())}).Build();
            var ids=Enumerable.Range(0,10).Select(_=>Guid.NewGuid()).ToArray();var staff=Guid.NewGuid();
            foreach(var id in ids.Append(staff))db.Accounts.Add(new(){Id=id,Issuer="https://beta-fixture.example.test/realms/members",Subject=id.ToString(),Email="fictional."+id+"@example.test",Realm=id==staff?AccountRealm.Staff:AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=clock.Now.AddDays(-6),LastSeenAt=clock.Now});
            foreach(var id in ids){db.MemberProfiles.Add(new(){AccountId=id,PublicId=Guid.NewGuid(),AgeStatus=AgeStatus.SelfDeclaredAdult,DateOfBirth=new(1990,1,1),DisplayName="Fictional Beta",HomeGeoNameId=2078025});db.MemberModes.Add(new(){AccountId=id,ModeId="friends",Enabled=true,Visible=true});}
            await db.SaveChangesAsync();var recorder=new BetaJourneyRecorder(options,config,env,clock,NullLogger<BetaJourneyRecorder>.Instance);
            await recorder.RecordAsync(ids[0],true,true,default);
            check("disabled analytics writes no daily observation",!await db.Set<BetaJourneyDay>().AnyAsync());
            config["BetaAnalytics:Enabled"]="true";
            await Task.WhenAll(Enumerable.Range(0,4).Select(n=>recorder.RecordAsync(ids[0],n%2==0,n%2==0,default)));
            await recorder.RecordAsync(ids[0],true,true,default);
            var today=DateOnly.FromDateTime(clock.Now.UtcDateTime);
            check("actual concurrent daily upsert deduplicates and merges observations",await db.Set<BetaJourneyDay>().CountAsync()==1&&await db.Set<BetaJourneyDay>().AnyAsync(d=>d.ExploreSucceeded&&d.ExploreHasPeer));
            await recorder.RecordAsync(staff,true,true,default);
            var bot=await db.MemberProfiles.SingleAsync(p=>p.AccountId==ids[1]);bot.Bot=true;await db.SaveChangesAsync();await recorder.RecordAsync(ids[1],true,true,default);bot.Bot=false;await db.SaveChangesAsync();
            check("staff and bot observations are excluded",await db.Set<BetaJourneyDay>().CountAsync()==1);
            await using(var raceDb=new SwoovrDbContext(options))
            await using(var state=await raceDb.Database.BeginTransactionAsync())
            {
                await raceDb.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM accounts WHERE id={ids[2]} FOR UPDATE");
                var raceConnection=new NpgsqlConnectionStringBuilder(connection){ApplicationName="swoovr-beta-deletion-race"}.ConnectionString;
                var raceOptions=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(raceConnection).Options;
                var racing=new BetaJourneyRecorder(raceOptions,config,env,clock,NullLogger<BetaJourneyRecorder>.Instance).RecordAsync(ids[2],false,false,default);
                var waiting=false;
                for(var attempt=0;attempt<25;attempt++)
                {
                    await using var probe=new NpgsqlCommand("SELECT count(*) FROM pg_stat_activity WHERE datname=@database AND application_name='swoovr-beta-deletion-race' AND wait_event_type='Lock'",admin);probe.Parameters.AddWithValue("database",name);
                    if(Convert.ToInt32(await probe.ExecuteScalarAsync())>0){waiting=true;break;}await Task.Delay(10);
                }
                check("beta deletion race reaches a positively observed database wait",waiting);
                await raceDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE accounts SET status='PendingDeletion' WHERE id={ids[2]}");
                await raceDb.Set<BetaJourneyDay>().Where(d=>d.AccountId==ids[2]).ExecuteDeleteAsync();await state.CommitAsync();await racing;
                check("waiting GET observation cannot recreate metadata after deletion authority",!await db.Set<BetaJourneyDay>().AnyAsync(d=>d.AccountId==ids[2]));
                await raceDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE accounts SET status='Active' WHERE id={ids[2]}");
            }
            foreach(var pair in ids.Select((id,n)=>(id,n)))db.Set<BetaJourneyDay>().Add(new(){AccountId=pair.id,Day=today.AddDays(-5),Dataset="local-synthetic",ExploreSucceeded=true,ExploreHasPeer=pair.n<5});
            await db.SaveChangesAsync();var query=new BetaScorecardQuery(db,config,env,clock);var report=await query.ReadAsync(1,default);
            var discovery=report.Metrics.Single(m=>m.Key=="discovery-with-peer");
            check("actual completed-week observations have exact denominators",report.FromInclusive==today.AddDays(-7)&&report.UntilExclusive==today&&discovery.Numerator==5&&discovery.Denominator==10&&discovery.Percentage==50);
            check("signup activation is bounded to cohort and source week",report.Metrics.Single(m=>m.Key=="activation-observed") is{Numerator:10,Denominator:10});
            check("unmeasured attendance quality and commercial results stay null",report.Metrics.Where(m=>m.Status=="not-recorded").All(m=>m.Numerator==null&&m.Percentage==null)&&report.Metrics.Single(m=>m.Key=="real-world-outcomes").Status=="not-recorded");
            for(var n=0;n<10;n++)
            {
                var from=ids[n];var to=ids[(n+1)%10];var pair=ConnectionRequest.Pair(from,to);var accepted=n<5;
                db.ConnectionRequests.Add(new(){Id=Guid.NewGuid(),FromAccountId=from,ToAccountId=to,PairLow=pair.Low,PairHigh=pair.High,ModeId="friends",Status=accepted?ConnectionRequestStatus.Accepted:ConnectionRequestStatus.Pending,CreatedAt=clock.Now.AddDays(-5),AnsweredAt=accepted?clock.Now.AddDays(-4):null});
                if(!accepted)continue;
                var conversation=Guid.NewGuid();db.Conversations.Add(new(){Id=conversation,PairLow=pair.Low,PairHigh=pair.High,CreatedAt=clock.Now.AddDays(-4).AddHours(1),LastMessageAt=clock.Now.AddDays(-3)});
                db.Messages.Add(new(){Id=Guid.NewGuid(),ConversationId=conversation,SenderAccountId=from,Kind=MessageKind.Text,Body="Fictional private marker never read for analytics",SentAt=clock.Now.AddDays(-3)});
                db.Messages.Add(new(){Id=Guid.NewGuid(),ConversationId=conversation,SenderAccountId=to,Kind=MessageKind.System,Body="Fictional system event",SentAt=clock.Now.AddDays(-3)});
            }
            await db.SaveChangesAsync();report=await query.ReadAsync(1,default);
            check("request acceptance uses the sent cohort and correct denominator",report.Metrics.Single(m=>m.Key=="request-acceptance") is{Numerator:5,Denominator:10,Percentage:50});
            check("system events do not manufacture reciprocal conversation",report.Metrics.Single(m=>m.Key=="reciprocal-message-metadata") is{Numerator:0,Denominator:5});
            await db.Messages.Where(m=>m.Kind==MessageKind.System).ExecuteUpdateAsync(s=>s.SetProperty(m=>m.Kind,MessageKind.Text));report=await query.ReadAsync(1,default);
            check("both retained sender metadata count reciprocal request cohorts",report.Metrics.Single(m=>m.Key=="reciprocal-message-metadata") is{Numerator:5,Denominator:5,Percentage:100});
            await db.ConnectionRequests.Where(r=>r.Status==ConnectionRequestStatus.Accepted).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,ConnectionRequestStatus.Ended));report=await query.ReadAsync(1,default);
            check("unknown ended intervals cannot inherit later reciprocal messages",report.Metrics.Single(m=>m.Key=="reciprocal-message-metadata") is{Numerator:0,Denominator:5,Status:"observed-lower-bound"});
            await db.ConnectionRequests.Where(r=>r.Status==ConnectionRequestStatus.Ended).ExecuteUpdateAsync(s=>s.SetProperty(r=>r.Status,ConnectionRequestStatus.Accepted));
            var encoded=JsonSerializer.Serialize(report);check("scorecard contains no account email traits or content",ids.All(id=>!encoded.Contains(id.ToString()))&&!encoded.Contains("@example.test")&&!encoded.Contains("HomeGeoNameId")&&!encoded.Contains("Fictional private marker"));
            await db.Set<BetaJourneyDay>().Where(d=>d.AccountId==ids[4]&&d.Day==today.AddDays(-5)).ExecuteUpdateAsync(s=>s.SetProperty(d=>d.ExploreHasPeer,false));
            report=await query.ReadAsync(1,default);check("one small source cell suppresses all numeric cells",report.Metrics.Where(m=>m.Status!="not-recorded").All(m=>m.Status=="suppressed-small-cells"&&m.Numerator==null&&m.Denominator==null&&m.Percentage==null));
            config["BetaAnalytics:Enabled"]="false";report=await query.ReadAsync(1,default);
            check("disabled collection is not zero historical retention",report.Collection=="disabled"&&report.Metrics.Single(m=>m.Key=="observed-day-7").Status=="not-recorded");
            bool refused=false;try{await query.ReadAsync(13,default);}catch(ArgumentException){refused=true;}check("arbitrary old reporting windows refused",refused);
            var account=await db.Accounts.SingleAsync(a=>a.Id==ids[0]);var exported=JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db,account,clock.Now,default));
            check("own export includes only own daily observations",exported.Contains("betaJourneyObservations")&&!exported.Contains(ids[1].ToString()));
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(env).AddSingleton<TimeProvider>(clock).AddLogging().BuildServiceProvider();
            var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);var evidence=new EvidencePreservation(services,new EvidenceKeys(config,founders),new ContactHistoryKeys(config,founders));
            var old=today.AddDays(-91);foreach(var id in ids.Take(2))db.Set<BetaJourneyDay>().Add(new(){AccountId=id,Day=old,Dataset="local-synthetic"});
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional beta metadata hold",PlacedBy=staff,PlacedByRole=AccountRole.Legal,PlacedAt=clock.Now.AddDays(-100),ReviewDue=clock.Now.AddDays(1),RelatedAccountIds=[ids[0]],RetentionCaseIds=[]};
            db.LegalHolds.Add(hold);db.LegalHoldScopes.Add(new(){HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=ids[0]});await db.SaveChangesAsync();
            var retention=new BetaJourneyRetention(db,evidence,clock);check("beta TTL expiry observes live preservation",await retention.PurgeAsync(default)&&await db.Set<BetaJourneyDay>().AnyAsync(d=>d.AccountId==ids[0]&&d.Day==old)&&!await db.Set<BetaJourneyDay>().AnyAsync(d=>d.AccountId==ids[1]&&d.Day==old));
            await evidence.CaptureAsync(hold,staff,clock.Now,default);
            check("beta evidence is separately encrypted and account-scoped",await db.EvidenceItems.AnyAsync(e=>e.HoldId==hold.Id&&e.Kind=="beta-journey"&&e.AccountId==ids[0])&&!(await db.EvidenceItems.FirstAsync(e=>e.HoldId==hold.Id&&e.Kind=="beta-journey")).Sealed.SequenceEqual(Array.Empty<byte>()));
            await db.Set<BetaJourneyDay>().Where(d=>d.AccountId==ids[0]).ExecuteDeleteAsync();
            check("preserve then erase ordinary beta metadata leaves held evidence",!await db.Set<BetaJourneyDay>().AnyAsync(d=>d.AccountId==ids[0])&&await db.EvidenceItems.AnyAsync(e=>e.Kind=="beta-journey"&&e.AccountId==ids[0]));
            db.Set<BetaJourneyDay>().Add(new(){AccountId=ids[2],Day=old,Dataset="local-synthetic"});hold.RelatedAccountIds=null;await db.SaveChangesAsync();
            check("unknown preservation coverage fails closed",!await retention.PurgeAsync(default)&&await db.Set<BetaJourneyDay>().AnyAsync(d=>d.AccountId==ids[2]&&d.Day==old));
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Host:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.Parse("2026-11-16T00:00:00Z");public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new InvalidOperationException("No delivery in beta analytics fixture.");}
}
