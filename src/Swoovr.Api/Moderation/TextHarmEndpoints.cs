using System.Security.Claims;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
namespace Swoovr.Api.Moderation;
public static class TextHarmEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/cases/{id:guid}/text-candidates",(Guid id,ClaimsPrincipal user,AccountResolver accounts,TextHarmCandidateReviewService reviews,HttpContext context,CancellationToken token)=>
            Staff(user,accounts,context,token,async actor=>Results.Ok(await reviews.ListAsync(actor,id,token)))).RequireAuthorization();
        app.MapGet("/api/admin/cases/{id:guid}/text-candidates/{review:guid}",(Guid id,Guid review,ClaimsPrincipal user,AccountResolver accounts,TextHarmCandidateReviewService reviews,HttpContext context,CancellationToken token)=>
            Staff(user,accounts,context,token,async actor=>Results.Ok(await reviews.ReadAsync(actor,id,review,token)))).RequireAuthorization();
        app.MapPost("/api/admin/cases/{id:guid}/text-candidates/{review:guid}",(Guid id,Guid review,TextHarmCandidateReviewRequest request,ClaimsPrincipal user,AccountResolver accounts,TextHarmCandidateReviewService reviews,HttpContext context,CancellationToken token)=>
            Staff(user,accounts,context,token,async actor=>Results.Ok(await reviews.MarkAsync(actor,id,review,request.Outcome,request.SanctionId,request.RemovalId,token)))).RequireAuthorization();
    }
    private static async Task<IResult> Staff(ClaimsPrincipal user,AccountResolver accounts,HttpContext context,CancellationToken token,Func<Guid,Task<IResult>> action)
    {
        context.Response.Headers.CacheControl="no-store";
        var(current,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Support,token);
        if(denied is not null)return denied;
        try{return await action(current!.Account!.Id);}
        catch(TextHarmReviewRefusal error){return AccountEndpoints.Problem(error.Status,error.Code,"That candidate is not available for this review.");}
    }
}
