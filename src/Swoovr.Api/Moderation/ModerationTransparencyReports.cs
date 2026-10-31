using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;

/// <summary>Internal aggregate revisions, never publication or a compliance attestation.</summary>
public sealed class ModerationTransparencyReports(SwoovrDbContext db,ModerationTransparencyQuery query,TimeProvider clock)
{
    public async Task<ModerationTransparencyExport> ReadAsync(int year,CancellationToken token)
    {
        _=ModerationTransparencyPolicy.Period(year,clock.GetUtcNow());
        var saved=await db.Set<ModerationTransparencySnapshot>().AsNoTracking().Where(s=>s.EndingYear==year).OrderByDescending(s=>s.Revision).FirstOrDefaultAsync(token);
        return saved is null?await query.ExportAsync(year,token):JsonSerializer.Deserialize<ModerationTransparencyExport>(saved.AggregateJson)
            ??throw new InvalidOperationException("Stored aggregate revision is unavailable.");
    }
    public async Task<int> SaveAsync(int year,CancellationToken token)
    {
        var period=ModerationTransparencyPolicy.Period(year,clock.GetUtcNow());
        if(period.From<clock.GetUtcNow().AddMonths(-36))throw new ArgumentException("Expired source windows cannot be recomputed; read their archived revision.");
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        return await SaveCoreAsync(year,false,token);
    }
    /// <summary>Called only before the first due source expiry while all recorded source rows remain present.</summary>
    public async Task ArchiveBeforeExpiryAsync(int year,CancellationToken token)
    {
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        if(await db.Set<ModerationTransparencySnapshot>().AnyAsync(s=>s.EndingYear==year,token))return;
        await SaveCoreAsync(year,true,token);
    }
    private async Task<int> SaveCoreAsync(int year,bool archival,CancellationToken token)
    {
        var result=await query.ExportAsync(year,token);
        var canonical=JsonSerializer.SerializeToUtf8Bytes(new{result.Header.EndingYear,result.Header.Scope,result.Header.Status,result.Counts,result.Automation,result.TrainingStatus,result.OwnInitiativeCoverage,result.DataQuality.CoverageStatus,result.DataQuality.RecordingBeganAt,result.DataQuality.Limitations});
        var fingerprint=Convert.ToHexStringLower(SHA256.HashData(canonical));
        var previous=await db.Set<ModerationTransparencySnapshot>().AsNoTracking().Where(s=>s.EndingYear==year).ToListAsync(token);
        if(previous.FirstOrDefault(s=>s.Fingerprint==fingerprint) is{}same)return same.Revision;
        if(archival&&previous.Count>0)return previous.Max(s=>s.Revision);
        var revision=previous.Count==0?1:checked(previous.Max(s=>s.Revision)+1);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        db.Set<ModerationTransparencySnapshot>().Add(new(){Id=Guid.NewGuid(),EndingYear=year,Revision=revision,GeneratedAt=result.Header.GeneratedAt,FinalizedAt=clock.GetUtcNow(),Fingerprint=fingerprint,AggregateJson=JsonSerializer.Serialize(result)});
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return revision;
    }
}
