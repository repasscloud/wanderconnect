using System.Data;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Recovery;

public sealed class RestoreReconciliation(SwoovrDbContext db,IServiceProvider services,EvidenceKeys keys,RetentionJobs retention)
{
    private sealed record Receipt(string OperationId,string Role,bool Ready,string AuthoritySha256,string AuthorityPayloadSha256,DateTimeOffset AuthorityCapturedAt,DateTimeOffset AuthorityReplayedAt);
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,MaxDepth=16};

    public async Task RunAsync(string target,string archivePath,string authoritySha,CancellationToken token)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(target,"^swoovr_restore_[0-9a-f]{32}$")||db.Database.GetDbConnection().Database!=target
            ||!System.Text.RegularExpressions.Regex.IsMatch(authoritySha,"^[0-9a-f]{64}$"))
            throw new InvalidOperationException("Only the bound quarantined target and authority digest may be reconciled.");
        await db.Database.OpenConnectionAsync(token);
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_lock(hashtextextended('swoovr:restore',0))",token);
        try
        {
            var connection=db.Database.GetDbConnection();await using var command=connection.CreateCommand();
            command.CommandText="SELECT row_to_json(g)::text FROM swoovr_operations.restore_guard g WHERE id=1";
            var raw=await command.ExecuteScalarAsync(token) as string??throw new InvalidOperationException("A fenced replay receipt is required.");
            using var state=JsonDocument.Parse(raw);var row=state.RootElement;
            var receipt=new Receipt(row.GetProperty("operation_id").GetString()!,row.GetProperty("role").GetString()!,row.GetProperty("ready").GetBoolean(),
                row.GetProperty("authority_sha256").GetString()!,row.GetProperty("authority_payload_sha256").GetString()!,
                row.GetProperty("authority_captured_at").GetDateTimeOffset(),row.GetProperty("authority_replayed_at").GetDateTimeOffset());
            var now=DateTimeOffset.UtcNow;
            if(receipt.Ready||receipt.Role!="api"||receipt.AuthoritySha256!=authoritySha||receipt.AuthorityCapturedAt>now.AddSeconds(5)
                ||now-receipt.AuthorityCapturedAt>TimeSpan.FromHours(1)||receipt.AuthorityReplayedAt<receipt.AuthorityCapturedAt.AddSeconds(-5))
                throw new InvalidOperationException("Current operation-bound authority must be replayed before reconciliation.");
            await using(var input=File.OpenRead(archivePath))
                if(Convert.ToHexStringLower(await SHA256.HashDataAsync(input,token))!=receipt.AuthorityPayloadSha256)
                    throw new CryptographicException("Authenticated authority payload does not match this replay.");
            var manifest=await ValidateAsync(archivePath,token);
            var rows=await db.MediaItems.AsNoTracking().Where(m=>m.ObjectKey!=null||m.PreservationSourceKey!=null).ToListAsync(token);
            var references=rows.SelectMany(m=>new[]{m.ObjectKey,m.PreservationSourceKey}.Where(k=>k!=null).Select(k=>new{Key=k!,Media=m})).GroupBy(x=>x.Key).ToDictionary(g=>g.Key,g=>g.ToList());
            if(manifest.Objects.Select(o=>o.Key).Distinct().Count()!=manifest.Objects.Count||!references.Keys.Order().SequenceEqual(manifest.Objects.Select(o=>o.Key).Order()))
                throw new InvalidOperationException("Authority media membership does not match the current replayed database.");
            var store=services.GetService<IMediaStore>();if(references.Count>0&&store is null)throw new InvalidOperationException("Private storage is required before restoration may serve.");
            var objects=manifest.Objects.ToDictionary(o=>o.Entry);
            await using(var input=File.OpenRead(archivePath))
            await using(var reader=new TarReader(input))
            {
                TarEntry? entry;
                while((entry=await reader.GetNextEntryAsync(false,token)) is not null)
                {
                    if(!objects.TryGetValue(entry.Name,out var item))continue;
                    if(entry.Length>64*1024*1024)throw new IOException("Restored object exceeds the supported bound.");
                    using var buffer=new MemoryStream();await entry.DataStream!.CopyToAsync(buffer,token);var bytes=buffer.ToArray();
                    var plain=item.Key.StartsWith("evidence/",StringComparison.Ordinal)?Convert.FromBase64String(EvidenceSeal.Open(keys.Encryption,bytes)):bytes;
                    var expected=SHA256.HashData(plain);
                    if(references[item.Key].Any(r=>!CryptographicOperations.FixedTimeEquals(expected,r.Media.Sha256)))
                        throw new CryptographicException("Restored object does not match its current database reference.");
                    var existing=await store!.GetAsync(item.Key,token);
                    if(existing is not null&&!CryptographicOperations.FixedTimeEquals(SHA256.HashData(existing),SHA256.HashData(bytes)))
                        throw new CryptographicException("Existing private storage differs; separate operator recovery is required.");
                    if(existing is null)await store.PutAsync(item.Key,bytes,item.ContentType,token);
                    var readback=await store.GetAsync(item.Key,token)??throw new IOException("Restored object is unavailable.");
                    if(Convert.ToHexStringLower(SHA256.HashData(readback))!=item.Sha256)throw new CryptographicException("Restored object readback failed.");
                }
            }
            var result=await retention.RunAsync(now,token,restoring:true);
            if(result.FailedClosed)throw new InvalidOperationException("Preservation/expiry reconciliation failed closed; target remains fenced.");
            // Revoke every restored API session and device address. No old target queues may use a previous device binding.
            await using var transaction=await db.Database.BeginTransactionAsync(token);
            var cutoff=SessionRevocationPolicy.Cutoff(DateTimeOffset.UtcNow);
            await db.Accounts.Where(a=>a.SessionsRevokedAt==null||a.SessionsRevokedAt<cutoff).ExecuteUpdateAsync(set=>set.SetProperty(a=>a.SessionsRevokedAt,cutoff),token);
            await db.PushDevices.ExecuteDeleteAsync(token);
            var completed=DateTimeOffset.UtcNow;
            var changed=await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE swoovr_operations.restore_guard SET ready=true,reconciled_at={completed} WHERE id=1 AND NOT ready AND role='api' AND authority_sha256={authoritySha} AND authority_payload_sha256={receipt.AuthorityPayloadSha256}",token);
            if(changed!=1)throw new InvalidOperationException("Restore approval changed; no serving receipt committed.");
            Audit.Record(db,completed,null,"restore","restore.reconciled","restore",receipt.OperationId);
            await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_unlock(hashtextextended('swoovr:restore',0))",CancellationToken.None);
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task<BackupManifest> ValidateAsync(string path,CancellationToken token)
    {
        var seen=new Dictionary<string,(long Bytes,string Sha)>();BackupManifest? manifest=null;
        await using var input=File.OpenRead(path);await using var reader=new TarReader(input);TarEntry? entry;
        while((entry=await reader.GetNextEntryAsync(false,token)) is not null)
        {
            if(entry.EntryType!=TarEntryType.RegularFile||!seen.TryAdd(entry.Name,(entry.Length,""))||entry.DataStream is null)
                throw new InvalidOperationException("Duplicate/nonregular archive entry refused.");
            if(entry.Name=="manifest.json")
            {
                if(entry.Length>64*1024*1024)throw new IOException("Authority manifest exceeds supported bound.");
                using var data=new MemoryStream();await entry.DataStream.CopyToAsync(data,token);
                using var document=JsonDocument.Parse(data.ToArray());
                if(document.RootElement.EnumerateObject().Select(p=>p.Name).Distinct().Count()!=document.RootElement.EnumerateObject().Count())throw new JsonException("Duplicate manifest field.");
                manifest=JsonSerializer.Deserialize<BackupManifest>(data.ToArray(),Json)??throw new JsonException("Missing authority manifest.");
            }
            else seen[entry.Name]=(entry.Length,Convert.ToHexStringLower(await SHA256.HashDataAsync(entry.DataStream,token)));
        }
        if(manifest is not{Schema:1,Role:"api"}||manifest.Objects is null||manifest.CapturedAt.Offset!=TimeSpan.Zero
            ||!seen.TryGetValue("database.dump",out var database)||database.Sha!=manifest.DatabaseSha256||seen.Count!=manifest.Objects.Count+2)
            throw new InvalidOperationException("Complete verified database/media authority is required.");
        foreach(var item in manifest.Objects)
        {
            var expected="objects/"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(item.Key)))+".blob";
            if(item.Entry!=expected||!seen.TryGetValue(item.Entry,out var data)||data.Bytes!=item.Bytes||data.Sha!=item.Sha256
                ||item.Bytes<0||item.Bytes>64*1024*1024||string.IsNullOrWhiteSpace(item.ContentType)||item.ContentType.Length>255||item.ContentType.Contains('\r')||item.ContentType.Contains('\n'))
                throw new InvalidOperationException("Malformed or changed authority media entry.");
        }
        return manifest;
    }
}
