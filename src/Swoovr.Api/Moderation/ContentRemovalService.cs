using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Moderation;

public sealed class ContentRemovalRefusal(int status,string code):Exception("Content removal refused.")
{public int Status{get;}=status;public string Code{get;}=code;}

/// <summary>Case-scoped field removal with independent encrypted evidence and exact-source comparison.</summary>
public sealed class ContentRemovalService(SwoovrDbContext db,EvidenceKeys keys,EvidencePreservation evidence,ModerationTransparencyRecorder reporting)
{
    private sealed record Snapshot(string Text,string Reason,string ExpectedSha256);
    private static ContentRemovalRefusal Refuse(int status,string code)=>new(status,code);
    public async Task<ContentRemovalReceipt> RemoveAsync(Guid actor,Guid caseId,ContentRemovalRequest request,DateTimeOffset now,CancellationToken token)
    {
        if(request.CommandId==Guid.Empty||request.ReportId==Guid.Empty||!ContentRemovalPolicy.ValidTarget(request.Field,request.ModeId,request.MessageId)
            ||string.IsNullOrWhiteSpace(request.Reason)||request.Reason.Trim().Length>ContentRemovalPolicy.ReasonMax)
            throw Refuse(400,"invalid-content-removal");
        var subject=await db.ModerationCases.Where(c=>c.Id==caseId).Select(c=>(Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token);
        if(subject is null)throw Refuse(404,"content-not-available");
        // Every lock needed by member mutation is acquired before preservation; no generic moderation filter wraps this endpoint.
        await using var state=await AccountLifecycleLease.AcquireAsync(db,subject.Value,token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        await using var caseMutex=await PreservationLease.CaseAsync(db,caseId,token);
        var review=await db.ModerationCases.SingleOrDefaultAsync(c=>c.Id==caseId,token);
        if(review is null)throw Refuse(404,"content-not-available");
        await db.Entry(review).ReloadAsync(token);
        if(review.SubjectAccountId!=subject.Value)throw Refuse(409,"content-review-changed");
        var staff=await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==actor&&a.Realm==AccountRealm.Staff&&a.Status==AccountStatus.Active,token);
        var role=staff?.IsRootAdmin==true||await db.RoleGrants.AnyAsync(g=>g.AccountId==actor&&g.Role==AccountRole.Admin,token)?AccountRole.Admin
            :await db.RoleGrants.AnyAsync(g=>g.AccountId==actor&&g.Role==AccountRole.Support,token)?AccountRole.Support:AccountRole.Unknown;
        if(staff is null)throw Refuse(403,"content-removal-not-permitted");
        var profile=await db.MemberProfiles.SingleOrDefaultAsync(p=>p.AccountId==subject.Value,token);
        var people=await SafetyRetention.CaseAccountsAsync(db,caseId,token);
        var conflicted=await StaffAccessEndpoints.ExcludedAsync(db,actor,people,token);
        if(!ContentRemovalPolicy.Allows(role,review.Queue,profile?.Employee==true,conflicted))throw Refuse(403,"content-removal-not-permitted");
        var report=await db.Reports.AsNoTracking().SingleOrDefaultAsync(r=>r.Id==request.ReportId&&r.CaseId==caseId&&r.SubjectAccountId==subject.Value,token);
        if(report is null)throw Refuse(404,"content-not-available");
        if(keys.CurrentVersion is not{}version)throw Refuse(503,"evidence-not-configured");
        var prior=await db.Set<ContentRemoval>().AsNoTracking().SingleOrDefaultAsync(r=>r.Id==request.CommandId,token);
        if(prior is not null)
        {
            if(prior.CaseId!=caseId||prior.ReportId!=request.ReportId||prior.ActorAccountId!=actor||prior.Field!=request.Field
                ||prior.ModeId!=request.ModeId||prior.MessageId!=request.MessageId)throw Refuse(409,"content-command-reused");
            if(prior.SealedSnapshot is null||prior.EvidenceDisposedAt is not null)throw Refuse(409,"content-evidence-expired");
            Snapshot original;
            try{original=JsonSerializer.Deserialize<Snapshot>(Open(prior))
                ??throw Refuse(503,"evidence-not-configured");}
            catch(Exception error)when(error is System.Security.Cryptography.CryptographicException or KeyNotFoundException or JsonException or ArgumentException)
            {throw Refuse(503,"evidence-not-configured");}
            if(original.ExpectedSha256!=request.ExpectedSourceSha256||original.Reason!=request.Reason.Trim())throw Refuse(409,"content-command-reused");
            return new(prior.Id,caseId,prior.Field,prior.RemovedAt,true);
        }
        if(review.Status==CaseStatus.Closed||review.EvidencePurgedAt is not null)throw Refuse(409,"content-review-closed");
        MemberMode? mode=null;Message? message=null;string? text;
        if(request.Field=="message")
        {
            if(report.SubjectType is not(ReportSubject.Message or ReportSubject.Conversation))throw Refuse(409,"message-not-in-report");
            message=await db.Messages.SingleOrDefaultAsync(m=>m.Id==request.MessageId&&m.SenderAccountId==subject.Value&&m.Kind==MessageKind.Text,token);
            if(message is null||message.DeletedAt!=null||message.ModerationRemovedAt!=null)throw Refuse(404,"content-not-available");
            await db.Entry(message).ReloadAsync(token);
            var snapshots=await db.CaseEvidence.AsNoTracking().Where(e=>e.CaseId==caseId&&e.ReportId==request.ReportId).ToListAsync(token);
            if(!snapshots.Any(e=>ContentRemovalPolicy.ReportContainsMessage(e.Kind,e.Snapshot,message.Id)))throw Refuse(409,"message-not-in-report");
            text=message.Body;
        }
        else
        {
            if(report.SubjectType is not(ReportSubject.Profile or ReportSubject.Bot)||profile is null)throw Refuse(409,"profile-not-in-report");
            await db.Entry(profile).ReloadAsync(token);
            if(request.Field=="mode-about")
            {
                mode=await db.MemberModes.SingleOrDefaultAsync(m=>m.AccountId==subject.Value&&m.ModeId==request.ModeId,token);
                if(mode is null)throw Refuse(404,"content-not-available");
                await db.Entry(mode).ReloadAsync(token);text=mode.About;
            }
            else text=profile.About;
        }
        if(string.IsNullOrEmpty(text)||!ContentRemovalPolicy.SameSource(text,request.ExpectedSourceSha256))throw Refuse(409,"content-source-changed");
        // Recapture every applicable live obligation before source loss. Failure leaves the source intact.
        await evidence.PreserveBeforeDeletionAsync(subject.Value,now,token);
        var originalJson=JsonSerializer.Serialize(new Snapshot(text,request.Reason.Trim(),request.ExpectedSourceSha256));
        var sealedSnapshot=EvidenceSeal.Seal(version,keys.Encryption[version],originalJson);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        var removal=new ContentRemoval{Id=request.CommandId,CaseId=caseId,ReportId=request.ReportId,SubjectAccountId=subject.Value,ActorAccountId=actor,
            Field=request.Field,ModeId=request.ModeId,MessageId=request.MessageId,RemovedAt=now,SealedSnapshot=sealedSnapshot,SnapshotHash=EvidenceSeal.Hash(originalJson)};
        db.Set<ContentRemoval>().Add(removal);
        ModerationTransparencyMetadata metadata;
        try{metadata=await ModerationTransparencyRecorder.ChoiceAsync(db,caseId,request.Reporting,token);}
        catch(ArgumentException){throw Refuse(400,"invalid-reporting-metadata");}
        await reporting.StageContentRemovalAsync(removal,now,metadata,token);
        if(message is not null){message.Body="";message.ModerationRemovedAt=now;message.ModerationRemovalId=request.CommandId;}
        else if(mode is not null){mode.About=null;mode.ShareCoreAbout=false;mode.UpdatedAt=now;}
        else profile!.About=null;
        db.CaseEvidence.Add(new(){Id=Guid.NewGuid(),CaseId=caseId,ReportId=request.ReportId,Kind="content-removal",MediaIds=[],CapturedAt=now,
            Snapshot=JsonSerializer.Serialize(new{removalId=request.CommandId,field=request.Field,modeId=request.ModeId,messageId=request.MessageId})});
        var notice=new MemberNotice{Id=Guid.NewGuid(),AccountId=subject.Value,Kind=NoticeKind.ContentRemoved,CreatedAt=now};
        db.MemberNotices.Add(notice);
        db.Notifications.Add(new(){Id=Guid.NewGuid(),AccountId=subject.Value,Kind=NotificationKind.Account,SubjectId=notice.Id,CreatedAt=now,UpdatedAt=now});
        review.UpdatedAt=now;
        Audit.Record(db,now,actor,"account","content.removed","content-removal",request.CommandId.ToString(),request.Field);
        await db.SaveChangesAsync(token);
        if(message is null)await Swoovr.Api.Profiles.ProfilePrivacy.CaptureAsync(db,subject.Value,now,token);
        // Original removal/evidence/notice sources are saved under the existing state/barrier/case authority.
        await ReportOutcomeCapture.InTransactionAsync(db,caseId,now,token);
        await transaction.CommitAsync(token);
        return new(request.CommandId,caseId,request.Field,now,false);
    }
    private string Open(ContentRemoval removal)
    {
        if(removal.SealedSnapshot is null)throw Refuse(404,"content-evidence-expired");
        var json=EvidenceSeal.Open(keys.Encryption,removal.SealedSnapshot);
        if(!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(EvidenceSeal.Hash(json),removal.SnapshotHash))throw Refuse(503,"evidence-not-configured");
        return json;
    }
    private async Task<Guid> SubjectAsync(Guid id,CancellationToken token)=>await db.ModerationCases.Where(c=>c.Id==id).Select(c=>(Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token)??throw Refuse(404,"content-not-available");
    private async Task<ModerationCase> AuthorizeReadAsync(Guid actor,Guid id,Guid subject,CancellationToken token)
    {
        var review=await db.ModerationCases.AsNoTracking().SingleOrDefaultAsync(c=>c.Id==id&&c.SubjectAccountId==subject,token)??throw Refuse(404,"content-not-available");
        var staff=await db.Accounts.AsNoTracking().SingleOrDefaultAsync(a=>a.Id==actor&&a.Realm==AccountRealm.Staff&&a.Status==AccountStatus.Active,token);
        var role=staff?.IsRootAdmin==true||await db.RoleGrants.AnyAsync(g=>g.AccountId==actor&&g.Role==AccountRole.Admin,token)?AccountRole.Admin:await db.RoleGrants.AnyAsync(g=>g.AccountId==actor&&g.Role==AccountRole.Support,token)?AccountRole.Support:AccountRole.Unknown;
        var affiliated=await db.MemberProfiles.AnyAsync(p=>p.AccountId==subject&&p.Employee,token);
        if(staff is null||!ContentRemovalPolicy.Allows(role,review.Queue,affiliated,await StaffAccessEndpoints.ExcludedAsync(db,actor,await SafetyRetention.CaseAccountsAsync(db,id,token),token)))throw Refuse(403,"content-removal-not-permitted");
        return review;
    }
    public async Task<IReadOnlyList<ContentRemovalSource>> SourcesAsync(Guid actor,Guid id,CancellationToken token)
    {
        var subject=await SubjectAsync(id,token);await using var owner=await AccountLifecycleLease.AcquireAsync(db,subject,token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);await using var mutex=await PreservationLease.CaseAsync(db,id,token);
        var review=await AuthorizeReadAsync(actor,id,subject,token);if(review.Status==CaseStatus.Closed||review.EvidencePurgedAt!=null)return [];
        var result=new List<ContentRemovalSource>();
        foreach(var report in await db.Reports.AsNoTracking().Where(r=>r.CaseId==id&&r.SubjectAccountId==subject).OrderByDescending(r=>r.CreatedAt).Take(20).ToListAsync(token))
        {
            void Add(string field,string? mode,Guid? message,string? value){if(!string.IsNullOrEmpty(value)&&result.Count<50)result.Add(new(report.Id,field,mode,message,value,ContentRemovalPolicy.Hash(value)));}
            if(report.SubjectType is ReportSubject.Profile or ReportSubject.Bot)
            {
                Add("about",null,null,await db.MemberProfiles.Where(p=>p.AccountId==subject).Select(p=>p.About).SingleOrDefaultAsync(token));
                foreach(var mode in await db.MemberModes.AsNoTracking().Where(m=>m.AccountId==subject).OrderBy(m=>m.ModeId).ToListAsync(token))Add("mode-about",mode.ModeId,null,mode.About);
            }
            else if(report.SubjectType is ReportSubject.Message or ReportSubject.Conversation)
            {
                var snapshots=await db.CaseEvidence.AsNoTracking().Where(e=>e.CaseId==id&&e.ReportId==report.Id&&(e.Kind=="message"||e.Kind=="conversation")).ToListAsync(token);
                var reportedIds=new HashSet<Guid>();
                foreach(var snapshot in snapshots)
                    try
                    {
                        using var parsed=JsonDocument.Parse(snapshot.Snapshot);
                        if(parsed.RootElement.ValueKind==JsonValueKind.Object&&parsed.RootElement.TryGetProperty("messages",out var rows)&&rows.ValueKind==JsonValueKind.Array)
                            foreach(var row in rows.EnumerateArray().Take(100))
                                if(row.ValueKind==JsonValueKind.Object&&row.TryGetProperty("id",out var value)&&value.ValueKind==JsonValueKind.String&&value.TryGetGuid(out var messageId))reportedIds.Add(messageId);
                    }
                    catch(JsonException){ }
                foreach(var message in await db.Messages.AsNoTracking().Where(m=>m.SenderAccountId==subject&&reportedIds.Contains(m.Id)&&m.Kind==MessageKind.Text&&m.DeletedAt==null&&m.ModerationRemovedAt==null).OrderByDescending(m=>m.SentAt).ToListAsync(token))
                    if(snapshots.Any(e=>ContentRemovalPolicy.ReportContainsMessage(e.Kind,e.Snapshot,message.Id)))Add("message",null,message.Id,message.Body);
            }
        }
        return result;
    }
    public async Task<ContentRemovalEvidence> EvidenceAsync(Guid actor,Guid id,Guid removalId,CancellationToken token)
    {
        var subject=await SubjectAsync(id,token);await using var owner=await AccountLifecycleLease.AcquireAsync(db,subject,token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);await using var mutex=await PreservationLease.CaseAsync(db,id,token);
        var review=await AuthorizeReadAsync(actor,id,subject,token);
        var row=await db.Set<ContentRemoval>().AsNoTracking().SingleOrDefaultAsync(r=>r.Id==removalId&&r.CaseId==id,token);
        if(row is null||row.SealedSnapshot is null||row.EvidenceDisposedAt!=null||review.EvidencePurgedAt!=null)throw Refuse(404,"content-evidence-expired");
        try
        {
            var snapshot=JsonSerializer.Deserialize<Snapshot>(Open(row))??throw Refuse(503,"evidence-not-configured");
            return new(row.Id,id,row.Field,row.ModeId,row.MessageId,row.RemovedAt,snapshot.Text,snapshot.Reason);
        }
        catch(Exception error)when(error is System.Security.Cryptography.CryptographicException or KeyNotFoundException or JsonException or ArgumentException){throw Refuse(503,"evidence-not-configured");}
    }

}
