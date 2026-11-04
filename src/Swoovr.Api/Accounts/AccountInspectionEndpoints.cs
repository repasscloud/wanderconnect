using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Media;
using Swoovr.Api.Profiles;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>D294: scoped staff inspection, never member impersonation, visits, chat browsing or Legal identity decryption.</summary>
public static class AccountInspectionEndpoints
{
    public static readonly string[] HistoryActions=["age.updated","profile.updated","home-market.override","staff.affiliated","staff.revoked","moderation.warn","moderation.restrict","moderation.suspend","moderation.terminate","entitlement.granted","dob.correction-granted","dob.correction-revoked","dob.correction-used",
        // #190 (D323): account detail actions.
        "entitlement.revoked","profile-dob.opened","account.locked","account.unlocked","sessions.revoked",
        "verification.photoVerified.applied","verification.photoVerified.revoked","verification.idVerified.applied","verification.idVerified.revoked"];
    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/admin/accounts/{id:guid}").RequireAuthorization();
        group.AddEndpointFilter(async(context,next)=>{context.HttpContext.Response.Headers.CacheControl="no-store";return await next(context);});
        group.MapGet("/assurance",async(Guid id,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var(current,denied)=await StaffAsync(user,accounts,id,false,token);if(denied is not null)return denied;
            var profile=await accounts.Db!.MemberProfiles.FindAsync([id],token);
            if(profile is null)return Results.NotFound();
            return Results.Ok(new StaffAgeAssurance(id,profile.DateOfBirth is{}date?AgePolicy.YearsOn(date,DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime)):null,
                profile.DateOfBirth is not null,profile.DateOfBirthVerified,Wire(profile.AgeStatus)));
        });
        group.MapPost("/date-of-birth",async(Guid id,IdentityAccessRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var(current,denied)=await StaffAsync(user,accounts,id,false,token);if(denied is not null)return denied;
            if(string.IsNullOrWhiteSpace(request.Purpose)||request.Purpose.Trim().Length>500)return Problem(400,ProblemCodes.PurposeRequired,"Record a purpose (up to 500 characters).");
            var db=accounts.Db!;var now=clock.GetUtcNow();
            if(await StaffAccessEndpoints.GrantAsync(db,current!.Account!.Id,StaffAccessPolicy.ProfileDobOpen,now,token)is null)return StaffAccessEndpoints.NeedsGrant(StaffAccessPolicy.ProfileDobOpen);
            var profile=await db.MemberProfiles.FindAsync([id],token);if(profile is null)return Results.NotFound();
            db.IdentityAccesses.Add(new IdentityAccess{AccountId=id,ActorAccountId=current.Account.Id,Purpose=request.Purpose.Trim(),At=now});
            Audit.Record(db,now,current.Account.Id,"account","profile-dob.opened","account",id.ToString());
            await db.SaveChangesAsync(token);return Results.Ok(new StaffDateOfBirth(profile.DateOfBirth,profile.DateOfBirthVerified));
        });
        group.MapGet("/profile-preview",async(Guid id,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var(current,denied)=await StaffAsync(user,accounts,id,true,token);if(denied is not null)return denied;
            var preview=await PreviewAsync(accounts.Db!,accounts.Services.GetRequiredService<MediaLinks>(),id,current!.Allows(AccountRole.Admin),clock.GetUtcNow(),token);
            if(preview is null)return Results.NotFound();
            Audit.Record(accounts.Db!,clock.GetUtcNow(),current.Account!.Id,"account","profile.staff-previewed","account",id.ToString());
            await accounts.Db!.SaveChangesAsync(token);return Results.Ok(preview);
        });
        group.MapGet("/profile-photos",async(Guid id,ClaimsPrincipal user,AccountResolver accounts,CancellationToken token)=>
        {
            var(current,denied)=await StaffAsync(user,accounts,id,true,token);if(denied is not null)return denied;
            if(!current!.Allows(AccountRole.Admin))return Results.Forbid();
            var db=accounts.Db!;
            var rows=await db.MediaItems.Where(i=>i.AccountId==id&&i.Purpose==null&&i.RemovedAt==null&&i.Status!=MediaStatus.Blocked&&i.Status!=MediaStatus.Removed).OrderByDescending(i=>i.CreatedAt).Take(100).ToListAsync(token);
            var ids=rows.Select(i=>i.Id).ToArray();var placements=await db.MediaPlacements.Where(p=>ids.Contains(p.MediaId)).ToListAsync(token);
            return Results.Ok(rows.Select(i=>new StaffProfilePhoto(i.Id,Wire(i.Status),i.CreatedAt,placements.Where(p=>p.MediaId==i.Id).Select(p=>p.Scope).Distinct().ToList(),placements.Where(p=>p.MediaId==i.Id&&p.IsMain).Select(p=>p.Scope).Distinct().ToList())).ToList());
        });
        group.MapPost("/profile-photos/{photoId:guid}/view",async(Guid id,Guid photoId,MediaViewRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            var(current,denied)=await StaffAsync(user,accounts,id,true,token);if(denied is not null)return denied;
            if(!current!.Allows(AccountRole.Admin))return Results.Forbid();
            if(request.Purpose?.Trim()is not{Length:>0 and <=500}purpose)return Problem(400,ProblemCodes.PurposeRequired,"Record why this profile photo must be opened.");
            var db=accounts.Db!;var now=clock.GetUtcNow();
            var photo=await db.MediaItems.SingleOrDefaultAsync(i=>i.Id==photoId&&i.AccountId==id,token);
            if(photo is null||!MediaPolicy.ProfileInspectable(photo))return Results.NotFound();
            if(await PhotoReviewWorkflows.AckUntilAsync(db,current.Account!.Id,now,token)is null)return PhotoReviewWorkflows.AckRequired();
            var link=await PhotoReviewWorkflows.ReviewLinkAsync(db,accounts.Services.GetRequiredService<MediaLinks>(),current.Account.Id,photo.Id,"inspection",now,token);
            if(link is null)return Problem(503,ProblemCodes.MediaUnavailable,"Photo links are unavailable.");
            db.MediaReviewViews.Add(new MediaReviewView{MediaId=photo.Id,ActorAccountId=current.Account!.Id,Purpose=purpose,At=now});
            Audit.Record(db,now,current.Account.Id,"account","media.profile-inspected","media",photo.Id.ToString());
            await db.SaveChangesAsync(token);return Results.Ok(link);
        });
        group.MapGet("/action-history",async(Guid id,long? before,ClaimsPrincipal user,AccountResolver accounts,CancellationToken token)=>
        {
            var(_,denied)=await StaffAsync(user,accounts,id,true,token);if(denied is not null)return denied;
            if(before<=0)return Problem(400,ProblemCodes.RecordChanged,"Choose a valid history cursor.");
            return Results.Ok(await HistoryAsync(accounts.Db!,id,before,token));
        });
    }

    private static async Task<(CurrentAccount? Current,IResult? Denied)> StaffAsync(ClaimsPrincipal user,AccountResolver accounts,Guid id,bool preview,CancellationToken token)
    {
        var current=await accounts.ResolveAsync(user,token);if(current is null)return(null,Results.Unauthorized());
        if(current.Account is null||accounts.Db is null)return(null,Problem(503,ProblemCodes.PersistenceUnavailable,"Accounts are unavailable."));
        var allowed=current.Facts.Realm==AccountRealm.Staff&&(current.Allows(AccountRole.Admin)||current.Allows(AccountRole.Support)||!preview&&current.Allows(AccountRole.Legal));
        if(!allowed)return(null,Results.Forbid());
        if(await StaffAccessEndpoints.ExcludedAsync(accounts.Db,current.Account.Id,[id],token))return(null,StaffAccessEndpoints.Conflict());
        return(current,null);
    }

    public static async Task<StaffActionPage> HistoryAsync(SwoovrDbContext db,Guid id,long? before,CancellationToken token)
    {
        var key=id.ToString();
        var rows=await db.AuditEntries.Where(a=>a.TargetType=="account"&&a.TargetId==key&&HistoryActions.Contains(a.Action)&&(before==null||a.Id<before))
            .OrderByDescending(a=>a.Id).Take(26).Select(a=>new StaffActionEntry(a.Id,a.At,a.Action)).ToListAsync(token);
        return new(rows.Take(25).ToList(),rows.Count>25?rows[24].Id:null);
    }

    public static async Task<StaffProfilePreview?> PreviewAsync(SwoovrDbContext db,MediaLinks links,Guid id,bool admin,DateTimeOffset now,CancellationToken token)
    {
        var profile=await db.MemberProfiles.FindAsync([id],token);if(profile is null)return null;
        var modes=await db.ConnectionModes.Where(m=>m.Available).OrderBy(m=>m.SortOrder).ToListAsync(token);
        var settings=await db.MemberModes.Where(m=>m.AccountId==id).ToListAsync(token);
        var visible=settings.Where(AccountInspectionPolicy.PublicMode).Select(m=>m.ModeId).ToArray();
        var photos=await MediaEndpoints.PhotosAsync(db,links,[id],visible,now,token);
        var identity=await db.MemberIdentities.FindAsync([id],token);
        var shown=ProfileEndpoints.Public(identity,profile.DateOfBirth is{}date?AgePolicy.YearsOn(date,DateOnly.FromDateTime(now.UtcDateTime)):null,await ProfileEndpoints.GenderLabelsAsync(db,token));
        var chips=await ProfileEndpoints.IntentionLabelsAsync(db,[id],null,token);
        var badges=ProfileEndpoints.Badges(profile,await ProfileEndpoints.BadgeOrderAsync(db,token));
        var views=modes.Select(m=>
        {
            var mode=settings.SingleOrDefault(s=>s.ModeId==m.Id);var publiclyShown=mode is not null&&AccountInspectionPolicy.PublicMode(mode);
            ProfileCard? card=publiclyShown?new ProfileCard(profile.PublicId,ModePrivacy.DisplayName(mode!.DisplayName,profile.DisplayName)??"Unnamed member",profile.Area,
                ModePrivacy.About(mode.About,profile.About,mode.ShareCoreAbout),chips.GetValueOrDefault((id,m.Id),[]),badges,shown.Age,shown.Gender,shown.Pronouns,null,photos.Main.GetValueOrDefault((id,m.Id))):null;
            return new StaffModePreview(m.Id,m.Label,mode?.Enabled??false,mode?.Visible??false,card,publiclyShown?photos.Modes.GetValueOrDefault((id,m.Id),[]):[]);
        }).ToList();
        var cases=await db.ModerationCases.Where(c=>c.SubjectAccountId==id&&(admin||c.Queue==CaseQueue.Support)).OrderByDescending(c=>c.CreatedAt).Take(25)
            .Select(c=>new StaffCaseLink(c.Id,c.Status.ToString())).ToListAsync(token);
        return new(id,views,cases,admin);
    }
}
