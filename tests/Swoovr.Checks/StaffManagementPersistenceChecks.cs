using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Auth;
using Swoovr.Api.Ticketing;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class StaffManagementPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string,bool> check)
    {
        await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var actor=Guid.NewGuid();var target=Guid.NewGuid();
        var keycloak=new KeycloakOptions{BaseUrl="https://auth.example.test"};
        var issuer=keycloak.Issuer(keycloak.StaffRealm);var subject=Guid.NewGuid().ToString();
        db.Accounts.AddRange(new Account{Id=actor,Issuer=issuer,Subject=subject,Realm=AccountRealm.Staff,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now},
            new Account{Id=target,Issuer=issuer,Subject=Guid.NewGuid().ToString(),Realm=AccountRealm.Staff,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now,Email="fictional.target@example.test"});
        db.RoleGrants.AddRange(new RoleGrant{Id=Guid.NewGuid(),AccountId=actor,Role=AccountRole.Admin,GrantedAt=now},new RoleGrant{Id=Guid.NewGuid(),AccountId=target,Role=AccountRole.Support,GrantedAt=now});
        db.Invitations.Add(new Invitation{Id=Guid.NewGuid(),Realm=AccountRealm.Staff,Email="fictional.target@example.test",Role=AccountRole.Admin,CreatedAt=now,ExpiresAt=now.AddDays(1)});
        await db.SaveChangesAsync();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Staff:LocalPasswords"]="true"}).Build();
        using var services=new ServiceCollection().AddSingleton(db).AddSingleton(TimeProvider.System).BuildServiceProvider();
        var resolver=new AccountResolver(services,keycloak,new SessionEvidencePolicy(new()),TimeProvider.System,config,new DevEnvironment(),NullLogger<AccountResolver>.Instance);
        var user=new ClaimsPrincipal(new ClaimsIdentity([new("iss",issuer),new("sub",subject),new("email_verified","true"),new("auth_time",now.AddSeconds(-3).ToUnixTimeSeconds().ToString()),new("iat",now.ToUnixTimeSeconds().ToString()),new("amr","pwd"),new("amr","otp")],"checks"));
        var provider=new FakeStaff();var signal=new TicketingStaffSignal();
        var request=new StaffAccountUpdate("Fictional","Employee","fictional.target@example.test",null,"active",["support","business"],0,"role-change",true);
        var result=await StaffAccountEndpoints.UpdateAsync(target,request,user,resolver,provider,TimeProvider.System,signal,CancellationToken.None);
        check("staff update with multiple roles confirmed",Status(result)==200);
        check("staff roles replaced from application request",await db.RoleGrants.CountAsync(g=>g.AccountId==target)==2&&await db.RoleGrants.AnyAsync(g=>g.AccountId==target&&g.Role==AccountRole.Business));
        check("staff update revokes pending invitation restoring older roles",!await db.Invitations.AnyAsync(i=>i.Email=="fictional.target@example.test"&&i.RevokedAt==null));
        var account=await db.Accounts.SingleAsync(a=>a.Id==target);
        check("staff update persists revision and session cutoff",account.StaffRevision==1&&account.SessionsRevokedAt>now&&provider.Logouts==1);
        result=await StaffAccountEndpoints.UpdateAsync(target,request,user,resolver,provider,TimeProvider.System,signal,CancellationToken.None);
        check("stale staff form cannot overwrite later edit",Status(result)==409&&provider.Updates==1);
        provider.Fail=true;
        result=await StaffAccountEndpoints.UpdateAsync(target,request with{ExpectedRevision=1},user,resolver,provider,TimeProvider.System,signal,CancellationToken.None);
        check("provider uncertainty reported as unavailable",Status(result)==503);
        await db.Entry(account).ReloadAsync();
        check("provider uncertainty leaves durable staff access locked",account.Status==AccountStatus.Locked&&account.StaffRevision==2&&account.StatusReason=="staff-update-pending");
        check("staff security changes retain actor target and reason",await db.AuditEntries.AnyAsync(a=>a.ActorAccountId==actor&&a.TargetId==target.ToString()&&a.Action=="staff.update-requested"&&a.Detail=="role-change"));
        provider.Fail=false;
        result=await StaffAccountEndpoints.UpdateAsync(target,request with{ExpectedRevision=2,Roles=["legal"]},user,resolver,provider,TimeProvider.System,signal,CancellationToken.None);
        check("nonroot cannot appoint Legal through direct API orchestration",Status(result)==403);
        result=await StaffAccountEndpoints.UpdateAsync(actor,request with{ExpectedRevision=0,Roles=["admin","business"]},user,resolver,provider,TimeProvider.System,signal,CancellationToken.None);
        check("self staff role mutation refused",Status(result)==403);
        await db.RoleGrants.Where(g=>g.AccountId==actor).ExecuteDeleteAsync();
        db.RoleGrants.Add(new RoleGrant{Id=Guid.NewGuid(),AccountId=actor,Role=AccountRole.Business,GrantedAt=now});await db.SaveChangesAsync();
        var before=provider.Logouts;
        result=await StaffAccountEndpoints.UpdateAsync(target,request with{ExpectedRevision=2},user,resolver,provider,TimeProvider.System,signal,CancellationToken.None);
        check("Business direct management request denied without provider side effects",Status(result)==403&&provider.Logouts==before);
        await db.RoleGrants.Where(g=>g.AccountId==actor).ExecuteDeleteAsync();
        db.RoleGrants.Add(new RoleGrant{Id=Guid.NewGuid(),AccountId=actor,Role=AccountRole.Admin,GrantedAt=now});await db.SaveChangesAsync();
        var race=new CutoffRace(options,actor);
        await using var raceDb=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>(options).AddInterceptors(race).Options);
        using var raceServices=new ServiceCollection().AddSingleton(raceDb).AddSingleton(TimeProvider.System).BuildServiceProvider();
        var raceResolver=new AccountResolver(raceServices,keycloak,new SessionEvidencePolicy(new()),TimeProvider.System,config,new DevEnvironment(),NullLogger<AccountResolver>.Instance);
        var authorization=await StaffAccountEndpoints.AuthorizeAsync(user,raceResolver,true,CancellationToken.None);
        check("revocation between resolution and actor reload cannot preserve staff authority",race.Fired&&authorization.Actor is null&&authorization.Denied is Microsoft.AspNetCore.Http.HttpResults.UnauthorizedHttpResult);

    }
    private sealed class CutoffRace(DbContextOptions<SwoovrDbContext> options,Guid actor):DbCommandInterceptor
    {
        public bool Fired;
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command,CommandExecutedEventData eventData,DbDataReader result,CancellationToken ct=default)
        {
            if(!Fired&&command.CommandText.Contains("FROM role_grants"))
            {
                Fired=true;await using var other=new SwoovrDbContext(options);
                await other.Accounts.Where(a=>a.Id==actor).ExecuteUpdateAsync(u=>u.SetProperty(a=>a.SessionsRevokedAt,DateTimeOffset.UtcNow.AddSeconds(30)),ct);
            }
            return result;
        }
    }
    private static int? Status(IResult result)=>result is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult ? 403 : (result as IStatusCodeHttpResult)?.StatusCode;
    private sealed class DevEnvironment:IHostEnvironment {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Checks";public string ContentRootPath{get;set;}="/tmp";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class FakeStaff:IStaffIdentityAdmin
    {
        public bool Fail;public int Logouts,Updates;public bool Configured=>true;
        public Task<bool> SignOutEverywhereAsync(string s,CancellationToken ct){Logouts++;return Task.FromResult(!Fail);}
        public Task<bool> UpdateAccountAsync(string s,IdentityAccountUpdate u,CancellationToken ct){Updates++;return Task.FromResult(!Fail);}
        public Task<IdentityAccountInfo?> AccountAsync(string s,CancellationToken ct)=>Task.FromResult<IdentityAccountInfo?>(null);
        public Task<string?> CreateAccountAsync(IdentityAccountUpdate u,CancellationToken ct)=>Task.FromResult<string?>(Guid.NewGuid().ToString());
        public Task<bool> RequireActionsAsync(string s,IReadOnlyList<string>a,CancellationToken ct)=>Task.FromResult(!Fail);
        public Task<bool> PasswordEmailAsync(string s,CancellationToken ct)=>Task.FromResult(!Fail);
        public Task<bool> SignOutOtherSessionsAsync(string s,string? k,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> DeleteUserAsync(string s,CancellationToken ct)=>throw new NotSupportedException();
        public Task<IdentitySignIns?> SignInsAsync(string s,CancellationToken ct)=>Task.FromResult<IdentitySignIns?>(null);
        public Task<bool> EndSessionAsync(string s,string id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<UnlinkOutcome> UnlinkProviderAsync(string s,string a,CancellationToken ct)=>throw new NotSupportedException();
    }
}
