using System.Data;
using System.Diagnostics;
using System.Formats.Tar;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Api.Evidence;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Recovery;

public sealed record BackupObject(string Key,string Entry,string Sha256,int Bytes,string ContentType);
public sealed record BackupManifest(int Schema,string Role,DateTimeOffset CapturedAt,string DatabaseSha256,IReadOnlyList<BackupObject> Objects);

/// <summary>One exported PostgreSQL snapshot plus every referenced private media object; stdout is encrypted by the operator pipeline.</summary>
public sealed class BackupBundle(SwoovrDbContext db,IServiceProvider services,EvidenceKeys keys,IConfiguration configuration)
{
    public async Task WriteAsync(Stream output,CancellationToken token)
    {
        var folder=Path.Combine(Path.GetTempPath(),"swoovr-backup-bundle-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        if(!OperatingSystem.IsWindows())File.SetUnixFileMode(folder,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.UserExecute);
        try
        {
            await using var snapshot=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,token);
            var snapshotId=await db.Database.SqlQueryRaw<string>("SELECT pg_export_snapshot() AS \"Value\"").SingleAsync(token);
            var media=await db.MediaItems.AsNoTracking().Where(m=>m.ObjectKey!=null||m.PreservationSourceKey!=null).ToListAsync(token);
            var dump=Path.Combine(folder,"database.dump");
            await DumpAsync(dump,snapshotId,token);
            var references=media.SelectMany(m=>new[]{m.ObjectKey,m.PreservationSourceKey}.Where(k=>k!=null).Select(k=>new{Key=k!,Media=m})).GroupBy(x=>x.Key).OrderBy(g=>g.Key).ToList();
            var store=services.GetService<IMediaStore>();
            if(references.Count>0&&store is null)throw new InvalidOperationException("Private object storage is required for a complete backup.");
            var objects=new List<BackupObject>();
            await using var archive=new TarWriter(output,TarEntryFormat.Pax,leaveOpen:true);
            await archive.WriteEntryAsync(dump,"database.dump",token);
            foreach(var group in references)
            {
                var bytes=await store!.GetAsync(group.Key,token)??throw new IOException("A referenced backup object is unavailable.");
                if(bytes.Length>64*1024*1024)throw new IOException("A backup object exceeds the supported bound.");
                var plain=group.Key.StartsWith("evidence/",StringComparison.Ordinal)
                    ?Convert.FromBase64String(EvidenceSeal.Open(keys.Encryption,bytes)):bytes;
                var hash=SHA256.HashData(plain);
                if(group.Any(x=>!CryptographicOperations.FixedTimeEquals(x.Media.Sha256,hash)))
                    throw new CryptographicException("A referenced private object's integrity cannot be confirmed.");
                var entry="objects/"+Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(group.Key)))+".blob";
                await using var data=new MemoryStream(bytes,false);
                await archive.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile,entry){DataStream=data},token);
                objects.Add(new(group.Key,entry,Convert.ToHexStringLower(SHA256.HashData(bytes)),bytes.Length,
                    group.Key.StartsWith("evidence/",StringComparison.Ordinal)?"application/octet-stream":group.First().Media.ContentType));
            }
            await using var source=File.OpenRead(dump);
            var manifest=new BackupManifest(1,"api",DateTimeOffset.UtcNow,Convert.ToHexStringLower(await SHA256.HashDataAsync(source,token)),objects);
            await using var metadata=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            await archive.WriteEntryAsync(new PaxTarEntry(TarEntryType.RegularFile,"manifest.json"){DataStream=metadata},token);
            await snapshot.CommitAsync(token);
        }
        finally{Directory.Delete(folder,true);}
    }

    private async Task DumpAsync(string destination,string snapshotId,CancellationToken token)
    {
        var connection=new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Swoovr"));
        var start=new ProcessStartInfo(Environment.GetEnvironmentVariable("SWOOVR_PG_DUMP")??"pg_dump")
        {UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(var argument in new[]{"--format=custom","--no-owner","--no-acl","--exclude-schema=swoovr_operations","--snapshot="+snapshotId})start.ArgumentList.Add(argument);
        start.Environment["PGHOST"]=connection.Host;start.Environment["PGPORT"]=connection.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        start.Environment["PGDATABASE"]=connection.Database;start.Environment["PGUSER"]=connection.Username;start.Environment["PGPASSWORD"]=connection.Password;
        start.Environment["PGSSLMODE"]=connection.SslMode switch
        {SslMode.VerifyFull=>"verify-full",SslMode.VerifyCA=>"verify-ca",SslMode.Require=>"require",SslMode.Disable=>"disable",SslMode.Allow=>"allow",_=>"prefer"};
        if(connection.RootCertificate is{Length:>0} certificate)start.Environment["PGSSLROOTCERT"]=certificate;
        using var process=Process.Start(start)??throw new IOException("Backup dump process did not start.");
        try
        {
            await using var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None);
            if(!OperatingSystem.IsWindows())File.SetUnixFileMode(destination,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            var errors=process.StandardError.ReadToEndAsync(token);
            await process.StandardOutput.BaseStream.CopyToAsync(output,token);await process.WaitForExitAsync(token);await errors;
            if(process.ExitCode!=0)throw new IOException("Database snapshot export failed; diagnostics are not echoed.");
        }
        finally{if(!process.HasExited)process.Kill(entireProcessTree:true);}
    }
}
