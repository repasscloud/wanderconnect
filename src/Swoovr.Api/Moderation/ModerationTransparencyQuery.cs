using System.Data;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;

/// <summary>New receipt aggregates plus explicit legacy gaps. Caller must enforce live staff Admin authority; not a public service.</summary>
public sealed class ModerationTransparencyQuery(SwoovrDbContext db,TimeProvider clock)
{
    public async Task<ModerationTransparencyExport> ExportAsync(int endingYear,CancellationToken token)
    {
        var now=clock.GetUtcNow();
        var period=ModerationTransparencyPolicy.Period(endingYear,now);
        // One database snapshot, so concurrent action/retention cannot produce mutually inconsistent cells.
        // Endpoint audits AFTER this read-only transaction commits; never write under this snapshot.
        if(db.Database.CurrentTransaction is not null)throw new InvalidOperationException("Aggregate export requires its own read snapshot.");
        await using var snapshot=await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead,token);
        var all=db.Set<ModerationTransparencyFact>().AsNoTracking();
        var periodFacts=all.Where(f=>f.ActionAt>=period.From&&f.ActionAt<period.Until);
        var recorded=periodFacts.Where(f=>f.Environment=="production");
        var terminations=recorded.Where(f=>f.Action=="termination");
        var au=terminations.Where(f=>f.HomeCountryAtAction=="AU"&&f.CountryProvenance=="member-declared-home-at-action");
        var bases=ModerationTransparencyPolicy.Bases;
        var detection=ModerationTransparencyPolicy.Detection;
        var enforcement=ModerationTransparencyPolicy.Enforcement;
        var initiatives=ModerationTransparencyPolicy.Initiative;
        var definitions=ModerationTransparencyPolicy.AutomationDefinitions;
        var basisCounts=await au.GroupBy(f=>f.BasisReviewed&&bases.Contains(f.ReviewedPolicyBasis)?f.ReviewedPolicyBasis:"unknown")
            .Select(g=>new{Key=g.Key,Count=g.Select(f=>f.SubjectAccountId).Distinct().LongCount()}).ToListAsync(token);
        var content=recorded.Where(f=>f.Action=="content-removal");
        var contentCounts=await content.GroupBy(f=>new{
            Detection=detection.Contains(f.DetectionMechanism)?f.DetectionMechanism:"unknown",
            Enforcement=enforcement.Contains(f.EnforcementMechanism)?f.EnforcementMechanism:"unknown"})
            .Select(g=>new{g.Key.Detection,g.Key.Enforcement,Count=g.LongCount()}).ToListAsync(token);
        var initiativeCounts=await recorded.GroupBy(f=>initiatives.Contains(f.Initiative)?f.Initiative:"unknown")
            .Select(g=>new{Key=g.Key,Count=g.LongCount()}).ToListAsync(token);
        var automationCounts=await recorded.GroupBy(f=>definitions.Contains(f.AutomationDefinition)?f.AutomationDefinition:"unknown")
            .Select(g=>new{Key=g.Key,Count=g.LongCount()}).ToListAsync(token);
        var rows=new List<(string Metric,string Category,long Count)>();
        rows.Add(("terminated-declared-au-unique-accounts","all",await au.Select(f=>f.SubjectAccountId).Distinct().LongCountAsync(token)));
        rows.AddRange(bases.Select(b=>("terminated-declared-au-unique-accounts-by-reviewed-basis",b,basisCounts.SingleOrDefault(x=>x.Key==b)?.Count??0L)));
        rows.Add(("termination-decisions-country-unknown","unknown",await terminations.Where(f=>f.CountryProvenance!="member-declared-home-at-action"||f.HomeCountryAtAction!="AU"&&f.HomeCountryAtAction!="other").LongCountAsync(token)));
        rows.AddRange(from d in detection from e in enforcement select ("content-removal-decisions",d+"/"+e,contentCounts.SingleOrDefault(x=>x.Detection==d&&x.Enforcement==e)?.Count??0L));
        rows.AddRange(initiatives.Select(i=>("recorded-action-decisions-by-initiative",i,initiativeCounts.SingleOrDefault(x=>x.Key==i)?.Count??0L)));
        rows.AddRange(definitions.Select(d=>("recorded-action-decisions-by-automation",d,automationCounts.SingleOrDefault(x=>x.Key==d)?.Count??0L)));
        var fromDay=DateOnly.FromDateTime(period.From.UtcDateTime);var untilDay=DateOnly.FromDateTime(period.Until.UtcDateTime);
        var inspectionRows=await db.Set<ModerationInspectionTotal>().AsNoTracking().Where(i=>i.Environment=="production"&&i.Day>=fromDay&&i.Day<untilDay)
            .GroupBy(i=>i.Definition).Select(g=>new{Definition=g.Key,Completed=g.Sum(i=>i.CompletedInspections),Incomplete=g.Sum(i=>i.IncompleteHistory),Candidates=g.Sum(i=>i.CreatedCandidates),Failures=g.Sum(i=>i.FailedProcessingAttempts)}).ToListAsync(token);
        foreach(var definition in new[]{ModerationInspectionRecorder.TextDefinition,ModerationInspectionRecorder.ImageDefinition})
        {
            var counts=inspectionRows.SingleOrDefault(i=>i.Definition==definition);
            rows.Add(("recorded-completed-inspection-transitions",definition,counts?.Completed??0));
            rows.Add(("recorded-incomplete-history-inspections",definition,counts?.Incomplete??0));
            rows.Add(("recorded-created-review-candidates",definition,counts?.Candidates??0));
            rows.Add(("recorded-failed-processing-attempts",definition,counts?.Failures??0));
        }
        // These are identifiable source totals internally; the export contains counts only. No current-profile country join.
        var legacyTerminations=await db.Sanctions.AsNoTracking().Where(s=>s.Kind==SanctionKind.Termination&&s.StartsAt>=period.From&&s.StartsAt<period.Until
            &&!all.Any(f=>f.SourceKind=="sanction"&&f.SourceId==s.Id)).LongCountAsync(token);
        var legacyRemovals=await db.Set<ContentRemoval>().AsNoTracking().Where(r=>r.RemovedAt>=period.From&&r.RemovedAt<period.Until
            &&!all.Any(f=>f.SourceKind=="content-removal"&&f.SourceId==r.Id)).LongCountAsync(token);
        rows.Add(("retained-legacy-termination-decisions-missing-receipt","unknown-country-and-basis",legacyTerminations));
        rows.Add(("retained-legacy-text-removals-missing-receipt","unknown-mechanisms",legacyRemovals));
        rows.Add(("receipt-environment-unknown","excluded",await periodFacts.Where(f=>f.Environment!="production"&&f.Environment!="local-synthetic").LongCountAsync(token)));
        var recordingBegan=await all.Where(f=>f.Environment=="production").Select(f=>(DateTimeOffset?)f.RecordedAt).MinAsync(token);
        await snapshot.CommitAsync(token);
        var disclosed=ModerationTransparencyPolicy.Disclose(rows.Select(r=>r.Count));
        var suppressed=disclosed.Any(n=>n is null);
        var cells=rows.Select((r,i)=>new ModerationTransparencyCell(r.Metric,r.Category,disclosed[i],suppressed?"suppressed":"recorded-subset")).ToList();
        foreach(var metric in new[]{"eligible-content-writes","independently-labelled-reviews","classifier-error-rates","completed-training-sessions"})
            cells.Add(new(metric,"not-recorded",null,"not-recorded"));
        return new(new(endingYear,period.From,period.Until,"UTC",period.PublicationBenchmark,now,
            "production-labelled action receipts; retained legacy gaps separately; declared home country, not verified residence","draft-benchmark-not-compliance"),
            new("incomplete-not-a-population-total",suppressed||recordingBegan is null?null:new DateTimeOffset(recordingBegan.Value.Year,recordingBegan.Value.Month,1,0,0,0,TimeSpan.Zero),now,[
                "No established complete metrics pipeline or historical country-at-action/policy-basis reconstruction.",
                "Earliest receipt month is not proof of continuous or complete recording; it is hidden with small-cell suppression.",
                "Inspection transitions, new review candidates and failed processing attempts are recorded subsets, not unique content/accounts, coverage, classifier mistakes or false-positive rates.",
                "Retained legacy counts omit already disposed records and do not establish historical Australian totals.",
                "Zero means no rows in the recorded subset, not no events in the service.",
                "AU means member-declared home country at action, never verified residence or current-profile inference.",
                "An account may appear in several policy bases; basis counts are not additive. Decisions are not unique complaints or content items.",
                "Classifier candidates/no-action cases do not establish accuracy, false-positive or false-negative rates.",
                "All numeric cells are suppressed if any measured cell is between one and four; no per-account filters."]),
            cells,[
                new("local-text-rules-v1","flag potential text harm for human review","required before sanction","not-recorded",null,null,"recorded processing-attempt counts only; classifier error rates unmeasured"),
                new("image-screening-unversioned","screen uploaded images; provider/version completeness unknown","uncertain images need human review; known-content matches may be blocked automatically","not-recorded",null,null,"recorded processing-attempt counts only; classifier error rates unmeasured")
            ],"not-recorded; no completed training asserted","recorded action initiative and anonymous inspection/candidate transitions; eligible-write population and continuous coverage remain unknown");
    }
}
