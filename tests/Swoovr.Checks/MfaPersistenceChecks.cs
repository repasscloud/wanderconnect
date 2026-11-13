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
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class MfaPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
        var now=DateTimeOffset.UtcNow;var adminId=Guid.NewGuid();var supportId=Guid.NewGuid();var memberId=Guid.NewGuid();var staffId=Guid.NewGuid();
        var keycloak=new KeycloakOptions{BaseUrl="https://mfa.example.test"};
        Account A(Guid id,AccountRealm realm)=>new(){Id=id,Realm=realm,Issuer=keycloak.Issuer(realm==AccountRealm.Staff?keycloak.StaffRealm:keycloak.MembersRealm),Subject=Guid.NewGuid().ToString(),Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};
        var admin=A(adminId,AccountRealm.Staff);var support=A(supportId,AccountRealm.Staff);var member=A(memberId,AccountRealm.Members);var staff=A(staffId,AccountRealm.Staff);
        member.Email="fictional.mfa@example.test";member.EmailAcceptedAt=now.AddDays(-1);
        db.Accounts.AddRange(admin,support,member,staff);
        db.RoleGrants.AddRange(new RoleGrant{Id=Guid.NewGuid(),AccountId=adminId,Role=AccountRole.Admin,GrantedAt=now},new RoleGrant{Id=Guid.NewGuid(),AccountId=supportId,Role=AccountRole.Support,GrantedAt=now},new RoleGrant{Id=Guid.NewGuid(),AccountId=staffId,Role=AccountRole.Business,GrantedAt=now});
        db.MemberProfiles.Add(new MemberProfile{AccountId=memberId,AgeStatus=AgeStatus.Attested,DateOfBirth=new DateOnly(1990,1,1)});
        db.BetaEnrolments.Add(new BetaEnrolment{Id=Guid.NewGuid(),Email=member.Email,AccountId=memberId,CreatedAt=now,ActivatedAt=now});
        await db.SaveChangesAsync();
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Staff:LocalPasswords"]="true",["Phone:Required"]="false"}).Build();var mail=new FakeMail();
        using var services=new ServiceCollection().AddSingleton(db).AddSingleton(TimeProvider.System).AddSingleton<IConfiguration>(config).AddSingleton<IEmailSender>(mail).BuildServiceProvider();
        var resolver=new AccountResolver(services,keycloak,new SessionEvidencePolicy(new()),TimeProvider.System,config,new Dev(),NullLogger<AccountResolver>.Instance);
        ClaimsPrincipal User(Account a,DateTimeOffset auth,bool otp=true,string sid="owned-mfa-session")=>new(new ClaimsIdentity(new[]{new Claim("iss",a.Issuer),new Claim("sub",a.Subject),new Claim("email_verified","true"),new Claim("auth_time",auth.ToUnixTimeSeconds().ToString()),new Claim("iat",now.ToUnixTimeSeconds().ToString()),new Claim("amr","pwd"),new Claim("sid",sid),new Claim("azp","swoovr-admin-web")}.Concat(otp?[new Claim("amr","otp")]:[]),"checks"));
        var adminUser=User(admin,now.AddMinutes(-2));
        var begin=await MfaEndpoints.BeginAsync(adminUser,resolver,TimeProvider.System,config,CancellationToken.None);
        var challenge=((IValueHttpResult)begin).Value as MfaElevationChallenge??throw new Exception("challenge missing");
        var initial=await MfaEndpoints.CompleteAsync(new(challenge.Challenge),adminUser,resolver,TimeProvider.System,config,CancellationToken.None);
        check("ordinary MFA sign-in does not complete pending elevation",Status(initial)==403);
        adminUser=User(admin,DateTimeOffset.UtcNow);
        var complete=await MfaEndpoints.CompleteAsync(new(challenge.Challenge),adminUser,resolver,TimeProvider.System,config,CancellationToken.None);
        check("fresh same-user session completes independent elevation",Status(complete)==200);
        check("elevation challenge replay refused",Status(await MfaEndpoints.CompleteAsync(new(challenge.Challenge),adminUser,resolver,TimeProvider.System,config,CancellationToken.None))==403);
        var current=(await resolver.ResolveAsync(adminUser,CancellationToken.None))!;
        // D366: a fresh MFA sign-in elevates by itself, so the receipt's session binding is checked after a 1-minute sign-in window.
        var shortSignIn=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Mfa:ElevationMinutes"]="1"}).Build();
        check("the Verify MFA receipt outlasts the sign-in window on its own session",await MfaElevationService.HasAsync(current,adminUser,db,shortSignIn,now.AddMinutes(3),CancellationToken.None));
        check("another device cannot reuse elevation",!await MfaElevationService.HasAsync(current,User(admin,DateTimeOffset.UtcNow,sid:"other-device"),db,shortSignIn,now.AddMinutes(3),CancellationToken.None));
        check("expired elevation refused",!await MfaElevationService.HasAsync(current,adminUser,db,config,now.AddMinutes(61),CancellationToken.None));
        // D366 (issue 336): a sign-in that itself completed MFA counts for 60 minutes; password-only or older sign-ins do not.
        var freshUser=User(admin,now.AddMinutes(-30),sid:"fresh-sign-in");var freshCurrent=(await resolver.ResolveAsync(freshUser,CancellationToken.None))!;
        check("MFA sign-in within 60 minutes counts as elevation",await MfaElevationService.HasAsync(freshCurrent,freshUser,db,config,now,CancellationToken.None));
        check("MFA sign-in elevation ends 60 minutes after sign-in",await MfaElevationService.UntilAsync(freshCurrent,freshUser,db,config,now,CancellationToken.None) is {} freshUntil&&Math.Abs((freshUntil-now.AddMinutes(30)).TotalSeconds)<2);
        var oldUser=User(admin,now.AddMinutes(-61),sid:"old-sign-in");var oldCurrent=(await resolver.ResolveAsync(oldUser,CancellationToken.None))!;
        check("MFA sign-in older than 60 minutes needs Verify MFA",!await MfaElevationService.HasAsync(oldCurrent,oldUser,db,config,now,CancellationToken.None));
        var passwordUser=User(admin,now.AddMinutes(-1),otp:false,sid:"password-only");var passwordCurrent=(await resolver.ResolveAsync(passwordUser,CancellationToken.None))!;
        check("password-only sign-in is never elevation",!await MfaElevationService.HasAsync(passwordCurrent,passwordUser,db,config,now,CancellationToken.None));
        var photo=Guid.NewGuid();
        db.MediaItems.Add(new MediaItem{Id=photo,AccountId=memberId,Status=MediaStatus.Uncertain,CreatedAt=now,ContentType="image/jpeg",Sha256=new byte[32],UploadSha256=new byte[32],Width=300,Height=300,Bytes=100});await db.SaveChangesAsync();
        check("a staff media link alone carries no viewing authority",await Swoovr.Api.Media.MfaEvidenceAccess.CheckAsync(photo,memberId,"case",adminUser,resolver,now,CancellationToken.None) is not null);
        db.MediaReviewViews.Add(new MediaReviewView{MediaId=photo,ActorAccountId=adminId,Purpose="Fictional evidence review",At=now});await db.SaveChangesAsync();
        check("issued staff evidence link works only with live MFA elevation",await Swoovr.Api.Media.MfaEvidenceAccess.CheckAsync(photo,memberId,"case",adminUser,resolver,now,CancellationToken.None) is null);
        check("staff evidence delivery refuses expired elevation",await Swoovr.Api.Media.MfaEvidenceAccess.CheckAsync(photo,memberId,"case",adminUser,resolver,now.AddMinutes(61),CancellationToken.None) is not null);
        check("Business cannot read staff evidence even with the signed link",await Swoovr.Api.Media.MfaEvidenceAccess.CheckAsync(photo,memberId,"case",User(staff,now),resolver,now,CancellationToken.None) is not null);
        var provider=new FakeProvider();var supportUser=User(support,now.AddSeconds(-20));
        var required=await MfaEndpoints.RequireAsync(memberId,new(true,"security-review",true),supportUser,resolver,provider,TimeProvider.System,CancellationToken.None);
        check("Support can impose member MFA without an elevation receipt",Status(required)==200&&member.MfaRequired&&member.MfaRequirementRole==AccountRole.Support&&provider.Requirements.SequenceEqual([true]));
        check("requirement change cuts off sessions and requests independent notification",member.SessionsRevokedAt>now&&mail.Count==1);
        member.MfaRequirementRole=AccountRole.Admin;await db.SaveChangesAsync();
        check("Support cannot remove Admin-imposed policy",Status(await MfaEndpoints.RequireAsync(memberId,new(false,"security-review",true),supportUser,resolver,provider,TimeProvider.System,CancellationToken.None))==403);
        var reset=await MfaEndpoints.ResetAsync(staffId,new("case:1234",true,true),adminUser,resolver,provider,provider,TimeProvider.System,new TicketingStaffSignal(),CancellationToken.None);
        check("independent Admin reset keeps staff enrolment required",Status(reset)==200&&provider.ClearRequirements.Last()&&staff.MfaLastResetAt is not null&&!staff.MfaResetPending);
        check("self MFA reset refused",Status(await MfaEndpoints.ResetAsync(adminId,new("case:1234",true,true),adminUser,resolver,provider,provider,TimeProvider.System,new TicketingStaffSignal(),CancellationToken.None))==403);
        check("recovery needs verified identity attestation and case",Status(await MfaEndpoints.ResetAsync(staffId,new("case:1234",false,true),adminUser,resolver,provider,provider,TimeProvider.System,new TicketingStaffSignal(),CancellationToken.None))==400);
        provider.FailClear=true;
        reset=await MfaEndpoints.ResetAsync(memberId,new("case:1234",true,true),adminUser,resolver,provider,provider,TimeProvider.System,new TicketingStaffSignal(),CancellationToken.None);
        check("unconfirmed credential removal leaves durable recovery gate",Status(reset)==503&&member.MfaResetPending&&member.MfaRequired);
        check("MFA events retain actor target and bounded case reason",await db.AuditEntries.AnyAsync(a=>a.ActorAccountId==adminId&&a.TargetId==memberId.ToString()&&a.Action=="mfa.reset-requested"&&a.Detail!.Contains("case:1234")));
        member.SessionsRevokedAt=null;member.MfaResetPending=false;await db.SaveChangesAsync();
        var memberUser=User(member,now);
        var (blocked,denied)=await AccountEndpoints.MemberAsync(User(member,now,otp:false),resolver,CancellationToken.None);
        check("normal member functions deny a required non-MFA session",blocked is null&&Status(denied!)==403);
        var (statusAccount,statusDenied)=await AccountEndpoints.MemberAsync(User(member,now,otp:false),resolver,CancellationToken.None,ageGateExempt:true,barredExempt:true,mfaExempt:true);
        check("required member can still reach security and recovery status",statusAccount is not null&&statusDenied is null);
        check("required member cannot self-disable",Status(await MfaEndpoints.DisableAsync(memberUser,resolver,provider,TimeProvider.System,CancellationToken.None))==403);
        member.MfaRequired=false;member.MfaRequirementRole=null;await db.SaveChangesAsync();provider.FailClear=false;provider.Credentials=true;
        var disabled=await MfaEndpoints.DisableAsync(memberUser,resolver,provider,TimeProvider.System,CancellationToken.None);
        check("optional member self-disable preserves a JSON response and ends sessions",Status(disabled)==200&&((IValueHttpResult)disabled).Value is MfaView{Required:false,Status:"disabled"}&&!provider.ClearRequirements.Last()&&member.SessionsRevokedAt>now);
    }
    private static int? Status(IResult r)=>r is Microsoft.AspNetCore.Http.HttpResults.ForbidHttpResult?403:(r as IStatusCodeHttpResult)?.StatusCode;
    private sealed class Dev:IHostEnvironment{public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Checks";public string ContentRootPath{get;set;}="/tmp";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class FakeMail:IEmailSender{public int Count;public Task<bool> SendAsync(EmailMessage m,CancellationToken ct){Count++;return Task.FromResult(true);}}
    private sealed class FakeProvider:IStaffIdentityAdmin
    {
        public bool Configured=>true;public bool FailClear,Credentials=true;public List<bool> Requirements=[],ClearRequirements=[];
        public Task<bool> SetMfaRequiredAsync(string s,bool required,CancellationToken ct){Requirements.Add(required);return Task.FromResult(true);}
        public Task<bool> ClearMfaAsync(string s,bool required,CancellationToken ct){ClearRequirements.Add(required);if(!FailClear)Credentials=false;return Task.FromResult(!FailClear);}
        public Task<IdentityAccountInfo?> AccountAsync(string s,CancellationToken ct)=>Task.FromResult<IdentityAccountInfo?>(new(s,"Fictional","Adult",null,true,true,null,Credentials?["otp"]:[],[]));
        public Task<bool> SignOutEverywhereAsync(string s,CancellationToken ct)=>Task.FromResult(true);
        public Task<IdentitySignIns?> SignInsAsync(string s,CancellationToken ct)=>Task.FromResult<IdentitySignIns?>(null);
        public Task<string?> CreateAccountAsync(IdentityAccountUpdate u,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> UpdateAccountAsync(string s,IdentityAccountUpdate u,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> RequireActionsAsync(string s,IReadOnlyList<string>a,CancellationToken ct)=>Task.FromResult(true);
        public Task<bool> PasswordEmailAsync(string s,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> SignOutOtherSessionsAsync(string s,string? k,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> DeleteUserAsync(string s,CancellationToken ct)=>throw new NotSupportedException();
        public Task<bool> EndSessionAsync(string s,string id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<UnlinkOutcome> UnlinkProviderAsync(string s,string a,CancellationToken ct)=>throw new NotSupportedException();
    }
}
