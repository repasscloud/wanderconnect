using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Media;

public sealed record ImagePipelineClaim(int ProtocolVersion,string SourceKey,string SourceEtag,DateTimeOffset? SourceUploadedAt=null,long? SourceBytes=null);
public sealed record ImagePipelineOperation(int Status,object Body);

/// <summary>#294 durable staging inbox. Never publishes MediaItem or enables delivery; cutover adapters remain a release gate.</summary>
public sealed class ImagePipelineStore(SwoovrDbContext db,ImagePipelineOptions options)
{
    public async Task<ImageProcessingJob> CreateAsync(Guid account,DateTimeOffset now,CancellationToken token)
    {
        if(!options.Enabled)throw new InvalidOperationException("Image pipeline disabled.");
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,account,token);
        if(!await db.Accounts.AnyAsync(a=>a.Id==account&&a.Status==AccountStatus.Active,token))throw new InvalidOperationException("Account unavailable.");
        var job=new ImageProcessingJob{Id=Guid.NewGuid(),ImageId=Guid.NewGuid(),UploadId=Guid.NewGuid(),AccountId=account,CreatedAt=now,UploadExpiresAt=now.AddMinutes(10),PolicyVersion=options.PolicyVersion};
        job.SourceKey=$"incoming/{job.ImageId:D}/{job.UploadId:D}";db.Add(job);
        Audit.Record(db,now,account,"account","image-pipeline.intent","image",job.ImageId.ToString(),"staging-only");await db.SaveChangesAsync(token);return job;
    }

    public async Task<ImagePipelineOperation> ClaimAsync(ImagePipelineClaim request,DateTimeOffset now,CancellationToken token)
    {
        if(request.ProtocolVersion!=1||!ImagePipelineProtocol.ValidSourceKey(request.SourceKey)||string.IsNullOrWhiteSpace(request.SourceEtag)||request.SourceEtag.Length>128)return Error(400,"invalid-job");
        var origin=await db.ImageProcessingJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.SourceKey==request.SourceKey,token);
        if(origin is null)return Error(404,"unknown-job");
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,origin.AccountId,token);
        await using var media=await AccountLifecycleLease.AcquireMediaAsync(db,origin.AccountId,token);
        var job=await db.ImageProcessingJobs.SingleAsync(j=>j.Id==origin.Id,token);await db.Entry(job).ReloadAsync(token);
        if(job.CancelledAt is not null||!await AccountAvailableAsync(job.AccountId,now,token))return new(200,new{action="ignore",reason="source-unavailable"});
        if(job.SourceEtag is not null&&job.SourceEtag!=request.SourceEtag)return Error(409,"source-changed");
        if(job.CompletedAt is not null)return new(200,new{action="complete",imageId=job.ImageId,jobId=job.Id});
        if(job.PolicyVersion!=options.PolicyVersion)return Error(409,"policy-version-changed");
        // Permission expiry limits ingress, not queue backlog. Trust only Worker-authenticated R2 HEAD timestamp.
        if(job.ClaimedAt is null&&(request.SourceUploadedAt is null||request.SourceUploadedAt<job.CreatedAt.AddSeconds(-30)||request.SourceUploadedAt>job.UploadExpiresAt.AddSeconds(30)||request.SourceUploadedAt>now.AddSeconds(30)||request.SourceBytes is null or <1 or >ImagePipelineProtocol.MaxBytes))return Error(409,"invalid-upload-time");
        job.SourceEtag=request.SourceEtag;job.UploadedAt??=request.SourceUploadedAt;job.ClaimedAt??=now;job.State="processing";await db.SaveChangesAsync(token);
        return new(200,new{protocolVersion=1,action="process",jobId=job.Id,imageId=job.ImageId,uploadId=job.UploadId,job.SourceKey,job.SourceEtag,job.SourceSha256,job.Purpose,job.LifecycleRevision,maxBytes=ImagePipelineProtocol.MaxBytes,job.PolicyVersion});
    }

    public async Task<ImagePipelineOperation> CompleteAsync(JsonElement report,DateTimeOffset now,CancellationToken token)
    {
        if(!ValidateReport(report,out var parsed))return Error(400,"invalid-report");
        var r=parsed!;var digest=ImagePipelineProtocol.Digest(report);
        var origin=await db.ImageProcessingJobs.AsNoTracking().SingleOrDefaultAsync(j=>j.Id==r.JobId,token);
        if(origin is null)return Error(404,"unknown-job");
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,origin.AccountId,token);
        await using var media=await AccountLifecycleLease.AcquireMediaAsync(db,origin.AccountId,token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        // Serialize a reused event ID even if maliciously paired with another account/job.
        var eventKey=AccountLifecycleLease.Key(r.EventId);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({0x494D4745}, {eventKey})",token);
        var receipt=await db.ImageProcessingReceipts.AsNoTracking().SingleOrDefaultAsync(e=>e.EventId==r.EventId,token);
        if(receipt is not null)return receipt.JobId==r.JobId&&receipt.PayloadDigest==digest?Accepted("duplicate"):Error(409,"event-payload-mismatch");
        var job=await db.ImageProcessingJobs.SingleAsync(j=>j.Id==r.JobId,token);await db.Entry(job).ReloadAsync(token);
        var specialist=r.Outcome=="blocked";
        if(job.ImageId!=r.ImageId||job.SourceKey!=r.SourceKey||job.SourceEtag!=r.SourceEtag||job.ClaimedAt is null||r.Revision>job.LifecycleRevision)return Error(409,"job-state-changed");
        // Late known-content evidence remains recorded even after removal/account unavailability. It never restores access.
        if(!specialist&&(job.LifecycleRevision!=r.Revision||job.CancelledAt is not null||job.CompletedAt is not null))return Error(409,"job-state-changed");
        if(specialist&&job.Outcome=="blocked")return Error(409,"specialist-already-recorded");
        if(job.PolicyVersion!=r.PolicyVersion||r.PolicyVersion!=options.PolicyVersion)return Error(409,"policy-version-changed");
        if(job.SourceSha256 is not null&&job.SourceSha256!=r.SourceSha256)return Error(409,"source-changed");
        if(!specialist&&!await AccountAvailableAsync(job.AccountId,now,token))return Error(409,"account-unavailable");
        job.SourceSha256=r.SourceSha256;job.Outcome=r.Outcome;job.Reason=r.Reason;job.State=r.Outcome+"-staged";job.VariantsJson=report.GetProperty("variants").GetRawText();job.ResultsJson=SanitizedResults(report.GetProperty("results"));
        if(r.Outcome!="pending"){job.CompletedAt=now;job.CompletedEventId=r.EventId;}
        db.Add(new ImageProcessingReceipt{EventId=r.EventId,JobId=job.Id,PayloadDigest=digest,CommittedAt=now});
        Audit.Record(db,now,null,"system","image-pipeline.reported","image",job.ImageId.ToString(),r.Outcome);
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return Accepted(job.State);
    }

    public async Task<bool> CancelAsync(Guid image,Guid account,DateTimeOffset now,CancellationToken token)
    {
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,account,token);
        await using var media=await AccountLifecycleLease.AcquireMediaAsync(db,account,token);
        var job=await db.ImageProcessingJobs.SingleOrDefaultAsync(j=>j.ImageId==image&&j.AccountId==account,token);if(job is null)return false;
        if(job.CancelledAt is null){job.CancelledAt=now;job.LifecycleRevision++;job.State="cancelled";Audit.Record(db,now,account,"account","image-pipeline.cancelled","image",image.ToString(),"staging-only");await db.SaveChangesAsync(token);}return true;
    }
    private async Task<bool> AccountAvailableAsync(Guid account,DateTimeOffset now,CancellationToken token)
    {
        if(!await db.Accounts.AnyAsync(a=>a.Id==account&&a.Status==AccountStatus.Active,token))return false;
        return !Enforcement.For(await db.Sanctions.Where(s=>s.AccountId==account&&s.LiftedAt==null).ToListAsync(token),now).NoPhotoUploads;
    }
    private static ImagePipelineOperation Accepted(string state)=>new(200,new{accepted=true,deliverable=false,state,mode="staging-only"});
    private static ImagePipelineOperation Error(int status,string reason)=>new(status,new{code=reason,permanent=true,deliverable=false});
    private sealed record Parsed(Guid EventId,Guid JobId,Guid ImageId,string SourceKey,string SourceEtag,string SourceSha256,long Revision,string PolicyVersion,string Outcome,string Reason);
    private static bool ValidateReport(JsonElement value,out Parsed? parsed)
    {
        parsed=null;
        try
        {
            if(value.ValueKind!=JsonValueKind.Object||value.GetRawText().Length>65536||HasDuplicateProperties(value)||value.GetProperty("protocolVersion").GetInt32()!=1)return false;
            var eventId=value.GetProperty("eventId").GetGuid();var jobId=value.GetProperty("jobId").GetGuid();var imageId=value.GetProperty("imageId").GetGuid();
            var key=value.GetProperty("sourceKey").GetString()!;var etag=value.GetProperty("sourceEtag").GetString()!;var sha=value.GetProperty("sourceSha256").GetString()!;
            var revision=value.GetProperty("lifecycleRevision").GetInt64();var policy=value.GetProperty("policyVersion").GetString()!;var outcome=value.GetProperty("outcome").GetString()!;var reason=value.GetProperty("reason").GetString()!;
            if(key is null||etag is null||sha is null||policy is null||outcome is null||reason is null)return false;
            if(eventId==Guid.Empty||jobId==Guid.Empty||imageId==Guid.Empty||!ImagePipelineProtocol.ValidSourceKey(key)||!key.StartsWith($"incoming/{imageId:D}/",StringComparison.Ordinal)||string.IsNullOrWhiteSpace(etag)||etag.Length>128||!ImagePipelineProtocol.ValidHash(sha)||revision<1||policy.Length is <1 or >80||reason.Length is <1 or >80||!Regex.IsMatch(reason,"^[a-z0-9][a-z0-9-]*$")||outcome is not("passed" or "review" or "blocked" or "malware" or "pending" or "unsupported"))return false;
            var variants=value.GetProperty("variants");if(variants.ValueKind!=JsonValueKind.Array||variants.GetArrayLength()>4||variants.GetRawText().Length>8192)return false;
            var names=new HashSet<string>(StringComparer.Ordinal);long total=0;
            foreach(var variant in variants.EnumerateArray())
            {
                var name=variant.GetProperty("name").GetString();var variantKey=variant.GetProperty("key").GetString();
                if(name is not("display" or "thumbnail")||!names.Add(name)||variantKey!=$"staged/{imageId:D}/{jobId:D}/{eventId:D}/{name}.webp"||variant.GetProperty("contentType").GetString()!="image/webp"||!ImagePipelineProtocol.ValidHash(variant.GetProperty("sha256").GetString()))return false;
                var width=variant.GetProperty("width").GetInt32();var height=variant.GetProperty("height").GetInt32();var bytes=variant.GetProperty("bytes").GetInt64();
                if(width is <1 or >8192||height is <1 or >8192||bytes is <1 or >10485760)return false;total+=bytes;
            }
            if(total>20971520||(outcome=="passed"||outcome=="review"&&reason!="known-content-test")&&variants.GetArrayLength()==0||outcome is "blocked" or "malware" or "pending" or "unsupported"&&variants.GetArrayLength()!=0)return false;
            var results=value.GetProperty("results");if(results.ValueKind!=JsonValueKind.Object)return false;
            // Validate evidence completeness, not model thresholds: Worker owns automatic policy.
            if(outcome is "passed" or "review")
            {
                foreach(var engine in new[]{"malware","nudenet","nsfw","arachnid"})if(results.GetProperty(engine).GetProperty("status").GetString()!="completed")return false;
                if(results.GetProperty("malware").GetProperty("classification").GetString()!="clean"||results.GetProperty("arachnid").GetProperty("classification").GetString() is not("no-known-match" or "test"))return false;
            }
            if(outcome=="blocked"&&(results.GetProperty("arachnid").GetProperty("status").GetString()!="completed"||results.GetProperty("arachnid").GetProperty("classification").GetString() is not("csam" or "harmful-abusive-material")))return false;
            if(outcome=="malware"&&(results.GetProperty("malware").GetProperty("status").GetString()!="completed"||results.GetProperty("malware").GetProperty("classification").GetString()!="infected"))return false;
            if(outcome=="passed"&&results.GetProperty("arachnid").GetProperty("classification").GetString()!="no-known-match")return false;
            parsed=new(eventId,jobId,imageId,key,etag,sha,revision,policy,outcome,reason);return true;
        }
        catch(Exception exception)when(exception is InvalidOperationException or KeyNotFoundException or FormatException or OverflowException or ArgumentException){return false;}
    }
    private static string SanitizedResults(JsonElement results)
    {
        var summary=new Dictionary<string,object?>();
        foreach(var name in new[]{"malware","nudenet","nsfw","arachnid"})
        {
            if(!results.TryGetProperty(name,out var engine)||engine.ValueKind!=JsonValueKind.Object)continue;
            var fields=new Dictionary<string,object?>();
            foreach(var key in new[]{"status","classification","engineVersion","signatureVersion","signatureTimestamp","modelVersion","modelRevision","modelSha256","adapterVersion","is_nsfw"})
                if(engine.TryGetProperty(key,out var field))
                {
                    if(field.ValueKind==JsonValueKind.String&&field.GetString() is {Length:<=128} text)fields[key]=text;
                    else if(field.ValueKind is JsonValueKind.True or JsonValueKind.False)fields[key]=field.GetBoolean();
                }
            if(name=="nudenet"&&engine.TryGetProperty("detections",out var detections)&&detections.ValueKind==JsonValueKind.Array)
                fields["detections"]=detections.EnumerateArray().Take(100).Where(d=>d.ValueKind==JsonValueKind.Object&&d.TryGetProperty("class",out var c)&&c.ValueKind==JsonValueKind.String&&c.GetString() is {Length:<=64}&&d.TryGetProperty("score",out var score)&&score.ValueKind==JsonValueKind.Number&&score.TryGetDouble(out var number)&&double.IsFinite(number)).Select(d=>new{label=d.GetProperty("class").GetString(),score=d.GetProperty("score").GetDouble()}).ToArray();
            summary[name]=fields;
        }
        return JsonSerializer.Serialize(summary);
    }
    private static bool HasDuplicateProperties(JsonElement value)
    {
        if(value.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var property in value.EnumerateObject())if(!names.Add(property.Name)||HasDuplicateProperties(property.Value))return true;}
        else if(value.ValueKind==JsonValueKind.Array)foreach(var child in value.EnumerateArray())if(HasDuplicateProperties(child))return true;
        return false;
    }
}
