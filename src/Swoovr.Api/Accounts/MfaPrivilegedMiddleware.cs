using Microsoft.AspNetCore.Routing;
using Swoovr.Domain;
namespace Swoovr.Api.Accounts;

/// <summary>Checks matched endpoint templates, never client URL spelling. Existing roles and purpose grants still apply.</summary>
public sealed class MfaPrivilegedMiddleware(RequestDelegate next)
{
    public static bool Requires(string method,string template)
    {
        if(!template.StartsWith("/api/admin/",StringComparison.Ordinal))return false;
        // Recording the safety notice grants no media access. Reveal and decision routes retain elevation.
        if(method=="POST"&&template=="/api/admin/media/safety-ack")return false;
        var write=method is not("GET" or "HEAD" or "OPTIONS");
        if(template.StartsWith("/api/admin/mfa/elevation",StringComparison.Ordinal))return false;
        if(template.StartsWith("/api/admin/staff",StringComparison.Ordinal)||template.StartsWith("/api/admin/invitations",StringComparison.Ordinal)
            ||template.StartsWith("/api/admin/registration",StringComparison.Ordinal)||template.StartsWith("/api/admin/security",StringComparison.Ordinal))return write;
        if(template.StartsWith("/api/admin/mfa/",StringComparison.Ordinal))return write;
        // Existing sensitive access resources (identity/evidence/legal cases/police holds) retain all their role gates.
        if(new[]{"/api/admin/identity/","/api/admin/holds/","/api/admin/law-enforcement/","/api/admin/disclosures/"}.Any(template.StartsWith))return true;
        if(template.EndsWith("/identity",StringComparison.Ordinal)||template.StartsWith("/api/admin/media/",StringComparison.Ordinal)&&(template.EndsWith("/view",StringComparison.Ordinal)||template.EndsWith("/case-view",StringComparison.Ordinal))
            ||template.Contains("/content",StringComparison.Ordinal)||template.Contains("/text-review",StringComparison.Ordinal)||template.Contains("/text-candidates",StringComparison.Ordinal)
            ||template.Contains("/date-of-birth",StringComparison.Ordinal)||template.Contains("/dob-history",StringComparison.Ordinal))return true;
        // Every other administrative mutation is privileged too (including appeals and future endpoints).
        return write;
    }
    public async Task InvokeAsync(HttpContext context,AccountResolver accounts,TimeProvider clock,IConfiguration config)
    {
        if(context.GetEndpoint() is RouteEndpoint endpoint&&Requires(context.Request.Method,endpoint.RoutePattern.RawText??"")&&context.User.Identity?.IsAuthenticated==true)
        {
            var current=await accounts.ResolveAsync(context.User,context.RequestAborted);
            if(current is not null&&(current.Allows(AccountRole.Admin)||current.Allows(AccountRole.TrustSafety)))
            {
                if(accounts.Db is null||!await MfaElevationService.HasAsync(current,context.User,accounts.Db,config,clock.GetUtcNow(),context.RequestAborted))
                {
                    if(accounts.Db is not null&&current.Account is{}actor){MfaElevationService.Audit(accounts.Db,clock.GetUtcNow(),actor.Id,null,"elevation-denied","privileged-route");await accounts.Db.SaveChangesAsync(context.RequestAborted);}
                    await MfaElevationService.Required().ExecuteAsync(context);return;
                }
            }
        }
        await next(context);
    }
}
