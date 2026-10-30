using System.Security.Claims;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
namespace Swoovr.Api.Moderation;
public static class ModerationTransparencyEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/moderation-transparency/{year:int}",(int year,ClaimsPrincipal user,AccountResolver accounts,ModerationTransparencyReports reports,HttpContext context,TimeProvider clock,CancellationToken token)=>
            Staff(user,accounts,context,clock,token,async()=>Results.Ok(await reports.ReadAsync(year,token)))).RequireAuthorization();
        app.MapGet("/api/admin/moderation-transparency/{year:int}/export",(int year,ClaimsPrincipal user,AccountResolver accounts,ModerationTransparencyReports reports,HttpContext context,TimeProvider clock,CancellationToken token)=>
            Staff(user,accounts,context,clock,token,async()=>{var result=await reports.ReadAsync(year,token);context.Response.Headers.ContentDisposition=$"attachment; filename=swoovr-code-report-{year}.json";return Results.Json(result);})).RequireAuthorization();
        app.MapPost("/api/admin/moderation-transparency/{year:int}/revisions",(int year,ClaimsPrincipal user,AccountResolver accounts,ModerationTransparencyReports reports,HttpContext context,TimeProvider clock,CancellationToken token)=>
            Staff(user,accounts,context,clock,token,async()=>Results.Ok(await reports.SaveAsync(year,token)),write:true)).RequireAuthorization();
    }
    private static async Task<IResult> Staff(ClaimsPrincipal user,AccountResolver accounts,HttpContext context,TimeProvider clock,CancellationToken token,Func<Task<IResult>> action,bool write=false)
    {
        context.Response.Headers.CacheControl="no-store";
        var(current,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Admin,token);if(denied is not null)return denied;
        if(write&&!current!.RecentlyAuthenticated(clock.GetUtcNow()))return AccountEndpoints.Problem(403,Swoovr.Contracts.ProblemCodes.StaleSession,"Sign in again before saving an internal report revision.");
        try{var result=await action();Audit.Record(accounts.Db!,clock.GetUtcNow(),current!.Account!.Id,"account",write?"code-report.revision":"code-report.read","annual-code-report",context.Request.RouteValues["year"]?.ToString());await accounts.Db!.SaveChangesAsync(token);return result;}
        catch(ArgumentException){return AccountEndpoints.Problem(400,Swoovr.Contracts.ProblemCodes.InvalidRequest,"Choose a completed UTC reporting year with retained sources for a new revision.");}
    }
}
