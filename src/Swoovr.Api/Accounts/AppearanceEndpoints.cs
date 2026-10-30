using System.Security.Claims;
using Swoovr.Contracts;
using static Swoovr.Api.Accounts.AccountEndpoints;
namespace Swoovr.Api.Accounts;
public static class AppearanceEndpoints
{
    private static IResult? Owner(HttpContext context,string issuer,string subject,bool required)
    {
        var current=AppearanceOwner.ForIdentity(issuer,subject);
        var supplied=context.Request.Headers.TryGetValue(AppearanceOwner.Header,out var values);
        if((required&&!supplied)||supplied&&(values.Count!=1||!AppearanceOwner.Valid(values[0])))return Problem(400,"appearance-owner-required","Reload appearance before saving.");
        if(supplied&&!string.Equals(values[0],current,StringComparison.Ordinal))return Results.Json(new AppearanceConflict("appearance-account-changed",null),statusCode:409);
        context.Response.Headers[AppearanceOwner.Header]=current;return null;
    }
    public static void Map(WebApplication app)
    {
        var group=app.MapGroup("/api/me/appearance").RequireAuthorization();
        group.MapGet("",async(ClaimsPrincipal user,AccountResolver accounts,AppearanceStore store,HttpContext context,CancellationToken token)=>
        {
            context.Response.Headers.CacheControl="no-store";
            var(current,denied)=await MemberAsync(user,accounts,token,ageGateExempt:true,phoneExempt:true,policyExempt:true);if(denied is not null)return denied;
            var ownerProblem=Owner(context,current!.Account!.Issuer,current.Account.Subject,false);if(ownerProblem is not null)return ownerProblem;
            var snapshot=await store.LoadAsync(current!.Account!.Id,token);return snapshot is null?Results.NoContent():Results.Ok(snapshot);
        });
        group.MapPut("",async(AppearanceUpdate update,ClaimsPrincipal user,AccountResolver accounts,AppearanceStore store,HttpContext context,CancellationToken token)=>
        {
            context.Response.Headers.CacheControl="no-store";
            var(current,denied)=await MemberAsync(user,accounts,token,ageGateExempt:true,phoneExempt:true,policyExempt:true);if(denied is not null)return denied;
            var ownerProblem=Owner(context,current!.Account!.Issuer,current.Account.Subject,true);if(ownerProblem is not null)return ownerProblem;
            var result=await store.SaveAsync(current!.Account!.Id,update,token);
            return result.Outcome switch
            {
                "saved"=>Results.Ok(result.Snapshot),
                "invalid"=>Problem(400,"invalid-appearance","Choose a supported appearance and valid revision/change identity."),
                "unavailable"=>Problem(403,ProblemCodes.AccountLocked,"This account cannot change appearance."),
                "exhausted"=>Results.Json(new AppearanceConflict("appearance-revision-exhausted",result.Snapshot),statusCode:409),
                _=>Results.Json(new AppearanceConflict("appearance-conflict",result.Snapshot),statusCode:409),
            };
        });
    }
}
