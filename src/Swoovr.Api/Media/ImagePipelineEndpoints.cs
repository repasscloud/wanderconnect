using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Media;

/// <summary>Opt-in staging endpoints for #294. Existing member image delivery, review and preservation remain authoritative.</summary>
public static class ImagePipelineEndpoints
{
    public static void Add(IServiceCollection services,IConfiguration configuration,IHostEnvironment environment,bool persistence)
    {
        services.AddSingleton(ImagePipelineOptions.Read(configuration,environment.IsDevelopment()));
        if(persistence)services.AddScoped<ImagePipelineStore>();
    }
    public static void Map(WebApplication app)
    {
        var options=app.Services.GetRequiredService<ImagePipelineOptions>();
        app.MapGet("/api/media/pipeline/readiness",(HttpRequest request,AccountResolver accounts)=>
        {
            if(!options.Enabled)return Results.Json(new{ready=false,mode="disabled",deliverable=false},statusCode:503);
            if(!options.Authenticates(request.Headers.Authorization))return Results.Unauthorized();
            if(accounts.Db is null)return Results.Json(new{ready=false,mode="staging-only",deliverable=false,reason="persistence-unavailable"},statusCode:503);
            return Results.Json(new{ready=true,mode="staging-only",deliverable=false,protocolVersion=1,policyVersion=options.PolicyVersion,blockers=new[]{"review-and-appeal-adapters","specialist-evidence-custody","hold-aware-raw-retention","current-viewer-delivery","client-cutover"}});
        });
        app.MapPost("/api/media/pipeline/claim",async(HttpRequest request,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            if(ServiceGate(request,accounts,options) is{} denied)return denied;
            var body=await ReadAsync(request,token);if(body is null)return Invalid();
            try
            {
                var claim=body.Value.Deserialize<ImagePipelineClaim>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if(claim is null)return Invalid();
                return Result(await accounts.Services.GetRequiredService<ImagePipelineStore>().ClaimAsync(claim,clock.GetUtcNow(),token));
            }
            catch(JsonException){return Invalid();}
        });
        app.MapPost("/api/media/pipeline/results",async(HttpRequest request,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            if(ServiceGate(request,accounts,options) is{} denied)return denied;
            var body=await ReadAsync(request,token);if(body is null)return Invalid();
            return Result(await accounts.Services.GetRequiredService<ImagePipelineStore>().CompleteAsync(body.Value,clock.GetUtcNow(),token));
        });
        app.MapPost("/api/media/pipeline/access",(HttpRequest request,AccountResolver accounts)=>
        {
            if(ServiceGate(request,accounts,options) is{} denied)return denied;
            // An accepted scan report is never a delivery grant. No configurable bypass of this cutover gate.
            return Results.Json(new{allowed=false,deliverable=false,reason="pipeline-cutover-incomplete"},statusCode:503);
        });
        app.MapPost("/api/me/photos/uploads",async(HttpRequest request,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            if(!options.Enabled)return Results.NotFound();
            var(current,denied)=await MemberAsync(user,accounts,token);if(denied is not null)return denied;
            if(current!.Enforcement.NoPhotoUploads)return Problem(403,"restricted","You can't add photos right now.");
            var body=await ReadAsync(request,token);
            if(body is null||body.Value.ValueKind!=JsonValueKind.Object||!body.Value.TryGetProperty("purpose",out var purpose)||purpose.ValueKind!=JsonValueKind.String||purpose.GetString()!="profile")return Problem(400,"unsupported-image-purpose","Choose a supported photo upload.");
            var db=accounts.Db!;var me=current.Profile!.AccountId;var now=clock.GetUtcNow();
            await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,me,token);
            if(!await db.Accounts.AnyAsync(a=>a.Id==me&&a.Status==AccountStatus.Active,token))return Results.NotFound();
            var jobs=db.ImageProcessingJobs.Where(j=>j.AccountId==me&&j.CancelledAt==null);
            var recent=await jobs.CountAsync(j=>j.CreatedAt>now.AddHours(-1),token)+await db.MediaItems.CountAsync(i=>i.AccountId==me&&i.Purpose==null&&i.CreatedAt>now.AddHours(-1),token);
            if(recent>=MediaPolicy.UploadsPerHour)return Problem(429,"upload-limit","You've uploaded a lot of photos in the last hour. Try again later.");
            var kept=await jobs.CountAsync(token)+await db.MediaItems.CountAsync(i=>i.AccountId==me&&i.Purpose==null&&i.RemovedAt==null&&i.Status!=MediaStatus.Removed,token);
            if(kept>=MediaPolicy.MaxKept)return Problem(409,"photo-limit","Delete a photo before adding another.");
            var job=await accounts.Services.GetRequiredService<ImagePipelineStore>().CreateAsync(me,now,token);
            var capability=new ImageUploadCapability(1,job.ImageId,job.UploadId,job.Id,job.SourceKey,job.Purpose,ImagePipelineProtocol.MaxBytes,job.UploadExpiresAt.ToUnixTimeSeconds(),job.LifecycleRevision);
            var signed=options.Sign(capability);
            return Results.Ok(new{protocolVersion=1,imageId=job.ImageId,uploadId=job.UploadId,jobId=job.Id,uploadUrl=new Uri(options.UploadOrigin!,"uploads").AbsoluteUri,uploadToken=signed,method="PUT",expiresAt=capability.ExpiresAt,maxBytes=capability.MaxBytes,state="pending",deliverable=false,mode="staging-only"});
        }).RequireAuthorization();
        app.MapGet("/api/me/photos/uploads/{image:guid}",async(Guid image,ClaimsPrincipal user,AccountResolver accounts,CancellationToken token)=>
        {
            if(!options.Enabled)return Results.NotFound();var(current,denied)=await MemberAsync(user,accounts,token);if(denied is not null)return denied;
            var row=await accounts.Db!.ImageProcessingJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.ImageId==image&&j.AccountId==current!.Profile!.AccountId,token);if(row is null)return Results.NotFound();
            // Do not disclose known-content classifications, scanner detections or source keys to members.
            var state=row.CancelledAt is not null?"removed":row.Outcome is "blocked" or "malware" or "unsupported"?"refused":row.CompletedAt is null?"pending":"pending-review";
            return Results.Ok(new{imageId=row.ImageId,state,deliverable=false});
        }).RequireAuthorization();
        app.MapDelete("/api/me/photos/uploads/{image:guid}",async(Guid image,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,CancellationToken token)=>
        {
            if(!options.Enabled)return Results.NotFound();var(current,denied)=await MemberAsync(user,accounts,token);if(denied is not null)return denied;
            return await accounts.Services.GetRequiredService<ImagePipelineStore>().CancelAsync(image,current!.Profile!.AccountId,clock.GetUtcNow(),token)?Results.NoContent():Results.NotFound();
        }).RequireAuthorization();
    }
    private static IResult? ServiceGate(HttpRequest request,AccountResolver accounts,ImagePipelineOptions options)=>!options.Enabled?Results.NotFound():!options.Authenticates(request.Headers.Authorization)?Results.Unauthorized():accounts.Db is null?Results.Json(new{code="persistence-unavailable"},statusCode:503):null;
    private static IResult Result(ImagePipelineOperation result)=>Results.Json(result.Body,statusCode:result.Status);
    private static IResult Invalid()=>Results.Json(new{code="invalid-request",permanent=true},statusCode:400);
    private static async Task<JsonElement?> ReadAsync(HttpRequest request,CancellationToken token)
    {
        if(request.ContentLength>65536||!request.HasJsonContentType())return null;
        try
        {
            using var memory=new MemoryStream();var chunk=new byte[4096];int read;
            while((read=await request.Body.ReadAsync(chunk,token))>0){if(memory.Length+read>65536)return null;memory.Write(chunk,0,read);}
            using var json=JsonDocument.Parse(memory.ToArray(),new JsonDocumentOptions{MaxDepth=16});return json.RootElement.ValueKind==JsonValueKind.Object?json.RootElement.Clone():null;
        }
        catch(Exception error)when(error is JsonException or IOException or BadHttpRequestException){return null;}
    }
}
