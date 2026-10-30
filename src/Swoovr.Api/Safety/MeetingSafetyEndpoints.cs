using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Safety;
public static class MeetingSafetyEndpoints
{
    public static void Map(WebApplication app)
    {
        var member=app.MapGroup("/api/me/meeting-safety").RequireAuthorization();
        member.AddEndpointFilter(async(context,next)=>app.Configuration.GetValue("MeetingSafety:Enabled",false)?await next(context):Results.Problem(statusCode:503,title:"Meeting safety is unavailable.",extensions:new Dictionary<string,object?>{{"code","meeting-safety-unavailable"}}));
        member.MapGet("",(ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,IHostEnvironment environment,CancellationToken token)=>Member(user,accounts,token,async id=>
        {
            var db=accounts.Db!;var contacts=await db.TrustedContacts.Where(c=>c.MemberId==id&&c.Consent!=TrustedContactConsent.Removed).OrderBy(c=>c.CreatedAt).ToListAsync(token);
            var sessions=await db.MeetingSafetySessions.Where(s=>s.MemberId==id).OrderByDescending(s=>s.CreatedAt).Take(50).ToListAsync(token);
            var views=new List<MeetingSafetySessionView>();foreach(var session in sessions)views.Add(await store.SessionViewAsync(session,token));
            return Results.Ok(new MeetingSafetyDashboard(contacts.Select(store.ContactView).ToList(),views,environment.IsDevelopment()?"local-simulation-only":"provider-acceptance-only"));
        }));
        member.MapPost("/contacts",(TrustedContactAdd request,ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,CancellationToken token)=>Member(user,accounts,token,async id=>Results.Ok(await store.AddContactAsync(id,request,token))));
        member.MapDelete("/contacts/{contact:guid}",(Guid contact,ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,CancellationToken token)=>Member(user,accounts,token,async id=>{await store.RemoveContactAsync(id,contact,token);return Results.Ok(new{removed=true});}));
        member.MapPost("/contacts/{contact:guid}/resend",(Guid contact,ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,CancellationToken token)=>Member(user,accounts,token,async id=>{await store.ResendAsync(id,contact,token);return Results.Ok(new{queued=true});}));
        member.MapGet("/contacts/{contact:guid}/compose",(Guid contact,ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,CancellationToken token)=>Member(user,accounts,token,async id=>
        {
            var row=await accounts.Db!.TrustedContacts.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==contact&&c.MemberId==id&&c.Consent==TrustedContactConsent.Confirmed,token);
            return row is null?Results.NotFound():Results.Ok(new TrustedContactCompose(store.Destination(row),row.Channel));
        }));
        member.MapPost("/sessions",(MeetingSafetyCreate request,ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,CancellationToken token)=>Member(user,accounts,token,async id=>Results.Ok(await store.CreateAsync(id,request,token))));
        member.MapPost("/sessions/{session:guid}/actions",(Guid session,MeetingSafetyAction request,ClaimsPrincipal user,AccountResolver accounts,MeetingSafetyStore store,CancellationToken token)=>Member(user,accounts,token,async id=>Results.Ok(await store.ActAsync(id,session,request,token))));
        var external=app.MapGroup("/api/safety/meeting-contact").RequireRateLimiting("meeting-safety-public");
        external.AddEndpointFilter(async(context,next)=>
        {
            var response=context.HttpContext.Response;response.Headers.CacheControl="no-store";response.Headers["Referrer-Policy"]="no-referrer";response.Headers["X-Robots-Tag"]="noindex, nofollow, noarchive";
            response.Headers["Content-Security-Policy"]="default-src 'none'; frame-ancestors 'none'; base-uri 'none'";
            return app.Configuration.GetValue("MeetingSafety:Enabled",false)?await next(context):Results.NotFound();
        });
        external.MapPost("/status",(MeetingSafetyTokenRequest request,MeetingSafetyStore store,CancellationToken token)=>store.ConsentStatusAsync(request.Token,token));
        external.MapPost("/consent",(MeetingSafetyConsentRequest request,MeetingSafetyStore store,CancellationToken token)=>store.ConsentAsync(request.Token,request.Action,token));
        external.MapPost("/scope",(MeetingSafetyContactCodeRequest request,MeetingSafetyContactVerification verification,CancellationToken token)=>verification.ScopeAsync(request.Link,token));
        external.MapPost("/code",(MeetingSafetyContactCodeRequest request,MeetingSafetyContactVerification verification,CancellationToken token)=>verification.RequestCodeAsync(request.Link,token));
        external.MapPost("/verify",(MeetingSafetyContactCodeCheck request,MeetingSafetyContactVerification verification,CancellationToken token)=>verification.VerifyAsync(request.Link,request.Code,token));
        external.MapPost("/summary",async(MeetingSafetyTokenRequest request,MeetingSafetyContactVerification verification,CancellationToken token)=>
        {
            var result=await verification.ReadAsync(request.Token,request.Grant,token);
            return !result.Available?Results.NotFound():result.View is{}view?Results.Ok(view):Results.Problem(statusCode:428,title:"Contact verification is required.");
        });
    }
    private static async Task<IResult> Member(ClaimsPrincipal user,AccountResolver accounts,CancellationToken token,Func<Guid,Task<IResult>> action)
    {
        var(current,denied)=await AccountEndpoints.MemberAsync(user,accounts,token,ageGateExempt:true,phoneExempt:true);if(denied is not null)return denied;
        try{return await action(current!.Account!.Id);}catch(MeetingSafetyRefusal refused){return Results.Problem(statusCode:refused.Status,title:"Meeting safety could not complete this action.",extensions:new Dictionary<string,object?>{{"code",refused.Code}});}
    }
}
