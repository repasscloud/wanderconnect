using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using static Swoovr.Api.Accounts.AccountEndpoints;
namespace Swoovr.Api.Accounts;

public static class MfaEndpoints
{
    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/me/mfa").RequireAuthorization().RequireRateLimiting("mfa-security");
        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, IIdentityDirectory provider, CancellationToken ct) =>
        {
            var (current, denied) = await MemberAsync(user,accounts,ct,ageGateExempt:true,barredExempt:true,mfaExempt:true); if (denied is not null) return denied;
            return await ViewAsync(current!.Account!, provider, current.Evidence.MultiFactor ? current.Facts.AuthenticatedAt : null, ct, accounts.Services.GetRequiredService<IConfiguration>().GetValue("Mfa:RecoveryCodesEnabled",false));
        });
        me.MapPost("/disable", DisableAsync);
        var admin = app.MapGroup("/api/admin/mfa").RequireAuthorization().RequireRateLimiting("mfa-security");
        admin.MapGet("/{id:guid}", async (Guid id, ClaimsPrincipal user, AccountResolver accounts, IIdentityDirectory members, IStaffIdentityAdmin staff, CancellationToken ct) =>
        {
            var (actor,denied) = await ActorAsync(user,accounts,ct); if(denied is not null)return denied;
            var target=await accounts.Db!.Accounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id,ct);
            if(target is null)return Results.NotFound();
            if(target.Realm==AccountRealm.Staff&&!actor!.Allows(AccountRole.Admin))return Results.Forbid();
            if(target.Realm is not(AccountRealm.Staff or AccountRealm.Members))return Results.NotFound();
            if(await StaffAccessEndpoints.ExcludedAsync(accounts.Db,actor!.Account!.Id,[id],ct))return StaffAccessEndpoints.Conflict();
            return await ViewAsync(target,target.Realm==AccountRealm.Staff?staff:members,null,ct);
        });
        admin.MapPut("/{id:guid}/requirement", RequireAsync);
        admin.MapPost("/{id:guid}/reset", ResetAsync);
        admin.MapPost("/elevation/begin", BeginAsync);
        admin.MapPost("/elevation/complete", CompleteAsync);
        admin.MapGet("/elevation", async(ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,IConfiguration config,CancellationToken ct)=>
        {
            var(current,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
            return Results.Ok(new MfaElevationView(await MfaElevationService.HasAsync(current!,user,accounts.Db!,config,clock.GetUtcNow(),ct),null));
        });
    }

    public static async Task<IResult> BeginAsync(ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,IConfiguration config,CancellationToken ct)
    {
        var(current,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
        if(!current!.Allows(AccountRole.Admin)&&!current.Allows(AccountRole.TrustSafety))return Results.Forbid();
        var session=MfaElevationService.Session(user);var client=MfaElevationService.Client(user,config);
        if(session is null||client is null)return MfaElevationService.Required();
        var db=accounts.Db!;var actor=current.Account!.Id;
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,actor,ct);
        (current,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
        if(!current!.Allows(AccountRole.Admin)&&!current.Allows(AccountRole.TrustSafety))return Results.Forbid();
        var now=clock.GetUtcNow();
        await db.Set<MfaElevation>().Where(e=>e.ChallengeExpiresAt<now&&(e.ExpiresAt==null||e.ExpiresAt<now)).ExecuteDeleteAsync(ct);
        if(await db.Set<MfaElevation>().CountAsync(e=>e.AccountId==actor&&e.CompletedAt==null&&e.ChallengeExpiresAt>now,ct)>=3)
            return Problem(429,ProblemCodes.TooManyAttempts,"Complete an existing verification request or wait five minutes.");
        // Supersede pending requests from this session; active receipts expire independently.
        await db.Set<MfaElevation>().Where(e=>e.AccountId==actor&&e.SessionDigest==session&&e.CompletedAt==null).ExecuteDeleteAsync(ct);
        var challenge=WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var row=new MfaElevation{Id=Guid.NewGuid(),AccountId=actor,ChallengeDigest=MfaPolicy.Digest(challenge),SessionDigest=session,ClientId=client,
            InitialAuthenticationAt=current.Facts.AuthenticatedAt,RequestedAt=now,ChallengeExpiresAt=now.AddMinutes(5)};
        db.Add(row);MfaElevationService.Audit(db,now,actor,null,"elevation-requested","pending");await db.SaveChangesAsync(ct);
        return Results.Ok(new MfaElevationChallenge(challenge,row.ChallengeExpiresAt));
    }
    public static async Task<IResult> CompleteAsync(MfaElevationComplete request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,IConfiguration config,CancellationToken ct)
    {
        var(current,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
        if(request.Challenge is not {Length:43}||request.Challenge.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('-' or '_')))return Results.Forbid();
        if(!current!.Allows(AccountRole.Admin)&&!current.Allows(AccountRole.TrustSafety))return Results.Forbid();
        var db=accounts.Db!;var actor=current.Account!.Id;
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,actor,ct);
        (current,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
        if(!current!.Allows(AccountRole.Admin)&&!current.Allows(AccountRole.TrustSafety))return Results.Forbid();
        var now=clock.GetUtcNow();var digest=MfaPolicy.Digest(request.Challenge);
        var row=await db.Set<MfaElevation>().SingleOrDefaultAsync(e=>e.AccountId==actor&&e.ChallengeDigest==digest,ct);
        if(row is null||!MfaPolicy.CanComplete(row,actor,MfaElevationService.Session(user)??"",MfaElevationService.Client(user,config)??"",current.Facts.AuthenticatedAt,current.Evidence.MultiFactor,now))
        {
            MfaElevationService.Audit(db,now,actor,null,"elevation-denied","refused");await db.SaveChangesAsync(ct);return MfaElevationService.Required();
        }
        var minutes=Math.Clamp(config.GetValue("Mfa:ElevationMinutes",10),1,15);
        row.CompletedAt=now;row.AuthenticatedAt=current.Facts.AuthenticatedAt;row.ExpiresAt=now.AddMinutes(minutes);
        MfaElevationService.Audit(db,now,actor,null,"elevation-completed","accepted");await db.SaveChangesAsync(ct);
        return Results.Ok(new MfaElevationView(true,row.ExpiresAt));
    }

    public static async Task<IResult> RequireAsync(Guid id,MfaChange request,ClaimsPrincipal user,AccountResolver accounts,IIdentityDirectory provider,TimeProvider clock,CancellationToken ct)
    {
        var(actor,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
        if(!request.Confirm||!StaffAccountEndpoints.ValidReason(request.Reason))return Invalid();
        var db=accounts.Db!;
        await using var lifecycle=await AccountLifecycleLease.AcquireManyAsync(db,[actor!.Account!.Id,id],ct);
        (actor,denied)=await ActorAsync(user,accounts,ct);if(denied is not null)return denied;
        if(!actor!.RecentlyAuthenticated(clock.GetUtcNow()))return Problem(403,ProblemCodes.StaleSession,"Sign in again before changing MFA policy.");
        var role=actor.StaffRole;
        var target=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==id&&a.Realm==AccountRealm.Members,ct);
        if(target is null)return Results.NotFound();await db.Entry(target).ReloadAsync(ct);
        if(!MfaPolicy.RequirementMayChange(role,target.MfaRequired?target.MfaRequirementRole:null))return Results.Forbid();
        if(await StaffAccessEndpoints.ExcludedAsync(db,actor.Account!.Id,[id],ct))return StaffAccessEndpoints.Conflict();
        if(!provider.Configured)return Unavailable();
        if(actor.Allows(AccountRole.Admin)||actor.Allows(AccountRole.TrustSafety))
            if(!await MfaElevationService.HasAsync(actor,user,db,accounts.Services.GetRequiredService<IConfiguration>(),clock.GetUtcNow(),ct))return MfaElevationService.Required();
        var now=clock.GetUtcNow();
        // Keep existing enforcement until provider removal is confirmed; additions block application use first.
        if(request.Required){target.MfaRequired=true;target.MfaRequirementRole=role;}
        target.SessionsRevokedAt=Cutoff(target.SessionsRevokedAt,now);
        MfaElevationService.Audit(db,now,actor.Account.Id,id,"requirement-requested",request.Required?"required":"remove",request.Reason);
        await db.SaveChangesAsync(ct);
        accounts.Services.GetService<Swoovr.Api.Connections.MessagesConnections>()?.Retire(id,target.SessionsRevokedAt,clock.GetUtcNow());
        var ok=await provider.SetMfaRequiredAsync(target.Subject,request.Required,ct)&&await provider.SignOutEverywhereAsync(target.Subject,ct);
        if(ok&&!request.Required){target.MfaRequired=false;target.MfaRequirementRole=null;}
        MfaElevationService.Audit(db,clock.GetUtcNow(),actor.Account.Id,id,request.Required?"requirement-imposed":"requirement-removed",ok?"confirmed":"unconfirmed",request.Reason);
        await db.SaveChangesAsync(ct);
        if(ok)await NotifyAsync(accounts,target,"The multi-factor authentication requirement on your Swoovr account was changed.",ct);
        return ok?await ViewAsync(target,provider,null,ct):Unavailable();
    }

    public static async Task<IResult> ResetAsync(Guid id,MfaReset request,ClaimsPrincipal user,AccountResolver accounts,IIdentityDirectory members,IStaffIdentityAdmin staff,TimeProvider clock,
        Ticketing.TicketingStaffSignal helpdesk,CancellationToken ct)
    {
        var(actor,denied)=await StaffAccountEndpoints.AuthorizeAsync(user,accounts,true,ct);if(denied is not null)return denied;
        if(!request.Confirm||!request.IdentityVerified||!System.Text.RegularExpressions.Regex.IsMatch(request.CaseReference??"",@"^case:[0-9]{1,18}$"))return Invalid();
        var db=accounts.Db!;
        await using var lifecycle=await AccountLifecycleLease.AcquireManyAsync(db,[actor!.Account!.Id,id],ct);
        (actor,denied)=await StaffAccountEndpoints.AuthorizeAsync(user,accounts,true,ct);if(denied is not null)return denied;
        if(!await MfaElevationService.HasAsync(actor!,user,db,accounts.Services.GetRequiredService<IConfiguration>(),clock.GetUtcNow(),ct))return MfaElevationService.Required();
        var target=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==id,ct);if(target is null)return Results.NotFound();await db.Entry(target).ReloadAsync(ct);
        if(target.Id==actor!.Account!.Id||target.IsRootAdmin||target.Realm is not(AccountRealm.Members or AccountRealm.Staff)||target.Status is AccountStatus.Deleted or AccountStatus.PendingDeletion)return Results.Forbid();
        if(target.Realm==AccountRealm.Staff)
        {
            var roles=await db.RoleGrants.Where(g=>g.AccountId==id).Select(g=>g.Role).ToListAsync(ct);
            if(!StaffManagementPolicy.CanManage(actor.Account.Id,actor.IsRootAdmin,target,roles,roles))return Results.Forbid();
        }
        if(await StaffAccessEndpoints.ExcludedAsync(db,actor.Account.Id,[id],ct))return StaffAccessEndpoints.Conflict();
        IIdentityDirectory provider=target.Realm==AccountRealm.Staff?staff:members;
        if(!provider.Configured)return Unavailable();
        var now=clock.GetUtcNow();target.MfaResetPending=true;target.SessionsRevokedAt=Cutoff(target.SessionsRevokedAt,now);target.StaffRevision++;
        // Required is independent of credentials: clearing a staff factor can never make staff MFA optional.
        var required=target.Realm==AccountRealm.Staff||target.MfaRequired;
        MfaElevationService.Audit(db,now,actor.Account.Id,id,"reset-requested","pending",request.CaseReference);await db.SaveChangesAsync(ct);helpdesk.Nudge();
        accounts.Services.GetService<Swoovr.Api.Connections.MessagesConnections>()?.Retire(id,target.SessionsRevokedAt,clock.GetUtcNow());
        var ok=await provider.ClearMfaAsync(target.Subject,required,ct);
        if(ok){target.MfaResetPending=false;target.MfaLastResetAt=clock.GetUtcNow();}
        MfaElevationService.Audit(db,clock.GetUtcNow(),actor.Account.Id,id,"credentials-cleared",ok?"confirmed":"unconfirmed",request.CaseReference);await db.SaveChangesAsync(ct);helpdesk.Nudge();
        if(ok)await NotifyAsync(accounts,target,"An administrator reset your Swoovr authenticator after an account recovery review. Existing sessions have ended. If MFA is required, you must enrol again before continuing.",ct);
        return ok?await ViewAsync(target,provider,null,ct):Unavailable();
    }

    public static async Task<IResult> DisableAsync(ClaimsPrincipal user,AccountResolver accounts,IIdentityDirectory provider,TimeProvider clock,CancellationToken ct)
    {
        var(current,denied)=await MemberAsync(user,accounts,ct,ageGateExempt:true,barredExempt:true,mfaExempt:true);if(denied is not null)return denied;
        var db=accounts.Db!;var id=current!.Account!.Id;
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,id,ct);
        (current,denied)=await MemberAsync(user,accounts,ct,ageGateExempt:true,barredExempt:true,mfaExempt:true);if(denied is not null)return denied;
        var target=current!.Account!;
        if(target.MfaRequired||target.MfaResetPending)return Results.Forbid();
        if(!current.Evidence.MultiFactor||!current.RecentlyAuthenticated(clock.GetUtcNow()))return Problem(403,ProblemCodes.StaleSession,"Sign in again with your current MFA before disabling it.");
        if(!provider.Configured)return Unavailable();
        var now=clock.GetUtcNow();target.MfaResetPending=true;target.SessionsRevokedAt=Cutoff(target.SessionsRevokedAt,now);
        MfaElevationService.Audit(db,now,id,id,"disable-requested","pending");await db.SaveChangesAsync(ct);
        accounts.Services.GetService<Swoovr.Api.Connections.MessagesConnections>()?.Retire(id,target.SessionsRevokedAt,clock.GetUtcNow());
        var ok=await provider.ClearMfaAsync(target.Subject,false,ct);
        if(ok){target.MfaResetPending=false;target.MfaLastResetAt=clock.GetUtcNow();}
        MfaElevationService.Audit(db,clock.GetUtcNow(),id,id,"disabled",ok?"confirmed":"unconfirmed");await db.SaveChangesAsync(ct);
        if(ok)await NotifyAsync(accounts,target,"Multi-factor authentication was disabled on your Swoovr account. If this wasn't you, contact Swoovr support straight away.",ct);
        return ok?await ViewAsync(target,provider,null,ct):Unavailable();
    }
    public static async Task<(CurrentAccount? Actor,IResult? Denied)> ActorAsync(ClaimsPrincipal user,AccountResolver accounts,CancellationToken ct)
    {
        var current=await accounts.ResolveAsync(user,ct);if(current is null)return(null,Results.Unauthorized());
        if(current.Account is null||accounts.Db is null)return(null,Unavailable());
        await accounts.Db.Entry(current.Account).ReloadAsync(ct);
        if(SessionRevocationPolicy.Refuses(current.Account.SessionsRevokedAt,current.Facts.AuthenticatedAt,user.FindFirstValue("iat"),accounts.Services.GetRequiredService<TimeProvider>().GetUtcNow()))return(null,Results.Unauthorized());
        return new[]{AccountRole.Admin,AccountRole.Support,AccountRole.TrustSafety,AccountRole.Legal}.Any(current.Allows)?(current,null):(null,Results.Forbid());
    }
    private static async Task<IResult> ViewAsync(Account target,IIdentityDirectory provider,DateTimeOffset? lastMfa,CancellationToken ct,bool recoveryAvailable=false)
    {
        var info=await provider.AccountAsync(target.Subject,ct);if(info is null)return Unavailable();
        var methods=info.CredentialTypes.Where(c=>c is "otp" or "recovery-authn-codes").ToArray();
        var required=target.Realm==AccountRealm.Staff||target.MfaRequired;
        var state=target.MfaResetPending||info.RequiredActions.Contains("CONFIGURE_TOTP")?"setup-pending":methods.Contains("otp")?"enabled":required?"required":"disabled";
        return Results.Ok(new MfaView(state,required,target.MfaResetPending,methods,lastMfa,target.MfaLastResetAt,recoveryAvailable));
    }
    private static DateTimeOffset Cutoff(DateTimeOffset? old,DateTimeOffset now)=>old>SessionRevocationPolicy.Cutoff(now)?old.Value:SessionRevocationPolicy.Cutoff(now);
    private static async Task NotifyAsync(AccountResolver accounts,Account target,string text,CancellationToken ct)
    {
        if(target.Email is null)return;
        var outcome=await accounts.Services.GetRequiredService<IEmailSender>().SendOutcomeAsync(new EmailMessage(target.Email,"Your Swoovr account security changed",text+"\n\n— Swoovr (RePass Cloud Pty Ltd)"),ct);
        var status=outcome.Status is "accepted" or "rejected"?outcome.Status:"unknown";
        Audit.Record(accounts.Db!,accounts.Services.GetRequiredService<TimeProvider>().GetUtcNow(),null,"system","mfa.notification-"+status,"account",target.Id.ToString());
        await accounts.Db!.SaveChangesAsync(ct);
    }
    private static IResult Invalid()=>Problem(400,ProblemCodes.InvalidRequest,"Confirm the security change and record its reason or verified recovery case.");
    private static IResult Unavailable()=>Problem(503,ProblemCodes.IdentityUnavailable,"The MFA change could not be confirmed. Contact support or review the current account before retrying.");
}
