using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

public static class RegisteredNamesEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/me/registered-names",async(ClaimsPrincipal user,AccountResolver accounts,HttpContext http,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            var(current,denied)=await MemberAsync(user,accounts,token,ageGateExempt:true,phoneExempt:true,policyExempt:true,registeredNamesExempt:true);
            return denied??Results.Ok(RegisteredNamesService.View(current!.Account!));
        }).RequireAuthorization();
        app.MapPut("/api/me/registered-names",async(RegisteredNamesUpdate update,ClaimsPrincipal user,AccountResolver accounts,HttpContext http,TimeProvider clock,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            var(current,denied)=await MemberAsync(user,accounts,token,ageGateExempt:true,phoneExempt:true,policyExempt:true,registeredNamesExempt:true);
            if(denied is not null)return denied;
            var result=await RegisteredNamesService.SaveAsync(accounts.Db!,current!.Account!.Id,update,clock.GetUtcNow(),token);
            return result.Error is null?Results.Ok(result.Value):Problem(result.Error=="record-changed"?409:400,result.Error,"Check and explicitly confirm your registered names. Reload if another session changed them.");
        }).RequireAuthorization();
        app.MapGet("/api/admin/accounts/{id:guid}/registered-names",async(Guid id,ClaimsPrincipal user,AccountResolver accounts,HttpContext http,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            var(_,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Support,token);
            if(denied is not null)return denied;
            var account=await accounts.Db!.Accounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==id&&a.Realm==AccountRealm.Members,token);
            return account is null?Results.NotFound():Results.Ok(RegisteredNamesService.View(account));
        }).RequireAuthorization();
    }
}
