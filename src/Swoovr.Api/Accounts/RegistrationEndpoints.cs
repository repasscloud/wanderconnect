using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public static class RegistrationEndpoints
{
    private static RegistrationAvailabilityView View(RegistrationAuthority.Row row,bool change=false)=>new(row.BetaEnabled,row.PublicEnabled,row.Revision,row.Policy.State,change);
    private static IResult Unavailable()=>Results.Problem(statusCode:503,title:"Registration availability is unavailable.");
    public static void Map(WebApplication app)
    {
        app.MapGet("/api/registration",async(HttpContext http,IServiceProvider services,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            if(services.GetService<SwoovrDbContext>()is not{}db)return Unavailable();
            var row=await RegistrationAuthority.ReadAsync(db,token);
            return row is null?Unavailable():Results.Ok(View(row));
        }).AllowAnonymous();
        // Dedicated provider credential, compared in constant time; never issued to browsers or placed in realm JSON.
        app.MapPost("/api/registration/admission",async(RegistrationAdmissionRequest request,HttpContext http,IConfiguration configuration,IServiceProvider services,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            var expected=configuration["Registration:ProviderToken"];
            var supplied=http.Request.Headers["X-Swoovr-Registration"].ToString();
            if(string.IsNullOrWhiteSpace(expected)||expected.Length<32||supplied.Length>512||!CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(expected)),SHA256.HashData(Encoding.UTF8.GetBytes(supplied))))return Results.Unauthorized();
            if(request.Email is null||request.Email.Length>254||services.GetService<SwoovrDbContext>()is not{}db)return Unavailable();
            var row=await RegistrationAuthority.ReadAsync(db,token);
            if(row is null)return Unavailable();
            return Results.Ok(new{allowed=RegistrationAvailability.Allows(row.Policy,await RegistrationAuthority.InvitedAsync(db,request.Email,token)),revision=row.Revision});
        }).AllowAnonymous();
        app.MapGet("/api/admin/registration",async(ClaimsPrincipal user,AccountResolver accounts,HttpContext http,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            var current=await accounts.ResolveAsync(user,token);
            if(current is null)return Results.Unauthorized();
            if(current.Account is null)return Unavailable();
            if(!(current.Allows(AccountRole.Admin)||current.Allows(AccountRole.Support)||current.Allows(AccountRole.Legal)))return Results.Forbid();
            var row=await RegistrationAuthority.ReadAsync(accounts.Db!,token);
            return row is null?Unavailable():Results.Ok(View(row,current.IsRootAdmin));
        }).RequireAuthorization();
        app.MapPut("/api/admin/registration",async(RegistrationAvailabilityUpdate update,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,HttpContext http,CancellationToken token)=>
        {
            http.Response.Headers.CacheControl="no-store";
            var(current,denied)=await AdminEndpoints.StaffAsync(user,accounts,AccountRole.Admin,token);
            if(denied is not null)return denied;
            if(!current!.IsRootAdmin)return Results.Forbid();
            if(!current.RecentlyAuthenticated(clock.GetUtcNow()))return Results.Problem(statusCode:403,title:"Sign in again before changing registration.");
            if(!RegistrationAvailability.ValidChange(update.ExpectedRevision,update.Confirm,update.Reason))return Results.BadRequest(new{code="confirmation-required"});
            var db=accounts.Db!;await using var transaction=await db.Database.BeginTransactionAsync(token);
            var before=await RegistrationAuthority.ReadAsync(db,token);
            if(before is null)return Unavailable();
            if(before.Revision!=update.ExpectedRevision)return Results.Conflict(new{code="revision-conflict"});
            var now=clock.GetUtcNow();var reason=update.Reason.Trim();
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE registration_availability SET beta_enabled={update.BetaEnabled},public_enabled={update.PublicEnabled},revision=revision+1,changed_at={now},changed_by={current.Account!.Id},reason={reason} WHERE id=1 AND revision={update.ExpectedRevision}",token);
            if(changed!=1)return Results.Conflict(new{code="revision-conflict"});
            Audit.Record(db,now,current.Account.Id,"account","registration.changed","registration-policy","1",JsonSerializer.Serialize(new{previous=new{before.BetaEnabled,before.PublicEnabled,before.Revision},next=new{update.BetaEnabled,update.PublicEnabled,Revision=update.ExpectedRevision+1},reason}));
            await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
            return Results.Ok(new RegistrationAvailabilityView(update.BetaEnabled,update.PublicEnabled,update.ExpectedRevision+1,new RegistrationAvailability(update.BetaEnabled,update.PublicEnabled,update.ExpectedRevision+1).State,true));
        }).RequireAuthorization();
    }
}
