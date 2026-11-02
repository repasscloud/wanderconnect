using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Api.Verification;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>D299 correction authority; dates never enter ordinary grant metadata or audit entries.</summary>
public static class DobChangeEndpoints
{
    public static void Map(WebApplication app)
    {
        var admin=app.MapGroup("/api/admin/accounts/{id:guid}/dob-correction").RequireAuthorization();
        admin.MapGet("",async(Guid id,ClaimsPrincipal user,AccountResolver accounts,CancellationToken token)=>
        {
            var current=await accounts.ResolveAsync(user,token);
            if(current is null)return Results.Unauthorized();
            if(!current.Allows(AccountRole.Admin))return Results.Forbid();
            if(accounts.Db is not{}db)return Problem(503,ProblemCodes.PersistenceUnavailable,"Accounts are unavailable.");
            if(await StaffAccessEndpoints.ExcludedAsync(db,current.Account!.Id,[id],token))return StaffAccessEndpoints.Conflict();
            return Results.Ok(await db.DobChangeGrants.Where(g=>g.AccountId==id).OrderByDescending(g=>g.CreatedAt)
                .Select(g=>new DobGrantView(g.Id,g.AccountId,g.CreatedAt,g.ExpiresAt,g.ConsumedAt,g.RevokedAt)).ToListAsync(token));
        });
        admin.MapPost("",async(Guid id,DobGrantRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var current=await accounts.ResolveAsync(user,token);
            if(current is null)return Results.Unauthorized();
            if(!current.Allows(AccountRole.Admin))return Results.Forbid();
            if(accounts.Db is not{}db||current.Account is null)return Problem(503,ProblemCodes.PersistenceUnavailable,"Accounts are unavailable.");
            if(string.IsNullOrWhiteSpace(request.Reason)||request.Reason.Trim().Length>500)return Problem(400,ProblemCodes.PurposeRequired,"Record a correction reason (up to 500 characters; do not include a date of birth).");
            await using var lifecycle=await AccountLifecycleLease.AcquireManyAsync(db,[id,current.Account.Id],token);
            current=await accounts.ResolveAsync(user,token);
            if(current is null||!current.Allows(AccountRole.Admin))return Results.Forbid();
            if(await StaffAccessEndpoints.ExcludedAsync(db,current.Account!.Id,[id],token))return StaffAccessEndpoints.Conflict();
            if(!await db.Accounts.AnyAsync(a=>a.Id==id&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,token)
                ||!await db.MemberProfiles.AnyAsync(p=>p.AccountId==id&&p.DateOfBirth!=null&&p.DobCheck==DobCheck.None,token))return Problem(404,ProblemCodes.NotAvailable,"No eligible member date is on file; finish any existing ID correction or review first.");
            await using var preservation=await PreservationLease.SharedAsync(db,token);
            await using var tx=await db.Database.BeginTransactionAsync(token);
            var now=clock.GetUtcNow();
            await db.DobChangeGrants.Where(g=>g.AccountId==id&&g.ConsumedAt==null&&g.RevokedAt==null)
                .ExecuteUpdateAsync(u=>u.SetProperty(g=>g.RevokedAt,now).SetProperty(g=>g.RevokedBy,current.Account.Id),token);
            var grant=new DobChangeGrant{Id=Guid.NewGuid(),AccountId=id,GrantedBy=current.Account.Id,Reason=request.Reason.Trim(),CreatedAt=now,ExpiresAt=now+DobChangePolicy.Window};
            db.DobChangeGrants.Add(grant);
            Audit.Record(db,now,current.Account.Id,"account","dob.correction-granted","account",id.ToString(),grant.Id.ToString());
            await db.SaveChangesAsync(token);await tx.CommitAsync(token);
            return Results.Ok(View(grant));
        });
        admin.MapPost("/{grantId:guid}/revoke",async(Guid id,Guid grantId,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var current=await accounts.ResolveAsync(user,token);
            if(current is null)return Results.Unauthorized();
            if(!current.Allows(AccountRole.Admin))return Results.Forbid();
            if(accounts.Db is not{}db||current.Account is null)return Problem(503,ProblemCodes.PersistenceUnavailable,"Accounts are unavailable.");
            await using var lifecycle=await AccountLifecycleLease.AcquireManyAsync(db,[id,current.Account.Id],token);
            current=await accounts.ResolveAsync(user,token);
            if(current is null||!current.Allows(AccountRole.Admin))return Results.Forbid();
            if(await StaffAccessEndpoints.ExcludedAsync(db,current.Account!.Id,[id],token))return StaffAccessEndpoints.Conflict();
            await using var preservation=await PreservationLease.SharedAsync(db,token);
            var grant=await db.DobChangeGrants.SingleOrDefaultAsync(g=>g.Id==grantId&&g.AccountId==id,token);
            if(grant is null)return Results.NotFound();
            if(grant.ConsumedAt is not null)return Problem(409,ProblemCodes.RecordChanged,"This correction has already been used.");
            grant.RevokedAt??=clock.GetUtcNow();grant.RevokedBy=current.Account.Id;
            Audit.Record(db,clock.GetUtcNow(),current.Account.Id,"account","dob.correction-revoked","account",id.ToString(),grant.Id.ToString());
            await db.SaveChangesAsync(token);return Results.Ok(View(grant));
        });
        app.MapPost("/api/admin/accounts/{id:guid}/dob-history",async(Guid id,IdentityAccessRequest request,ClaimsPrincipal user,AccountResolver accounts,IdentityKeys keys,TimeProvider clock,CancellationToken token)=>
        {
            var current=await accounts.ResolveAsync(user,token);
            if(current is null)return Results.Unauthorized();
            if(!current.Allows(AccountRole.Legal))return Results.Forbid();
            if(accounts.Db is not{}db||current.Account is null)return Problem(503,ProblemCodes.PersistenceUnavailable,"Accounts are unavailable.");
            if(!VerifiedIdentityPolicy.MayDecrypt(AccountRole.Legal,request.Purpose))return Problem(400,ProblemCodes.PurposeRequired,"Record why you need this history (up to 500 characters).");
            var now=clock.GetUtcNow();
            if(await StaffAccessEndpoints.GrantAsync(db,current.Account.Id,StaffAccessPolicy.IdentityOpen,now,token)is null)return StaffAccessEndpoints.NeedsGrant(StaffAccessPolicy.IdentityOpen);
            if(await StaffAccessEndpoints.ExcludedAsync(db,current.Account.Id,[id],token))return StaffAccessEndpoints.Conflict();
            var rows=await HistoryAsync(db,keys,id,token);
            db.IdentityAccesses.Add(new IdentityAccess{AccountId=id,ActorAccountId=current.Account.Id,Purpose=request.Purpose!.Trim(),At=now});
            Audit.Record(db,now,current.Account.Id,"account","dob.history-opened","account",id.ToString());
            await db.SaveChangesAsync(token);return Results.Ok(rows);
        }).RequireAuthorization();
    }

    public static DobGrantView View(DobChangeGrant grant)=>new(grant.Id,grant.AccountId,grant.CreatedAt,grant.ExpiresAt,grant.ConsumedAt,grant.RevokedAt);
    public static async Task<bool> ConsumeAsync(SwoovrDbContext db,Guid grant,Guid member,DateTimeOffset now,CancellationToken token)=>
        await db.DobChangeGrants.Where(g=>g.Id==grant&&g.AccountId==member&&g.CreatedAt<=now&&g.ExpiresAt>now&&g.ConsumedAt==null&&g.RevokedAt==null)
            .ExecuteUpdateAsync(u=>u.SetProperty(g=>g.ConsumedAt,now),token)==1;
    public static async Task<DateTimeOffset?> UntilAsync(SwoovrDbContext db,Guid id,DateTimeOffset now,CancellationToken token)=>
        await db.DobChangeGrants.Where(g=>g.AccountId==id&&g.CreatedAt<=now&&g.ExpiresAt>now&&g.ConsumedAt==null&&g.RevokedAt==null).Select(g=>(DateTimeOffset?)g.ExpiresAt).MaxAsync(token);
    public static async Task<List<DobHistoryView>> HistoryAsync(SwoovrDbContext db,IdentityKeys keys,Guid id,CancellationToken token)=>
        (await db.DobChangeHistories.Where(h=>h.AccountId==id).OrderBy(h=>h.ChangedAt).ToListAsync(token))
            .Select(h=>new DobHistoryView(h.Id,DobChangePolicy.Open(id,keys.Encryption,h.Sealed),h.ChangedAt)).ToList();
    public static void PreservePrevious(SwoovrDbContext db,IdentityKeys keys,MemberProfile profile,DateOnly corrected,Guid? grant,DateTimeOffset now)
    {
        if(profile.DateOfBirth is not{}previous||previous==corrected)return;
        if(keys.CurrentVersion is not{}version)throw new InvalidOperationException("DOB history encryption is unavailable.");
        db.DobChangeHistories.Add(new DobChangeHistory{Id=Guid.NewGuid(),AccountId=profile.AccountId,GrantId=grant,ChangedAt=now,
            Sealed=DobChangePolicy.Seal(profile.AccountId,previous,version,keys.Encryption[version])});
    }
}
