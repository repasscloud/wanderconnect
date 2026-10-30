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
            var encoded=JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db,await db.Accounts.SingleAsync(a=>a.Id==member),now,default));
            check("own data includes enrolment and iOS metadata without staff actor",encoded.Contains("betaEnrolments")&&encoded.Contains("IosDistributionState")&&!encoded.Contains(staff.ToString()));
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)43,32).ToArray())}).Build();
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(new Host()).AddSingleton(TimeProvider.System).AddLogging().BuildServiceProvider();
            check("current beta audience allows the linked enrolled member",await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            await db.BetaEnrolments.Where(e=>e.Id==oldId).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,now));
            check("revocation denies message audience without trusting an old hub",!await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            var notifier=new Swoovr.Api.Notifications.Notifier(services,new NeverPush(),new Sink(),NullLogger<Swoovr.Api.Notifications.Notifier>.Instance);
            await notifier.NotifyAsync(member,NotificationKind.Message,staff,null,null,now,default,text:"Fictional message preview");
            check("revoked beta cannot create new message notification delivery",!await db.Notifications.AnyAsync(n=>n.AccountId==member&&n.Kind==NotificationKind.Message));
            config["Beta:Enabled"]="false";
            check("public launch beta-off switch disables audience gate",await Swoovr.Api.Connections.BetaMessageAudience.AllowsAsync(services,member,default));
            config["Beta:Enabled"]="true";
            await db.BetaEnrolments.Where(e=>e.Id==oldId).ExecuteUpdateAsync(set=>set.SetProperty(e=>e.RevokedAt,(DateTimeOffset?)null));
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
            check("linked beta enrolment is captured as encrypted independent evidence",await db.EvidenceItems.AnyAsync(e=>e.Kind=="beta-enrolment"&&e.AccountId==member&&e.RecordId==oldId.ToString()));
            await db.BetaEnrolments.Where(e=>e.AccountId==member).ExecuteDeleteAsync();
            check("ordinary beta erasure keeps preserved enrolment evidence",!await db.BetaEnrolments.AnyAsync(e=>e.AccountId==member)&&await db.EvidenceItems.AnyAsync(e=>e.Kind=="beta-enrolment"&&e.RecordId==oldId.ToString()));
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Host:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class NeverPush:IPushSender{public string Kind=>"fixture-never-send";public Task<PushOutcome> SendAsync(PushMessage message,CancellationToken token)=>throw new InvalidOperationException("No push delivery in beta fixture.");}
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new InvalidOperationException("No delivery in beta enrolment fixture.");}
}
