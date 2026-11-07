using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;
namespace Swoovr.Api.Media;

/// <summary>Signed staff links are not independent privilege: recheck the recipient's live authority and MFA at delivery.</summary>
public static class MfaEvidenceAccess
{
    public static bool StaffVariant(string variant)=>variant is "review" or "case" or "inspection" or "appeal";
    public static async Task<IResult?> CheckAsync(Guid media,Guid owner,string variant,ClaimsPrincipal user,AccountResolver accounts,DateTimeOffset now,CancellationToken ct)
    {
        if(!StaffVariant(variant))return null;
        var current=await accounts.ResolveAsync(user,ct);if(current is null)return Results.Unauthorized();
        if(current.Account is null||accounts.Db is null)return Results.Forbid();
        var allowed=variant switch
        {
            "case"=>current.Allows(AccountRole.Admin)||current.Allows(AccountRole.Legal),
            "appeal"=>current.Allows(AccountRole.Admin),
            _=>current.Allows(AccountRole.Admin)||current.Allows(AccountRole.Support),
        };
        if(!allowed)return Results.Forbid();
        var db=accounts.Db;var actor=current.Account.Id;var since=now-MediaPolicy.LinkLifetime;
        // A link obtained by somebody else is never enough, even for another staff member.
        if(!await db.MediaReviewViews.AnyAsync(v=>v.MediaId==media&&v.ActorAccountId==actor&&v.At>=since&&v.At<=now,ct))return Results.Forbid();
        if(await StaffAccessEndpoints.ExcludedAsync(db,actor,[owner],ct))return StaffAccessEndpoints.Conflict();
        if(current.Allows(AccountRole.Admin)||current.Allows(AccountRole.TrustSafety))
            if(!await MfaElevationService.HasAsync(current,user,db,accounts.Services.GetRequiredService<IConfiguration>(),now,ct))return MfaElevationService.Required();
        if(variant=="case")
        {
            var key=media.ToString();
            var cases=await db.CaseEvidence.Where(e=>e.Snapshot.Contains(key)).Select(e=>e.CaseId).Distinct().ToListAsync(ct);
            foreach(var caseId in cases)
                if(await StaffAccessEndpoints.ExcludedAsync(db,actor,await Swoovr.Api.Retention.SafetyRetention.CaseAccountsAsync(db,caseId,ct),ct))return StaffAccessEndpoints.Conflict();
        }
        Audit.Record(db,now,actor,"account","media.staff-link-read","media",media.ToString(),variant);await db.SaveChangesAsync(ct);
        return null;
    }
}
