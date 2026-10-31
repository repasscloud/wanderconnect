using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Swoovr.Api.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Policies;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

// Owned actual PostgreSQL/service fixture. Fictional approved text is injected only here, never the deployed catalogue.
static class PolicyAcceptancePersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();var name="swoovr_policy_acceptance_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            var clock=new Clock();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)41,32).ToArray())}).Build();
            const string terms="Fictional approved fixture, not real legal terms.",guidelines="Fictional approved community fixture.";
            var version=new PublicPolicyVersion("fictional-approved-1",PublicPolicyStatus.Approved,clock.Now.AddDays(-1),ApprovedPolicyDocument.Hash(terms,guidelines));
            var catalogue=new ApprovedPolicyCatalogue([new(version,terms,guidelines)]);var empty=new ApprovedPolicyCatalogue();
            check("deployed empty catalogue cannot select fictional approved fixture",empty.Resolve(version.Id,clock.Now).State==PolicyRequirementState.ConfigurationInvalid);
            var a=Guid.NewGuid();var b=Guid.NewGuid();var c=Guid.NewGuid();var staff=Guid.NewGuid();
            foreach(var id in new[]{a,b,c,staff})db.Accounts.Add(new(){Id=id,Issuer="https://policy-fixture.example.test/realms/members",Subject=id.ToString(),Email="fictional."+id+"@example.test",EmailAcceptedAt=clock.Now.AddDays(-2),Realm=id==staff?AccountRealm.Staff:AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=clock.Now.AddDays(-2),LastSeenAt=clock.Now,IsRootAdmin=id==staff});
            foreach(var id in new[]{a,b,c})db.MemberProfiles.Add(new(){AccountId=id,PublicId=Guid.NewGuid(),AgeStatus=AgeStatus.Attested,DateOfBirth=new DateOnly(1990,1,1)});
            await db.SaveChangesAsync();var service=new PolicyAcceptanceService(db,catalogue,config,clock);
            check("no selected version preserves access without inventing acceptance",(await service.StateAsync(a,default)).RequiredVersion==null&&!await db.Set<PolicyAcceptance>().AnyAsync());
            check("unselected policies cannot record acceptance",await service.AcceptAsync(a,new(version.Id,true),default)==null);
            config["Policies:RequiredVersion"]=version.Id;
            check("approved requirement is unaccepted until explicit action",await service.RequiredAsync(a,default));
            config["Beta:Enabled"]="false";config["Phone:Required"]="false";
            async Task<(CurrentAccount? Current,IResult? Denied)> Gate(bool essential=false)
            {
                using var dependencies=new ServiceCollection().AddSingleton(db).AddSingleton(service).AddSingleton<IConfiguration>(config).AddSingleton<TimeProvider>(clock).AddLogging().BuildServiceProvider();
                var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("iss","https://policy-fixture.example.test/realms/members"),new Claim("sub",a.ToString()),new Claim("email","fictional."+a+"@example.test"),new Claim("email_verified","true"),new Claim("amr","pwd"),new Claim("auth_time",clock.Now.ToUnixTimeSeconds().ToString()),new Claim("iat",clock.Now.ToUnixTimeSeconds().ToString()),new Claim("swoovr_adult_attested_at",clock.Now.AddDays(-2).ToUnixTimeSeconds().ToString())},"trusted-fictional-validated-claims"));
                var resolver=new AccountResolver(dependencies,new KeycloakOptions{BaseUrl="https://policy-fixture.example.test",MembersRealm="members"},new SessionEvidencePolicy(new()),clock,config,new Development(),NullLogger<AccountResolver>.Instance);
                return await AccountEndpoints.MemberAsync(principal,resolver,default,barredExempt:essential);
            }
            check("actual member gate refuses unaccepted selected approved fixture",(await Gate()).Denied is IStatusCodeHttpResult{StatusCode:403});
            check("actual member gate preserves essential account rights",(await Gate(true)).Denied==null);

            check("wrong version and unchecked acceptance refused",await service.AcceptAsync(a,new("other",true),default)==null&&await service.AcceptAsync(a,new(version.Id,false),default)==null&&!await db.Set<PolicyAcceptance>().AnyAsync());
            var first=await service.AcceptAsync(a,new(version.Id,true),default);var acceptedAt=first!.AcceptedAt;clock.Now=clock.Now.AddSeconds(1);
            var replay=await service.AcceptAsync(a,new(version.Id,true),default);
            check("explicit source acceptance and retry preserve server timestamp/hash",first.Accepted&&replay!.AcceptedAt==acceptedAt&&replay.ContentSha256==version.ContentSha256&&await db.Set<PolicyAcceptance>().CountAsync(p=>p.AccountId==a)==1);
            check("actual member gate opens after explicit matching acceptance",(await Gate()).Denied==null);
            check("one account acceptance never satisfies another",await service.RequiredAsync(b,default));
            check("staff cannot acquire a member acceptance record",await service.AcceptAsync(staff,new(version.Id,true),default)==null);
            check("request carries no account timestamp approval or hash substitution",typeof(PolicyAcceptanceRequest).GetProperties().Select(p=>p.Name).Order().SequenceEqual(new[]{"Confirm","Version"}));
            async Task<MemberPolicyView?> Concurrent()
            {
                await using var replica=new SwoovrDbContext(options);return await new PolicyAcceptanceService(replica,catalogue,config,clock).AcceptAsync(b,new(version.Id,true),default);
            }
            var concurrent=await Task.WhenAll(Concurrent(),Concurrent());
            check("concurrent same-account acceptance commits once",concurrent.All(r=>r is{Accepted:true})&&concurrent[0]!.AcceptedAt==concurrent[1]!.AcceptedAt&&await db.Set<PolicyAcceptance>().CountAsync(p=>p.AccountId==b)==1);
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fixture_acceptance_failure() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.action='policy.accepted' THEN RAISE EXCEPTION 'Fictional acceptance rollback';END IF;RETURN NEW;END $$; CREATE TRIGGER fixture_acceptance_failure BEFORE INSERT ON audit_entries FOR EACH ROW EXECUTE FUNCTION fixture_acceptance_failure();");
            var rolledBack=false;
            await using(var replica=new SwoovrDbContext(options))
                try{await new PolicyAcceptanceService(replica,catalogue,config,clock).AcceptAsync(c,new(version.Id,true),default);}catch(DbUpdateException error)when(error.InnerException is PostgresException{SqlState:"P0001"}){rolledBack=true;}
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fixture_acceptance_failure ON audit_entries; DROP FUNCTION fixture_acceptance_failure();");
            check("acceptance and audit rollback together",rolledBack&&!await db.Set<PolicyAcceptance>().AnyAsync(p=>p.AccountId==c));
            async Task<bool> Refused(string sql)
            {try{await db.Database.ExecuteSqlRawAsync(sql);return false;}catch(PostgresException e)when(e.SqlState=="23514"){return true;}}
            check("database acceptance history cannot be rewritten",await Refused($"UPDATE policy_acceptances SET content_sha256=repeat('b',64) WHERE account_id='{a}'"));
            check("database acceptance cannot be erased from active account",await Refused($"DELETE FROM policy_acceptances WHERE account_id='{a}'"));
            var secondVersion=new PublicPolicyVersion("fictional-approved-2",PublicPolicyStatus.Approved,clock.Now.AddHours(-1),ApprovedPolicyDocument.Hash(terms+" New version.",guidelines));
            var updated=new ApprovedPolicyCatalogue([new(version,terms,guidelines),new(secondVersion,terms+" New version.",guidelines)]);
            config["Policies:RequiredVersion"]=secondVersion.Id;var next=new PolicyAcceptanceService(db,updated,config,clock);
            check("new selected version requires a new explicit choice",await next.RequiredAsync(a,default));
            check("new version appends while preserving old acceptance",(await next.AcceptAsync(a,new(secondVersion.Id,true),default))!.Accepted&&await db.Set<PolicyAcceptance>().CountAsync(p=>p.AccountId==a)==2);
            var mismatch=new ApprovedPolicyCatalogue([new(version with{ContentSha256=ApprovedPolicyDocument.Hash(terms+" changed",guidelines)},terms+" changed",guidelines)]);
            config["Policies:RequiredVersion"]=version.Id;bool refused=false;
            try{await new PolicyAcceptanceService(db,mismatch,config,clock).StateAsync(a,default);}catch(InvalidOperationException){refused=true;}
            check("same version source digest cannot silently replace accepted text",refused);
            config["Policies:RequiredVersion"]="draft-unknown";refused=false;
            try{service.Requirement();}catch(InvalidOperationException){refused=true;}
            check("unknown draft selection fails visibly",refused);
            config["Policies:RequiredVersion"]=version.Id;
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional acceptance preservation",PlacedBy=staff,PlacedByRole=AccountRole.Legal,PlacedAt=clock.Now,ReviewDue=clock.Now.AddDays(1),RelatedAccountIds=[a],RetentionCaseIds=[]};
            db.LegalHolds.Add(hold);db.LegalHoldScopes.Add(new(){HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=a});await db.SaveChangesAsync();
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<TimeProvider>(clock).AddLogging().BuildServiceProvider();
            var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);
            var evidence=new EvidencePreservation(services,new EvidenceKeys(config,founders),new ContactHistoryKeys(config,founders));
            var account=await db.Accounts.SingleAsync(x=>x.Id==a);account.Status=AccountStatus.PendingDeletion;account.DeletionFinalizingAt=clock.Now;await db.SaveChangesAsync();
            check("pending-deletion account cannot add acceptance",await service.AcceptAsync(a,new(version.Id,true),default)==null);
            check("uncaptured held acceptance blocks source erasure before provider work",!await evidence.PolicyAcceptanceCopiesReadyAsync(a,default)&&await Refused($"DELETE FROM policy_acceptances WHERE account_id='{a}'"));
            await evidence.PreserveBeforeDeletionAsync(a,clock.Now,default);
            check("actual hold captures minimal acceptance proofs",await db.EvidenceItems.CountAsync(e=>e.HoldId==hold.Id&&e.Kind=="policy-acceptance")==2);
            check("acceptance proof is decrypted and hash-checked before identity removal",await evidence.PolicyAcceptanceCopiesReadyAsync(a,default));
            var wrongConfig=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Evidence:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)99,32).ToArray())}).Build();
            var unreadable=new EvidencePreservation(services,new EvidenceKeys(wrongConfig,founders),new ContactHistoryKeys(config,founders));
            check("unreadable preserved acceptance refuses provider deletion even with matching metadata",!await unreadable.PolicyAcceptanceCopiesReadyAsync(a,default));

            var unknown=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Released,Reason="Fictional unresolved released coverage",PlacedBy=staff,PlacedByRole=AccountRole.Legal,PlacedAt=clock.Now.AddDays(-2),ReviewDue=clock.Now.AddDays(-1),ReleasedAt=clock.Now,ReleasedBy=staff,ReleaseReason="Fictional legacy release",RelatedAccountIds=null,RetentionCaseIds=[]};
            db.LegalHolds.Add(unknown);await db.SaveChangesAsync();
            check("unresolved released coverage blocks identity removal and source erasure",!await evidence.PolicyAcceptanceCopiesReadyAsync(a,default)&&await Refused($"DELETE FROM policy_acceptances WHERE account_id='{a}'"));
            unknown.RelatedAccountIds=[];await db.SaveChangesAsync();
            await db.Set<PolicyAcceptance>().Where(p=>p.AccountId==a).ExecuteDeleteAsync();
            check("final deletion clears ordinary acceptance while active hold copies remain",!await db.Set<PolicyAcceptance>().AnyAsync(p=>p.AccountId==a)&&await db.EvidenceItems.CountAsync(e=>e.HoldId==hold.Id&&e.Kind=="policy-acceptance")==2&&hold.Status==LegalHoldStatus.Active);
            check("preserved acceptance contains no approved document text",(await db.EvidenceItems.Where(e=>e.HoldId==hold.Id&&e.Kind=="policy-acceptance").ToListAsync()).All(e=>!evidence.Open(e).Contains(terms)));
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Development:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new InvalidOperationException("No delivery in acceptance fixture.");}
}
