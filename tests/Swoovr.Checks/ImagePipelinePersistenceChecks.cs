using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Api.Media;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class ImagePipelinePersistenceChecks
{
    public static async Task Run()
    {
        var source=Environment.GetEnvironmentVariable("SWOOVR_TEST_DATABASE");
        if(string.IsNullOrWhiteSpace(source)){Console.WriteLine("SKIP: image pipeline database checks (SWOOVR_TEST_DATABASE absent).");return;}
        var name="swoovr_image_pipeline_"+Guid.NewGuid().ToString("N");
        var builder=new NpgsqlConnectionStringBuilder(source){Database="postgres"};
        await using var admin=new NpgsqlConnection(builder.ConnectionString);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand($"CREATE DATABASE {name}",admin))await create.ExecuteNonQueryAsync();
        builder.Database=name;
        try
        {
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            var count=0;void Check(bool value,string what){if(!value)throw new Exception("Image pipeline persistence: "+what);count++;}
            Check(!db.Database.HasPendingModelChanges(),"migration matches current model");
            var now=DateTimeOffset.UtcNow;var owner=new Account{Id=Guid.NewGuid(),Issuer="https://fictional.example.test",Subject=Guid.NewGuid().ToString(),Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};db.Add(owner);await db.SaveChangesAsync();
            var config=new ImagePipelineOptions(true,new string('s',32),new string('u',32),new Uri("https://upload.example.test"),"swoovr-images-2026-11-08-v1");
            var store=new ImagePipelineStore(db,config);
            var job=await store.CreateAsync(owner.Id,now,default);
            Check(job.Purpose=="profile"&&job.State=="awaiting-upload","creates private profile staging intent");
            var claimed=await store.ClaimAsync(new(1,job.SourceKey,"etag-one",now,100),now,default);
            Check(claimed.Status==200,"authorised source claimed");
            var delayed=await store.ClaimAsync(new(1,job.SourceKey,"etag-one",now,100),now.AddHours(2),default);Check(delayed.Status==200,"queue delay does not expire valid stored upload");
            var changed=await store.ClaimAsync(new(1,job.SourceKey,"etag-two",now,100),now,default);
            Check(changed.Status==409,"changed source cannot inherit job");
            var eventId=Guid.NewGuid();
            JsonElement Report(Guid id,string outcome="passed",long revision=1,string reason="no-signal")=>JsonSerializer.SerializeToElement(new{protocolVersion=1,eventId=id,jobId=job.Id,imageId=job.ImageId,sourceKey=job.SourceKey,sourceEtag="etag-one",sourceSha256=new string('a',64),lifecycleRevision=revision,policyVersion=config.PolicyVersion,outcome,reason,results=new{malware=new{status="completed",classification="clean"},nudenet=new{status="completed",detections=Array.Empty<object>()},nsfw=new{status="completed",is_nsfw=false},arachnid=new{status="completed",classification="no-known-match"}},variants=new[]{new{name="display",key=$"staged/{job.ImageId:D}/{job.Id:D}/{id:D}/display.webp",contentType="image/webp",width=400,height=400,bytes=100,sha256=new string('b',64)}}});
            var stale=await store.CompleteAsync(Report(Guid.NewGuid(),revision:2),now,default);Check(stale.Status==409,"stale lifecycle refused");
            var invalid=System.Text.Json.Nodes.JsonNode.Parse(Report(Guid.NewGuid()).GetRawText())!;invalid["reason"]=null;
            Check((await store.CompleteAsync(JsonSerializer.SerializeToElement(invalid),now,default)).Status==400,"null report field refused instead of exception");
            var leaked=System.Text.Json.Nodes.JsonNode.Parse(Report(eventId).GetRawText())!;leaked["results"]!["arachnid"]!["raw"]=new System.Text.Json.Nodes.JsonObject{{"private","not persisted"}};
            var savedReport=JsonSerializer.SerializeToElement(leaked);
            var committed=await store.CompleteAsync(savedReport,now,default);Check(committed.Status==200,"result durable");
            Check(job.State=="passed-staged"&&!await db.MediaItems.AnyAsync(i=>i.Id==job.ImageId),"pass cannot create deliverable legacy media");
            Check(job.ResultsJson is not null&&!job.ResultsJson.Contains("not persisted")&&!job.ResultsJson.Contains("raw"),"raw provider fields excluded from persisted findings");
            var duplicate=await store.CompleteAsync(savedReport,now,default);Check(duplicate.Status==200&&await db.ImageProcessingReceipts.CountAsync()==1,"exact duplicate is successful no-op");
            leaked["reason"]="changed";
            var mismatch=await store.CompleteAsync(JsonSerializer.SerializeToElement(leaked),now,default);Check(mismatch.Status==409,"same event changed payload rejected");
            var later=await store.CompleteAsync(Report(Guid.NewGuid()),now,default);Check(later.Status==409,"new event cannot overwrite terminal result");
            Check(await db.AuditEntries.CountAsync(a=>a.Action=="image-pipeline.reported")==1,"duplicate creates no audit side effects");
            job=await store.CreateAsync(owner.Id,now,default);await store.ClaimAsync(new(1,job.SourceKey,"etag-one",now,100),now,default);
            var parallelReport=Report(Guid.NewGuid());
            async Task<ImagePipelineOperation> SendAsync(){await using var context=new SwoovrDbContext(options);return await new ImagePipelineStore(context,config).CompleteAsync(parallelReport,now,default);}
            var parallel=await Task.WhenAll(SendAsync(),SendAsync());Check(parallel.All(r=>r.Status==200),"concurrent duplicate callbacks succeed without double effects");
            Check(await db.ImageProcessingReceipts.CountAsync(r=>r.JobId==job.Id)==1,"parallel receipt uniqueness enforced");
            var cancelled=await store.CreateAsync(owner.Id,now,default);await store.CancelAsync(cancelled.ImageId,owner.Id,now,default);
            var cancelClaim=await store.ClaimAsync(new(1,cancelled.SourceKey,"etag-one",now,100),now,default);Check(cancelClaim.Status==200&&JsonSerializer.Serialize(cancelClaim.Body).Contains("ignore"),"cancelled upload cannot be processed");
            Check(cancelled.LifecycleRevision==2,"cancellation fences old reports");
            job=await store.CreateAsync(owner.Id,now,default);await store.ClaimAsync(new(1,job.SourceKey,"etag-one",now,100),now,default);
            var specialist=System.Text.Json.Nodes.JsonNode.Parse(Report(Guid.NewGuid(),outcome:"blocked",reason:"known-content-match").GetRawText())!;
            specialist["variants"]=new System.Text.Json.Nodes.JsonArray();specialist["results"]!["arachnid"]!["classification"]="csam";
            await store.CancelAsync(job.ImageId,owner.Id,now,default);
            owner.Status=AccountStatus.PendingDeletion;await db.SaveChangesAsync();
            var blocked=await store.CompleteAsync(JsonSerializer.SerializeToElement(specialist),now,default);
            Check(blocked.Status==200&&job.Outcome=="blocked"&&job.CancelledAt is not null,"late specialist evidence recorded without undoing removal");
            var gone=await store.ClaimAsync(new(1,job.SourceKey,"etag-one",now,100),now,default);Check(gone.Status==200,"deleted account gets safe ignore");
            Console.WriteLine($"PASS: image pipeline persistence ({count} checks, isolated PostgreSQL migrations/receipt/replay/lifecycle; no publication).");
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();await using var drop=new NpgsqlCommand($"DROP DATABASE {name} WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
}
