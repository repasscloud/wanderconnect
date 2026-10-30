using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Contracts;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;

/// <summary>Controlled staff-reviewed metadata, not a reporter/category/reason inference. Callers own authorization.</summary>
public sealed record ModerationTransparencyMetadata(string? ReviewedPolicyBasis=null,string DetectionMechanism="unknown",
    string Initiative="unknown",string AutomationDefinition="unknown");

/// <summary>Prospective source+fact staging only. Caller owns source authority and transaction; this never saves or takes a late lease.</summary>
public sealed class ModerationTransparencyRecorder(SwoovrDbContext db,IHostEnvironment environment,IConfiguration configuration,TimeProvider clock)
{
    public async Task<ModerationTransparencyFact> StageSanctionAsync(Sanction source,DateTimeOffset actionAt,
        ModerationTransparencyMetadata? metadata,CancellationToken token)
    {
        if(source.Kind!=SanctionKind.Termination||source.StartsAt!=actionAt)
            throw new InvalidOperationException("Transparency requires the exact termination action.");
        return await StageAsync("sanction",source.Id,source.AccountId,source.CaseId,actionAt,"termination",source,metadata,token);
    }
    public async Task<ModerationTransparencyFact> StageContentRemovalAsync(ContentRemoval source,DateTimeOffset actionAt,
        ModerationTransparencyMetadata? metadata,CancellationToken token)
    {
        if(source.RemovedAt!=actionAt)throw new InvalidOperationException("Transparency requires the exact content action.");
        return await StageAsync("content-removal",source.Id,source.SubjectAccountId,source.CaseId,actionAt,"content-removal",source,metadata,token);
    }
    public async Task StageMediaDecisionAsync(MediaItem item,bool automatedInspection,DateTimeOffset now,CancellationToken token)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Media reporting requires its source transaction.");
        db.ChangeTracker.DetectChanges();
        var status=db.Entry(item).Property(i=>i.Status);
        if(item.Status is not(MediaStatus.Blocked or MediaStatus.Rejected)||status.OriginalValue==status.CurrentValue)return;
        if(db.Entry(item).State!=EntityState.Modified)throw new InvalidOperationException("Media reporting requires a new decision.");
        if(await db.Set<ModerationTransparencyFact>().AnyAsync(f=>f.SourceKind=="media-decision"&&f.SourceId==item.Id,token))
            throw new InvalidOperationException("Media refusal already has an immutable action receipt.");
        var human=item.ReviewedBy is not null;
        var flagged=automatedInspection||human&&status.OriginalValue==MediaStatus.Uncertain&&item.Screener!=null&&item.ScreenedAt!=null;
        var actionAt=human?item.ReviewedAt??now:item.ScreenedAt??now;
        db.Set<ModerationTransparencyFact>().Add(new(){Id=Guid.NewGuid(),SourceKind="media-decision",SourceId=item.Id,SubjectAccountId=item.AccountId,SourceCaseId=item.CaseId,
            Action="content-removal",ActionAt=actionAt,RecordedAt=clock.GetUtcNow(),HomeCountryAtAction="unknown",CountryProvenance="unknown",
            ReviewedPolicyBasis="unknown",BasisReviewed=false,DetectionMechanism=flagged?"image-screening":"unknown",EnforcementMechanism=human?"human":"automated-screening",
            Initiative=flagged?"own-initiative":"unknown",AutomationDefinition=flagged?"image-screening-unversioned":"unknown",Environment=Dataset(environment,configuration)});
    }
    public static async Task<ModerationTransparencyMetadata> ChoiceAsync(SwoovrDbContext db,Guid? caseId,ModerationReportingChoice? choice,CancellationToken token)
    {
        if(choice is null)return new();
        var metadata=choice.SourceKind switch
        {
            "unknown" when choice.SourceId is null=>new ModerationTransparencyMetadata(choice.PolicyBasis),
            "staff-review" when choice.SourceId is null=>new(choice.PolicyBasis,"staff-review","own-initiative","none"),
            "member-report" when caseId is{}review&&choice.SourceId is{}report&&await db.Reports.AnyAsync(r=>r.Id==report&&r.CaseId==review,token)=>new(choice.PolicyBasis,"member-report","reported","none"),
            "local-text-rules" when caseId is{}review&&choice.SourceId is{}candidate&&await db.Set<TextHarmReview>().AnyAsync(r=>r.Id==candidate&&r.CaseId==review&&r.DetectorVersion==BoundedTextHarmDetector.Version,token)=>new(choice.PolicyBasis,"local-text-rules","own-initiative","local-text-rules-v1"),
            _=>throw new ArgumentException("Reporting source must match the reviewed case.")
        };
        Validate(metadata);return metadata;
    }
    private async Task<ModerationTransparencyFact> StageAsync(string kind,Guid sourceId,Guid subject,Guid? caseId,
        DateTimeOffset at,string action,object source,ModerationTransparencyMetadata? supplied,CancellationToken token)
    {
        if(db.Database.CurrentTransaction is null||sourceId==Guid.Empty||subject==Guid.Empty||caseId==Guid.Empty)
            throw new InvalidOperationException("Transparency requires source transaction and provenance.");
        var metadata=supplied??new();Validate(metadata);
        var basis=metadata.ReviewedPolicyBasis is null or "unknown"?"unknown":metadata.ReviewedPolicyBasis;
        var prior=db.Set<ModerationTransparencyFact>().Local.SingleOrDefault(f=>f.SourceKind==kind&&f.SourceId==sourceId)
            ??await db.Set<ModerationTransparencyFact>().SingleOrDefaultAsync(f=>f.SourceKind==kind&&f.SourceId==sourceId,token);
        if(prior is not null)
        {
            // Retry never re-reads current country/environment, nor replaces immutable original metadata after a move/deletion.
            if(db.Entry(source).State==EntityState.Detached||prior.SubjectAccountId!=subject||prior.SourceCaseId!=caseId||prior.ActionAt!=at||prior.Action!=action
                ||prior.ReviewedPolicyBasis!=basis||prior.BasisReviewed!=(basis!="unknown")||prior.DetectionMechanism!=metadata.DetectionMechanism
                ||prior.Initiative!=metadata.Initiative||prior.AutomationDefinition!=metadata.AutomationDefinition||prior.EnforcementMechanism!="human")
                throw new InvalidOperationException("Transparency source receipt conflicts.");
            return prior;
        }
        // Prevent a delayed/historical capture from joining today's profile and mislabelling it country-at-action.
        if(db.Entry(source).State!=EntityState.Added)
            throw new InvalidOperationException("Transparency only records newly added source actions.");
        var recordedAt=clock.GetUtcNow();
        if(at==default||at>recordedAt)throw new InvalidOperationException("Transparency action time is invalid.");
        if(!await db.Accounts.AsNoTracking().AnyAsync(a=>a.Id==subject&&a.Realm==AccountRealm.Members,token))
            throw new InvalidOperationException("Transparency source is not a member action.");
        if(caseId is{}review)
        {
            var tracked=db.ModerationCases.Local.SingleOrDefault(c=>c.Id==review);
            var owner=tracked?.SubjectAccountId??await db.ModerationCases.AsNoTracking().Where(c=>c.Id==review).Select(c=>(Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token);
            if(owner!=subject)throw new InvalidOperationException("Transparency case provenance does not match.");
        }
        string? country=null;
        if(action=="termination")country=await (from profile in db.MemberProfiles.AsNoTracking() join city in db.GeoCities.AsNoTracking()
            on profile.HomeGeoNameId equals (int?)city.GeoNameId where profile.AccountId==subject select city.CountryCode).SingleOrDefaultAsync(token);
        // A trusted GeoNames reference is a declared home-country source, NOT proof of residence/location/identity.
        var known=country is{Length:2}&&country.All(c=>c is >= 'A' and <= 'Z')&&country is not("ZZ" or "XX");
        var fact=new ModerationTransparencyFact{Id=Guid.NewGuid(),SourceKind=kind,SourceId=sourceId,SubjectAccountId=subject,SourceCaseId=caseId,
            Action=action,ActionAt=at,RecordedAt=recordedAt,HomeCountryAtAction=known?(country=="AU"?"AU":"other"):"unknown",
            CountryProvenance=known?"member-declared-home-at-action":"unknown",ReviewedPolicyBasis=basis,BasisReviewed=basis!="unknown",
            DetectionMechanism=metadata.DetectionMechanism,EnforcementMechanism="human",Initiative=metadata.Initiative,
            AutomationDefinition=metadata.AutomationDefinition,Environment=Dataset(environment,configuration)};
        db.Set<ModerationTransparencyFact>().Add(fact);return fact;
    }
    private static void Validate(ModerationTransparencyMetadata metadata)
    {
        if(metadata.ReviewedPolicyBasis is not null&&!ModerationTransparencyPolicy.Bases.Contains(metadata.ReviewedPolicyBasis)
            ||!ModerationTransparencyPolicy.Detection.Contains(metadata.DetectionMechanism)
            ||!ModerationTransparencyPolicy.Initiative.Contains(metadata.Initiative)
            ||!ModerationTransparencyPolicy.AutomationDefinitions.Contains(metadata.AutomationDefinition))
            throw new ArgumentException("Invalid transparency metadata.");
        // Human source hooks cannot claim automatic enforcement. Unknown remains a real data-quality value.
        if(metadata.DetectionMechanism=="local-text-rules"&&metadata.AutomationDefinition!="local-text-rules-v1"
            ||metadata.DetectionMechanism=="image-screening"&&metadata.AutomationDefinition!="image-screening-unversioned"
            ||metadata.AutomationDefinition=="none"&&metadata.DetectionMechanism is "local-text-rules" or "image-screening")
            throw new ArgumentException("Inconsistent transparency mechanism metadata.");
    }
    public static string Dataset(IHostEnvironment environment,IConfiguration configuration)
    {
        var selected=configuration["Reporting:Dataset"];
        if(selected is not(null or "unknown" or "local-synthetic" or "production"))throw new InvalidOperationException("Invalid reporting dataset configuration.");
        if(selected=="production"&&!environment.IsProduction())throw new InvalidOperationException("Non-production host cannot record production reporting data.");
        // .NET's implicit Production default is insufficient to assert real production data.
        return selected??(environment.IsDevelopment()?"local-synthetic":"unknown");
    }
}
