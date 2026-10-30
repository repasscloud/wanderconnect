using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;

public sealed class TextHarmReviewRefusal(int status,string code):Exception("Text review refused.")
{ public int Status{get;}=status; public string Code{get;}=code; }

/// <summary>Restricted own-initiative review; member reports, detector output and case existence never confer access.</summary>
public sealed class TextHarmCandidateReviewService(SwoovrDbContext db,EvidenceKeys keys,TimeProvider clock)
{
    private static TextHarmReviewRefusal Refuse(int status,string code)=>new(status,code);
    public async Task<IReadOnlyList<TextHarmCandidateSummary>> ListAsync(Guid actor,Guid caseId,CancellationToken token)
    {
        var subject=await SubjectAsync(caseId,token);
        await using var owners=await AccountLifecycleLease.AcquireManyAsync(db,[actor,subject],token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        await using var mutex=await PreservationLease.CaseAsync(db,caseId,token);
        var item=await AuthorizeAsync(actor,caseId,subject,token);
        if(item.EvidencePurgedAt is not null)return [];
        // Pending first, then newest terminal reviews; bounded metadata only, never plaintext/list decryption.
        var rows=await db.Set<TextHarmReview>().AsNoTracking().Where(r=>r.CaseId==caseId&&r.SubjectAccountId==subject&&r.EvidenceDisposedAt==null)
            .OrderBy(r=>r.ReviewedAt!=null).ThenByDescending(r=>r.CreatedAt).ThenBy(r=>r.Id).Take(100)
            .Select(r=>new TextHarmCandidateSummary(r.Id,r.Source.ToString(),r.ModeId,r.CreatedAt,r.ReviewedAt,r.Outcome)).ToListAsync(token);
        return rows;
    }
    public async Task<TextHarmCandidateEvidence> ReadAsync(Guid actor,Guid caseId,Guid reviewId,CancellationToken token)
    {
        var subject=await SubjectAsync(caseId,token);
        await using var owners=await AccountLifecycleLease.AcquireManyAsync(db,[actor,subject],token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        await using var mutex=await PreservationLease.CaseAsync(db,caseId,token);
        var item=await AuthorizeAsync(actor,caseId,subject,token);
        var row=await FindAsync(caseId,reviewId,subject,token);
        if(item.EvidencePurgedAt is not null)throw Refuse(404,"text-review-evidence-expired");
        var snapshot=Open(row);
        Audit.Record(db,clock.GetUtcNow(),actor,"account","text-harm.evidence-read","text-harm-review",row.Id.ToString());
        // Audit must commit before returning any decrypted text; no exception/body/raw source in audit detail.
        await db.SaveChangesAsync(token);
        return new(row.Id,caseId,row.Source.ToString(),row.ModeId,row.CreatedAt,snapshot.Text,row.Rules,row.Categories,row.ReviewedAt,row.Outcome);
    }
    public async Task<TextHarmCandidateReviewReceipt> MarkAsync(Guid actor,Guid caseId,Guid reviewId,string outcome,Guid? sanctionId,Guid? removalId,CancellationToken token)
    {
        if(outcome is not("reviewed-no-action" or "actioned")
            ||outcome=="reviewed-no-action"&&(sanctionId is not null||removalId is not null)
            ||outcome=="actioned"&&((sanctionId is null)==(removalId is null)))throw Refuse(400,"invalid-text-review");
        var subject=await SubjectAsync(caseId,token);
        await using var owners=await AccountLifecycleLease.AcquireManyAsync(db,[actor,subject],token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        await using var mutex=await PreservationLease.CaseAsync(db,caseId,token);
        var item=await AuthorizeAsync(actor,caseId,subject,token);
        var row=await FindAsync(caseId,reviewId,subject,token);
        if(item.EvidencePurgedAt is not null)throw Refuse(404,"text-review-evidence-expired");
        _=Open(row); // No marking unreadable/disposed evidence as reviewed.
        if(outcome=="actioned")
        {
            var actual= sanctionId is{}sanction
                ? await db.Sanctions.AsNoTracking().AnyAsync(s=>s.Id==sanction&&s.CaseId==caseId&&s.AccountId==subject&&s.StartsAt>=row.CreatedAt,token)
                : await db.Set<ContentRemoval>().AsNoTracking().AnyAsync(r=>r.Id==removalId&&r.CaseId==caseId&&r.SubjectAccountId==subject&&r.RemovedAt>=row.CreatedAt
                    &&(row.Source==TextHarmSource.Message?r.Field=="message"&&r.MessageId==row.SourceId:
                       row.Source==TextHarmSource.ModeAbout?r.Field=="mode-about"&&r.ModeId==row.ModeId:r.Field=="about"),token);
            if(!actual)throw Refuse(409,"text-review-action-required");
        }
        if(row.ReviewedAt is{}reviewed)
        {
            if(row.Outcome!=outcome||row.ReviewedBy!=actor)throw Refuse(409,"text-review-already-recorded");
            return new(row.Id,caseId,outcome,reviewed,true);
        }
        if(item.Status==CaseStatus.Closed)throw Refuse(409,"text-review-closed");
        var now=clock.GetUtcNow();
        await using var tx=await db.Database.BeginTransactionAsync(token);
        row.Outcome=outcome;row.ReviewedAt=now;row.ReviewedBy=actor;item.UpdatedAt=now;
        // Positive action reference is restricted IDs-only audit evidence; never a human reason or raw content.
        var action=sanctionId is{}s?"sanction:"+s:removalId is{}r?"content-removal:"+r:"none";
        Audit.Record(db,now,actor,"account","text-harm.reviewed","text-harm-review",row.Id.ToString(),outcome+":"+action);
        await db.SaveChangesAsync(token);await tx.CommitAsync(token);
        return new(row.Id,caseId,outcome,now,false);
    }
    private async Task<Guid> SubjectAsync(Guid id,CancellationToken token)=>await db.ModerationCases.AsNoTracking().Where(c=>c.Id==id)
        .Select(c=>(Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token)??throw Refuse(404,"text-review-not-available");
    private async Task<ModerationCase> AuthorizeAsync(Guid actor,Guid id,Guid subject,CancellationToken token)
    {
        var item=await db.ModerationCases.SingleOrDefaultAsync(c=>c.Id==id&&c.SubjectAccountId==subject,token)??throw Refuse(404,"text-review-not-available");
        await db.Entry(item).ReloadAsync(token);
        var staff=await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==actor&&a.Realm==AccountRealm.Staff&&a.Status==AccountStatus.Active,token);
        var role=staff?.IsRootAdmin==true||await db.RoleGrants.AnyAsync(g=>g.AccountId==actor&&g.Role==AccountRole.Admin,token)?AccountRole.Admin:
            await db.RoleGrants.AnyAsync(g=>g.AccountId==actor&&g.Role==AccountRole.Support,token)?AccountRole.Support:AccountRole.Unknown;
        var affiliated=await db.MemberProfiles.AnyAsync(p=>p.AccountId==subject&&p.Employee,token);
        var people=await SafetyRetention.CaseAccountsAsync(db,id,token);
        if(staff is null||!ContentRemovalPolicy.Allows(role,item.Queue,affiliated,await StaffAccessEndpoints.ExcludedAsync(db,actor,people,token)))
            throw Refuse(403,"text-review-not-permitted");
        return item;
    }
    private async Task<TextHarmReview> FindAsync(Guid caseId,Guid reviewId,Guid subject,CancellationToken token)
    {
        var row=await db.Set<TextHarmReview>().SingleOrDefaultAsync(r=>r.Id==reviewId&&r.CaseId==caseId&&r.SubjectAccountId==subject,token)
            ??throw Refuse(404,"text-review-not-available");
        await db.Entry(row).ReloadAsync(token);
        return row;
    }
    private TextHarmSnapshot Open(TextHarmReview row)
    {
        if(row.SealedEvidence is null||row.EvidenceDisposedAt is not null)throw Refuse(404,"text-review-evidence-expired");
        try
        {
            var value=JsonSerializer.Deserialize<TextHarmSnapshot>(EvidenceSeal.Open(keys.Encryption,row.SealedEvidence));
            if(value is null||value.Source!=row.Source||value.SourceId!=row.SourceId||value.Revision.ToString("D")!=row.SourceRevision
                ||value.ModeId!=row.ModeId||value.Version!=row.DetectorVersion)throw Refuse(503,"text-review-evidence-unavailable");
            return value;
        }
        catch(Exception error)when(error is System.Security.Cryptography.CryptographicException or KeyNotFoundException or JsonException or ArgumentException)
        {throw Refuse(503,"text-review-evidence-unavailable");}
    }
}
