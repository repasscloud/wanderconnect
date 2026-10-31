using System.Security.Claims;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
namespace Swoovr.Api.Analytics;
public static class BetaScorecardEndpoints
{
    public static void Map(WebApplication app)=>app.MapGet("/api/admin/beta-scorecard",async(int? weeksAgo,ClaimsPrincipal user,AccountResolver accounts,BetaScorecardQuery query,HttpContext context,TimeProvider clock,CancellationToken token)=>
    {
        context.Response.Headers.CacheControl="no-store";
        var(current,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Admin,token);if(denied is not null)return denied;
        if(context.Request.Query.Keys.Any(k=>k!="weeksAgo")||context.Request.Query["weeksAgo"].Count>1)return AccountEndpoints.Problem(400,Swoovr.Contracts.ProblemCodes.InvalidRequest,"Only one completed-week selector is supported; individual or trait filters are unavailable.");
        try{var report=await query.ReadAsync(weeksAgo??1,token);Audit.Record(accounts.Db!,clock.GetUtcNow(),current!.Account!.Id,"account","beta-scorecard.read","beta-scorecard",report.FromInclusive.ToString("yyyy-MM-dd"));await accounts.Db!.SaveChangesAsync(token);return Results.Ok(report);}
        catch(ArgumentException){return AccountEndpoints.Problem(400,Swoovr.Contracts.ProblemCodes.InvalidRequest,"Choose one of the12 fixed completed UTC weeks.");}
    }).RequireAuthorization();
}
