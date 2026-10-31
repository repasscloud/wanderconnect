using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;

public sealed record TextHarmProcessResult(string Result,Guid? ReviewId=null,Guid? CaseId=null,bool Urgent=false);
public sealed record TextHarmSnapshot(string Text,TextHarmSource Source,Guid SourceId,Guid Revision,string? ModeId,
    string Detector,string Version,TextHarmHistory History,IReadOnlyList<TextHarmSignal> Signals);

/// <summary>Durable inspection only; never sanctions, messages, member notices or external email.</summary>
public sealed class TextHarmProcessor(SwoovrDbContext db,EvidenceKeys keys,EvidencePreservation evidence,ITextHarmDetector detector,TimeProvider clock,ModerationInspectionRecorder inspectionTotals)
{
    // SQL aliases match these CLR names explicitly; values are parameterized, never concatenated identifiers/input.
    public sealed class HistoryRow { public Guid Id{get;set;} public string Body{get;set;}=""; public Guid Recipient{get;set;} }
    public async Task<TextHarmProcessResult> ProcessAsync(Guid intentId,CancellationToken token)
    {
        var owner=await db.Set<TextHarmIntent>().AsNoTracking().Where(i=>i.Id==intentId).Select(i=>(Guid?)i.AccountId).SingleOrDefaultAsync(token);
        if(owner is null)return new("missing");
        await using var state=await AccountLifecycleLease.AcquireAsync(db,owner.Value,token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        await using var tx=await db.Database.BeginTransactionAsync(token);
        var intent=await db.Set<TextHarmIntent>().SingleOrDefaultAsync(i=>i.Id==intentId,token);
        if(intent is null)return new("missing");
        await db.Entry(intent).ReloadAsync(token);
        if(intent.AccountId!=owner)throw new InvalidOperationException("Text inspection owner changed.");
        var prior=await db.Set<TextHarmReview>().AsNoTracking().SingleOrDefaultAsync(r=>r.IntentId==intentId,token);
        if(intent.ProcessedAt is not null)
            return prior is null?new(intent.Result??"processed"):new("already-recorded",prior.Id,prior.CaseId,prior.Categories.Contains("threats"));
        if(prior is not null)throw new InvalidOperationException("Text inspection receipt is inconsistent.");
        var now=clock.GetUtcNow();
        // Missing/decryption keys or malformed payload leave pending ciphertext intact for retry/operator diagnosis.
        if(intent.SealedText is null||keys.CurrentVersion is not{}key)throw new InvalidOperationException("Text inspection evidence is unavailable.");
        string text;
        try{text=JsonSerializer.Deserialize<string>(EvidenceSeal.Open(keys.Encryption,intent.SealedText))
            ??throw new InvalidOperationException("Text inspection evidence is unavailable.");}
        catch(Exception error)when(error is System.Security.Cryptography.CryptographicException or KeyNotFoundException or JsonException or ArgumentException)
        {throw new InvalidOperationException("Text inspection evidence is unavailable.");}
        if(text.Length==0||text.Length>4000)throw new InvalidOperationException("Text inspection payload is invalid.");
        var active=await db.Accounts.AsNoTracking().AnyAsync(a=>a.Id==intent.AccountId&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,token);
        var source=active?await CurrentAsync(intent,text,token):null;
        if(source is null)
        {
            await CompleteAsync(intent,"stale",now,token);
            await tx.CommitAsync(token);return new("stale");
        }
        // Source is used only for existence/revision validation. Never substitute its possibly replaced body.
        var history=source.Message is{}message?await HistoryAsync(message,text,token):new TextHarmHistory(true,0,0);
        var assessment=await detector.DetectAsync(new(intent.AccountId,intent.Source,text,history),token);
        if(assessment.Signals.Count==0)
        {
            await CompleteAsync(intent,"no-signal",now,token,history,false);
            await tx.CommitAsync(token);return new("no-signal");
        }
        var urgent=assessment.Signals.Any(s=>s.Category=="threats");
        var item=await db.ModerationCases.SingleOrDefaultAsync(c=>c.SubjectAccountId==intent.AccountId&&(c.Status==CaseStatus.Open||c.Status==CaseStatus.UnderReview),token);
        if(item is null)
        {
            item=new(){Id=Guid.NewGuid(),SubjectAccountId=intent.AccountId,Status=CaseStatus.Open,CreatedAt=now,SystemFlag="text-harm-candidate"};
            db.ModerationCases.Add(item);
        }
        item.Priority=ReportPolicy.Max(item.Priority,urgent?ReportPriority.Urgent:ReportPriority.Normal);
        var employee=await db.MemberProfiles.AnyAsync(p=>p.AccountId==intent.AccountId&&p.Employee,token);
        item.Queue=ReportPolicy.QueueFor(employee||item.Queue==CaseQueue.Admin,urgent);
        item.UpdatedAt=now;
        var people=new List<Guid>{intent.AccountId};var conversations=new List<Guid>();
        if(source.Message is{}sourceMessage)
        {
            var conversation=await db.Conversations.AsNoTracking().SingleAsync(c=>c.Id==sourceMessage.ConversationId,token);
            people.Add(conversation.PairLow==intent.AccountId?conversation.PairHigh:conversation.PairLow);conversations.Add(conversation.Id);
        }
        var review=new TextHarmReview{Id=Guid.NewGuid(),IntentId=intent.Id,CaseId=item.Id,SubjectAccountId=intent.AccountId,
            RelatedAccountIds=people.Distinct().ToArray(),Source=intent.Source,SourceId=intent.SourceId,SourceRevision=intent.Revision.ToString("D"),ModeId=intent.ModeId,
            DetectorVersion=assessment.Version,Rules=assessment.Signals.Select(s=>s.Rule).Distinct().ToArray(),
            Categories=assessment.Signals.Select(s=>s.Category).Distinct().ToArray(),CreatedAt=now,
            SealedEvidence=EvidenceSeal.Seal(key,keys.Encryption[key],JsonSerializer.Serialize(new TextHarmSnapshot(text,intent.Source,intent.SourceId,
                intent.Revision,intent.ModeId,assessment.Detector,assessment.Version,history,assessment.Signals)))};
        db.Set<TextHarmReview>().Add(review);
        // Save candidate before capture, in the same outer transaction. Root preservation inventory must include this source.
        await db.SaveChangesAsync(token);
        if(urgent)
        {
            // No invented member report/reporter. Unknown role yields existing provisional hold, legal review within30days.
            await LegalHoldEndpoints.PlaceAsync(db,evidence,AccountRole.Unknown,null,"Own-initiative text threat candidate; human review required.",
                people.Distinct().ToArray(),conversations,null,null,null,"Review the candidate and release only when no independent obligation remains.",
                item.Id,"text-harm-threat",now,token);
        }
        Audit.Record(db,now,null,"system","text-harm.candidate","text-harm-review",review.Id.ToString(),urgent?"urgent":"review");
        await CompleteAsync(intent,urgent?"candidate-urgent":"candidate",now,token,history,true);
        await tx.CommitAsync(token);
        // Caller invokes/retries EscalateAsync only AFTER this method returns and all leases have disposed.
        return new("created",review.Id,item.Id,urgent);
    }
    private sealed record CurrentSource(Message? Message);
    private async Task<CurrentSource?> CurrentAsync(TextHarmIntent intent,string text,CancellationToken token)
    {
        if(intent.Revision==Guid.Empty)return null;
        if(intent.Source==TextHarmSource.ProfileAbout)
            return intent.SourceId==intent.AccountId&&intent.ModeId is null&&await db.MemberProfiles.AnyAsync(p=>p.AccountId==intent.AccountId&&p.TextRevision==intent.Revision&&p.About==text,token)?new(null):null;
        if(intent.Source==TextHarmSource.ModeAbout)
            return intent.SourceId==intent.AccountId&&intent.ModeId is not null&&await db.MemberModes.AnyAsync(m=>m.AccountId==intent.AccountId&&m.ModeId==intent.ModeId&&m.TextRevision==intent.Revision&&m.About==text,token)?new(null):null;
        if(intent.Source!=TextHarmSource.Message||intent.ModeId is not null||intent.Revision!=intent.SourceId)return null;
        var message=await db.Messages.AsNoTracking().SingleOrDefaultAsync(m=>m.Id==intent.SourceId&&m.SenderAccountId==intent.AccountId&&m.Kind==MessageKind.Text
            &&m.DeletedAt==null&&m.ModerationRemovedAt==null&&m.Body==text,token);
        return message is null?null:new(message);
    }
    private async Task<TextHarmHistory> HistoryAsync(Message message,string text,CancellationToken token)
    {
        var lower=message.SentAt-TimeSpan.FromHours(1);
        var rows=await db.Database.SqlQuery<HistoryRow>($"""
            SELECT m.id AS "Id", m.body AS "Body", CASE WHEN c.pair_low={message.SenderAccountId} THEN c.pair_high ELSE c.pair_low END AS "Recipient"
            FROM messages m JOIN conversations c ON c.id=m.conversation_id
            WHERE m.sender_account_id={message.SenderAccountId} AND m.kind='Text'
              AND m.deleted_at IS NULL AND m.moderation_removed_at IS NULL
              AND m.sent_at>={lower} AND (m.sent_at<{message.SentAt} OR (m.sent_at={message.SentAt} AND m.id<{message.Id}))
              AND (c.pair_low={message.SenderAccountId} OR c.pair_high={message.SenderAccountId})
            ORDER BY m.sent_at DESC,m.id DESC LIMIT 501
            """).ToListAsync(token);
        if(rows.Count>500)return new(false,0,0);
        var normalized=BoundedTextHarmDetector.Normalise(text);
        var conversation=await db.Conversations.AsNoTracking().SingleAsync(c=>c.Id==message.ConversationId,token);
        var currentPeer=conversation.PairLow==message.SenderAccountId?conversation.PairHigh:conversation.PairLow;
        return new(true,rows.Count(m=>BoundedTextHarmDetector.Normalise(m.Body)==normalized),rows.Select(m=>m.Recipient).Append(currentPeer).Distinct().Count());
    }
    private async Task CompleteAsync(TextHarmIntent intent,string result,DateTimeOffset now,CancellationToken token,TextHarmHistory? inspectedHistory=null,bool createdCandidate=false)
    {
        // Requires root's new-source hold inventory BEFORE activation; no loss of held intent evidence.
        await evidence.PreserveBeforeDeletionAsync(intent.AccountId,now,token);
        intent.ProcessedAt=now;intent.Result=result;intent.SealedText=null;
        if(inspectedHistory is not null)await inspectionTotals.CompletedAsync(ModerationInspectionRecorder.TextDefinition,intent.Id,!inspectedHistory.Complete,createdCandidate,token);
        await db.SaveChangesAsync(token);
    }
}
