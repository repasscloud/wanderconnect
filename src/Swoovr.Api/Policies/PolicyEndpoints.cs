using System.Security.Claims;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
namespace Swoovr.Api.Policies;
public static class PolicyEndpoints
{
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/policies/{version}",(string version,ApprovedPolicyCatalogue catalogue,TimeProvider clock,HttpContext context)=>
        {
            context.Response.Headers.CacheControl="no-store";
            var document=catalogue.Find(version,clock.GetUtcNow());return document is null?Results.NotFound():Results.Ok(new ApprovedPolicyDocumentView(document.Metadata.Id,document.Metadata.ContentSha256,document.Metadata.EffectiveAt!.Value,document.Terms,document.CommunityGuidelines));
        });
        app.MapGet("/api/me/policies",async(ClaimsPrincipal user,AccountResolver accounts,PolicyAcceptanceService policies,HttpContext context,CancellationToken token)=>
        {
            context.Response.Headers.CacheControl="no-store";
            var(current,denied)=await AccountEndpoints.MemberAsync(user,accounts,token,ageGateExempt:true,barredExempt:true,pendingDeletionExempt:true,phoneExempt:true,policyExempt:true);
            return denied??Results.Ok(await policies.StateAsync(current!.Account!.Id,token));
        }).RequireAuthorization();
        app.MapPost("/api/me/policies/accept",async(PolicyAcceptanceRequest request,ClaimsPrincipal user,AccountResolver accounts,PolicyAcceptanceService policies,HttpContext context,CancellationToken token)=>
        {
            context.Response.Headers.CacheControl="no-store";
            var(current,denied)=await AccountEndpoints.MemberAsync(user,accounts,token,ageGateExempt:true,barredExempt:true,phoneExempt:true,policyExempt:true);if(denied is not null)return denied;
            var accepted=await policies.AcceptAsync(current!.Account!.Id,request,token);
            return accepted is null?AccountEndpoints.Problem(400,ProblemCodes.InvalidRequest,"Explicitly accept the exact currently required approved version."):Results.Ok(accepted);
        }).RequireAuthorization();
    }
}
