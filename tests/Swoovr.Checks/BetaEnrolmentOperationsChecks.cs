using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Messaging;
using Swoovr.Infrastructure.Persistence;

static class BetaEnrolmentOperationsChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();var name="swoovr_beta_ops_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync("20261030161747_BetaJourneyObservations");
            var oldId=Guid.NewGuid();var now=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO beta_enrolments(id,email,created_at) VALUES({oldId},{"fictional.beta-old@example.test"},{now})");
            await db.Database.MigrateAsync();
            var old=await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==oldId);
            check("beta upgrade preserves existing enrolment without inventing Apple history",old.IosDistributionState=="not-recorded"&&old.IosDistributionRevision==0&&old.IosDistributionChangedAt==null&&old.IosDistributionChangedBy==null);
            var member=Guid.NewGuid();var staff=Guid.NewGuid();
            foreach(var id in new[]{member,staff})db.Accounts.Add(new(){Id=id,Issuer="https://beta-ops.example.test/realms/members",Subject=id.ToString(),Email="fictional."+id+"@example.test",Realm=id==staff?AccountRealm.Staff:AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});
            db.MemberProfiles.Add(new(){AccountId=member,PublicId=Guid.NewGuid(),DateOfBirth=new(1990,1,1),AgeStatus=AgeStatus.SelfDeclaredAdult});await db.SaveChangesAsync();
            await db.BetaEnrolments.Where(e=>e.Id==oldId).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.AccountId,member).SetProperty(e=>e.ActivatedAt,now));
            foreach(var request in new[]{new IosDistributionUpdate("verified",0,true),new("invited",-1,true),new("invited",long.MaxValue,true),new("invited",0,false)})
                check("beta iOS update rejects unsupported unconfirmed or exhausted revision",(await BetaEnrolments.RecordIosAsync(db,oldId,request,staff,now,default)).Result==BetaEnrolments.IosUpdateResult.Invalid);
            var changed=await BetaEnrolments.RecordIosAsync(db,oldId,new("invited",0,true),staff,now,default);
            check("beta staff iOS record gets server revision actor and time",changed is{Result:BetaEnrolments.IosUpdateResult.Updated,Row.IosDistributionRevision:1}&&changed.Row.IosDistributionChangedAt==now&&changed.Row.IosDistributionChangedBy==staff);
            check("beta API view never claims provider verification",BetaEnrolments.View(changed.Row!).IosDistributionSource=="staff-recorded-not-provider-verified");
            check("stale beta form cannot overwrite a newer iOS record",(await BetaEnrolments.RecordIosAsync(db,oldId,new("accepted",0,true),staff,now.AddSeconds(1),default)).Result==BetaEnrolments.IosUpdateResult.Conflict);
            var same=await BetaEnrolments.RecordIosAsync(db,oldId,new("invited",1,true),staff,now.AddSeconds(1),default);
            check("same confirmed beta status is a no-op with original time",same.Result==BetaEnrolments.IosUpdateResult.Unchanged&&same.Row!.IosDistributionRevision==1&&same.Row.IosDistributionChangedAt==now);
            async Task<BetaEnrolments.IosUpdateResult> Concurrent(string status)
            {await using var other=new SwoovrDbContext(options);return(await BetaEnrolments.RecordIosAsync(other,oldId,new(status,1,true),staff,now.AddSeconds(2),default)).Result;}
            var races=await Task.WhenAll(Concurrent("accepted"),Concurrent("removed"));
            check("concurrent iOS records have one CAS winner and one conflict",races.Count(r=>r==BetaEnrolments.IosUpdateResult.Updated)==1&&races.Count(r=>r==BetaEnrolments.IosUpdateResult.Conflict)==1);
            check("iOS metadata never changes beta web access state",(await db.BetaEnrolments.AsNoTracking().SingleAsync(e=>e.Id==oldId)) is{RevokedAt:null,AccountId:not null,ActivatedAt:not null});
            check("unknown enrolment is not created by distribution recording",(await BetaEnrolments.RecordIosAsync(db,Guid.NewGuid(),new("invited",0,true),staff,now,default)).Result==BetaEnrolments.IosUpdateResult.NotFound);
            await new AppearanceStore(db,TimeProvider.System).SaveAsync(member,new(new("coral","system"),0,Guid.NewGuid()),default);
            var encoded=JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db,await db.Accounts.SingleAsync(a=>a.Id==member),now,default));
            check("own data includes private appearance metadata",encoded.Contains("appearance")&&encoded.Contains("coral"));
            check("own data includes enrolment and iOS metadata without staff actor",encoded.Contains("betaEnrolments")&&encoded.Contains("IosDistributionState")&&!encoded.Contains(staff.ToString()));
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)43,32).ToArray())}).Build();
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(new Host()).AddSingleton(TimeProvider.System).AddLogging().BuildServiceProvider();
            check("current beta audience allows the linked enrolled member",await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            await db.BetaEnrolments.Where(e=>e.Id==oldId).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,now));
            check("revocation denies message audience without trusting an old hub",!await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            var notifier=new Swoovr.Api.Notifications.Notifier(services,new NeverPush(),new Sink(),NullLogger<Swoovr.Api.Notifications.Notifier>.Instance);
            await notifier.NotifyAsync(member,NotificationKind.Message,staff,null,null,now,default,text:"Fictional message preview");
            check("revoked beta cannot create new message notification delivery",!await db.Notifications.AnyAsync(n=>n.AccountId==member&&n.Kind==NotificationKind.Message));
            await db.Database.ExecuteSqlRawAsync("UPDATE registration_availability SET beta_enabled=false,public_enabled=true,revision=revision+1 WHERE id=1");
            check("public launch beta-off switch disables audience gate",await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            await db.Database.ExecuteSqlRawAsync("UPDATE registration_availability SET beta_enabled=true,public_enabled=false,revision=revision+1 WHERE id=1");
            await db.BetaEnrolments.Where(e=>e.Id==oldId).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,(DateTimeOffset?)null));
            // PR #195 review: an open hub connection must stop receiving once a sign-in lock is placed (D323).
            var lockId=Guid.NewGuid();
            db.AccountLocks.Add(new AccountLock{Id=lockId,AccountId=member,ReasonCode="security-incident",CaseReference="SW-CHK001",CreatedAt=now,CreatedBy=staff,CreatedByRole=AccountRole.TrustSafety});
            await db.SaveChangesAsync();
            check("an active sign-in lock denies message audience without trusting an old hub",!await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            await db.AccountLocks.Where(l=>l.Id==lockId).ExecuteUpdateAsync(set=>set.SetProperty(l=>l.RemovedAt,(DateTimeOffset?)now));
            check("a removed sign-in lock restores message audience",await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);var evidence=new EvidencePreservation(services,new EvidenceKeys(config,founders),new ContactHistoryKeys(config,founders));
            var expiry=new BetaEnrolmentRetention(db,evidence,TimeProvider.System);
            var due=(await BetaEnrolments.AddAsync(db,"fictional.expiry-due@example.test",null,now.AddDays(-91),default)).Enrolment!;
            var young=(await BetaEnrolments.AddAsync(db,"fictional.expiry-young@example.test",null,now.AddDays(-89),default)).Enrolment!;
            var recentlyRevoked=(await BetaEnrolments.AddAsync(db,"fictional.expiry-revoked@example.test",null,now.AddDays(-100),default)).Enrolment!;
            await db.BetaEnrolments.Where(e=>e.Id==recentlyRevoked.Id).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,now.AddDays(-89)));
            config["Evidence:HoldCheckUnavailable"]="true";
            check("beta expiry refuses when preservation status is unavailable",!await expiry.PurgeAsync(default)&&await db.BetaEnrolments.AnyAsync(e=>e.Id==due.Id));
            config["Evidence:HoldCheckUnavailable"]="false";
            check("due unlinked beta records expire under known preservation authority",await expiry.PurgeAsync(default)&&!await db.BetaEnrolments.AnyAsync(e=>e.Id==due.Id));
            check("beta expiry uses revocation clock and keeps recent unlinked records",await db.BetaEnrolments.AnyAsync(e=>e.Id==young.Id)&&await db.BetaEnrolments.AnyAsync(e=>e.Id==recentlyRevoked.Id));
            var heldUnlinked=(await BetaEnrolments.AddAsync(db,"fictional.expiry-held@example.test",null,now.AddDays(-91),default)).Enrolment!;
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional beta record hold",PlacedBy=staff,PlacedByRole=AccountRole.Legal,PlacedAt=now,ReviewDue=now.AddDays(1),RelatedAccountIds=[member],RetentionCaseIds=[]};
            db.LegalHolds.Add(hold);db.LegalHoldScopes.Add(new(){HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=member});await db.SaveChangesAsync();await evidence.CaptureAsync(hold,staff,now,default);
            check("unlinked beta expiry conservatively defers during an account hold",await expiry.PurgeAsync(default)&&await db.BetaEnrolments.AnyAsync(e=>e.Id==heldUnlinked.Id));
            await db.BetaEnrolments.Where(e=>e.Id==oldId).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,now.AddDays(-91)));
            check("held linked revoked beta sources are not expired",await expiry.PurgeAsync(default)&&await db.BetaEnrolments.AnyAsync(e=>e.Id==oldId));
            await db.LegalHolds.Where(h=>h.Id==hold.Id).ExecuteUpdateAsync(set=>set.SetProperty(h=>h.Status,LegalHoldStatus.Released).SetProperty(h=>h.ReleasedAt,now));
            check("retained beta evidence continues to defer source expiry after release",await expiry.PurgeAsync(default)&&await db.BetaEnrolments.AnyAsync(e=>e.Id==oldId)&&await db.BetaEnrolments.AnyAsync(e=>e.Id==heldUnlinked.Id));
            check("private appearance is independently captured as encrypted evidence",await db.EvidenceItems.AnyAsync(e=>e.Kind=="appearance"&&e.AccountId==member));
            await db.Set<MemberAppearance>().Where(a=>a.AccountId==member).ExecuteDeleteAsync();
            check("appearance source erasure preserves its independent encrypted copy",!await db.Set<MemberAppearance>().AnyAsync(a=>a.AccountId==member)&&await db.EvidenceItems.AnyAsync(e=>e.Kind=="appearance"&&e.AccountId==member));
            check("linked beta enrolment is captured as encrypted independent evidence",await db.EvidenceItems.AnyAsync(e=>e.Kind=="beta-enrolment"&&e.AccountId==member&&e.RecordId==oldId.ToString()));
            await db.BetaEnrolments.Where(e=>e.AccountId==member).ExecuteDeleteAsync();
            check("ordinary beta erasure keeps preserved enrolment evidence",!await db.BetaEnrolments.AnyAsync(e=>e.AccountId==member)&&await db.EvidenceItems.AnyAsync(e=>e.Kind=="beta-enrolment"&&e.RecordId==oldId.ToString()));
            // Q98: real database filtering precedes bounded pagination, including beyond the legacy1000 cap.
            await db.MemberProfiles.Where(p=>p.AccountId==member).ExecuteUpdateAsync(set=>set.SetProperty(p=>p.DisplayName,"Fictional % Under_score Adult"));
            var created=new DateTimeOffset(2026,10,1,12,0,0,TimeSpan.Zero);
            var paging=Enumerable.Range(0,1005).Select(i=>new BetaEnrolment{Id=Guid.NewGuid(),Email=$"fictional.paging.{i:D4}@example.test",CreatedAt=created,
                AccountId=i==0?member:null,ActivatedAt=i==0?created:null,RevokedAt=i==1?created:null}).ToArray();
            db.BetaEnrolments.AddRange(paging);await db.SaveChangesAsync();
            Task<BetaEnrolmentPage?> Search(string? q="fictional.paging.",string? state=null,string? sort=null,DateOnly? from=null,DateOnly? to=null,int page=1,int size=25)
                =>BetaEnrolments.SearchAsync(db,q,state,sort,from,to,page,size,default);
            var first=(await Search())!;var second=(await Search(page:2))!;
            check("beta search totals are uncapped while pages are bounded",first.Total==1005&&first.Items.Count==25&&first.Page==1&&first.Size==25);
            check("beta search stable ties give disjoint repeatable pages",!first.Items.Select(e=>e.Id).Intersect(second.Items.Select(e=>e.Id)).Any()&&first.Items.Select(e=>e.Id).SequenceEqual((await Search())!.Items.Select(e=>e.Id)));
            check("beta search filters before the old list cap",(await Search("FICTIONAL.PAGING.1004")) is{Total:1,Items.Count:1});
            var named=await Search("under_score");
            check("beta names use canonical account links and literal substring matching",named is{Total:1}&&named.Items[0].Id==paging[0].Id&&named.Items[0].MemberName=="Fictional % Under_score Adult"&&(await Search("%")) is{Total:1});
            check("beta search supports enrolment and linked account references",(await Search(paging[1004].Id.ToString())) is{Total:1}&&(await Search(member.ToString())) is{Total:1});
            check("beta pending activated and revoked filters remain independent",(await Search(state:"pending")) is{Total:1003}&&(await Search(state:"activated")) is{Total:1}&&(await Search(state:"revoked")) is{Total:1});
            check("beta created-date bounds are inclusive UTC dates",(await Search(from:new(2026,10,1),to:new(2026,10,1))) is{Total:1005}&&(await Search(from:new(2026,10,2))) is{Total:0,Page:1,Items.Count:0});
            check("beta page overflow clamps to last page without arithmetic overflow",(await Search(page:int.MaxValue)) is{Page:41,Items.Count:5});
            check("beta selectable sizes and sort apply in the database",(await Search(sort:"email",size:50)) is{Items.Count:50} sorted&&sorted.Items[0].Email.EndsWith("0000@example.test"));
            check("beta search rejects unsupported filters and reversed dates",await Search(state:"verified") is null&&await Search(sort:"unexpected") is null&&await Search(size:1000) is null&&await Search(page:0) is null&&await Search(from:new(2026,10,2),to:new(2026,10,1)) is null&&await Search(new string('x',255)) is null);
            var address=await db.Accounts.Where(a=>a.Id==member).Select(a=>a.Email).SingleAsync();
            db.BetaEnrolments.Add(new(){Id=Guid.NewGuid(),Email=address!,CreatedAt=created});await db.SaveChangesAsync();
            check("matching email never fabricates a member-name relationship",(await Search(address)) is{Total:1} unlinked&&unlinked.Items[0].MemberName is null);

        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Host:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class NeverPush:IPushSender{public string Kind=>"fixture-never-send";public Task<PushOutcome> SendAsync(PushMessage message,CancellationToken token)=>throw new InvalidOperationException("No push delivery in beta fixture.");}
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new InvalidOperationException("No delivery in beta enrolment fixture.");}
}
