using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;
/// <summary>DEFAULT36month receipt/daily window; internally archive annual aggregates before source expiry.</summary>
public sealed class ModerationTransparencyRetention(SwoovrDbContext db,EvidencePreservation evidence,ModerationTransparencyReports reports,TimeProvider clock)
{
    private static int Year(DateTimeOffset at)=>at.Month>=7?at.Year+1:at.Year;
    public async Task<bool> PurgeAsync(CancellationToken token)
    {
        var now=clock.GetUtcNow();var cutoff=now.AddMonths(-36);var day=DateOnly.FromDateTime(cutoff.UtcDateTime);
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        var held=await evidence.TryHeldAccountsAsync(token);if(held is null)return false;
        if(await db.LegalHolds.AnyAsync(h=>h.RetentionCaseIds==null||h.RelatedAccountIds==null,token))return false;
        var facts=await db.Set<ModerationTransparencyFact>().AsNoTracking().Where(f=>f.ActionAt<cutoff).OrderBy(f=>f.ActionAt).ToListAsync(token);
        var days=await db.Set<ModerationInspectionTotal>().AsNoTracking().Where(i=>i.Day<day).Select(i=>i.Day).Distinct().ToListAsync(token);
        foreach(var year in facts.Select(f=>Year(f.ActionAt)).Concat(days.Select(d=>d.Month>=7?d.Year+1:d.Year)).Distinct().Order())
            await reports.ArchiveBeforeExpiryAsync(year,token);
        foreach(var fact in facts)
        {
            if(held.Contains(fact.SubjectAccountId))continue;
            if(fact.SourceCaseId is{}review)
            {
                if((await SafetyRetention.CaseAccountsAsync(db,review,token)).Any(held.Contains)||await db.LegalHolds.AnyAsync(h=>h.CaseId==review||h.RetentionCaseIds!=null&&h.RetentionCaseIds.Contains(review),token))continue;
            }
            if(await db.EvidenceItems.AnyAsync(e=>e.Kind=="moderation-transparency"&&e.RecordId==fact.Id.ToString(),token))continue;
            if(fact.SourceKind=="media-decision"&&await db.LegalHolds.AnyAsync(h=>h.SourceMediaId==fact.SourceId,token))continue;
            await db.Set<ModerationTransparencyFact>().Where(f=>f.Id==fact.Id&&f.ActionAt<cutoff).ExecuteDeleteAsync(token);
        }
        await db.Set<ModerationInspectionTotal>().Where(i=>i.Day<day).ExecuteDeleteAsync(token);
        return true;
    }
}
