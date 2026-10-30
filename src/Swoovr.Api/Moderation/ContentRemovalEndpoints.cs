using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Connections;
using Swoovr.Contracts;
using Swoovr.Domain;

namespace Swoovr.Api.Moderation;
public static class ContentRemovalEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/admin/cases/{id:guid}/content-sources",(Guid id,ClaimsPrincipal user,AccountResolver accounts,ContentRemovalService removals,HttpContext context,CancellationToken token)=>
            Staff(user,accounts,context,token,async actor=>Results.Ok(await removals.SourcesAsync(actor,id,token)))).RequireAuthorization();
        app.MapGet("/api/admin/cases/{id:guid}/content-removals/{removal:guid}",(Guid id,Guid removal,ClaimsPrincipal user,AccountResolver accounts,ContentRemovalService removals,HttpContext context,CancellationToken token)=>
            Staff(user,accounts,context,token,async actor=>Results.Ok(await removals.EvidenceAsync(actor,id,removal,token)))).RequireAuthorization();
        app.MapPost("/api/admin/cases/{id:guid}/content-removals",(Guid id,ContentRemovalRequest request,ClaimsPrincipal user,AccountResolver accounts,ContentRemovalService removals,TimeProvider clock,HttpContext context,IHubContext<MessagesHub> hub,CancellationToken token)=>
            Staff(user,accounts,context,token,async actor=>
            {
                var result=await removals.RemoveAsync(actor,id,request,clock.GetUtcNow(),token);
                if(!result.AlreadyApplied&&request.Field=="message"&&request.MessageId is{}messageId)
                {
                    var message=await accounts.Db!.Messages.AsNoTracking().SingleAsync(m=>m.Id==messageId,token);
                    var conversation=await accounts.Db.Conversations.AsNoTracking().SingleAsync(c=>c.Id==message.ConversationId,token);
                    // Reuse the existing typed upsert event; never describe staff removal as a member unsend.
                    foreach(var member in new[]{conversation.PairLow,conversation.PairHigh})
                        await BetaMessageAudience.SendAsync(accounts.Services,member,MessagingEndpoints.View(message,member,clock.GetUtcNow()),token);
                }
                return Results.Ok(result);
            })).RequireAuthorization();
    }
    private static async Task<IResult> Staff(ClaimsPrincipal user,AccountResolver accounts,HttpContext context,CancellationToken token,Func<Guid,Task<IResult>> action)
    {
        context.Response.Headers.CacheControl="no-store";
        var(current,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Support,token);
        if(denied is not null)return denied;
        try{return await action(current!.Account!.Id);}
        catch(ContentRemovalRefusal refusal){return AccountEndpoints.Problem(refusal.Status,refusal.Code,"That content cannot be accessed through this review.");}
    }
}
