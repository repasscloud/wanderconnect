namespace Swoovr.Domain;

public enum FeedbackContext { ChatOnly, MetInPerson }
public enum PrivateAnswer { Yes, No, Uncertain }

/// <summary>One author's own experience, never an attendance assertion about the other
/// participant. No public score, moderation verdict or recommendation weight is produced.</summary>
public sealed record FeedbackAnswers(FeedbackContext Context, PrivateAnswer ProfileRepresentedThem,
    PrivateAnswer IntentionsAligned, PrivateAnswer BoundariesRespected, PrivateAnswer Reconnect,
    ConnectionIntention? ReconnectCapacity, PrivateAnswer? Chemistry);
public sealed record FeedbackRevision(long Version, FeedbackAnswers Answers, DateTimeOffset SubmittedAt);

/// <summary>
/// Trusted historical server facts, NOT client input. Bind ActorId/Session from the same
/// validated context; resolve the retained interaction/pair and eligibility on the server.
/// Unmatching/blocking does not erase the interaction; it grants no live chat/profile access.
/// </summary>
public sealed record FeedbackAccess(AccessSession? Session, Guid ActorId, Guid InteractionId,
    Guid FirstParticipant, Guid SecondParticipant, bool HadGenuineConnection, bool HadAgreedMeeting);

/// <summary>
/// Server-private aggregate. Persistence must enforce unique(interaction,author), use
/// revision compare-and-swap and reviewed retention. Never serialize internal state/facts
/// or expose counterpart history. Reporting/safety review is a separate controlled service.
/// </summary>
public sealed class PrivateFeedback
{
    private static readonly AccountAccessPolicy Accounts=new();
    private readonly Guid interactionId, authorId, firstId, secondId;
    private readonly IReadOnlyList<FeedbackRevision> history;
    private PrivateFeedback(Guid interaction,Guid author,Guid first,Guid second,IEnumerable<FeedbackRevision> entries)
    {
        interactionId=interaction; authorId=author; firstId=first; secondId=second;
        history=Array.AsReadOnly(entries.ToArray());
    }
    public long Revision => history[^1].Version;
    public static PrivateFeedback? TrySubmit(FeedbackAccess? facts,FeedbackAnswers? answers,DateTimeOffset now)
    {
        if(!Eligible(facts) || !Valid(answers)) return null;
        return new(facts!.InteractionId,facts.ActorId,facts.FirstParticipant,facts.SecondParticipant,
            [new(1,answers!,now.ToUniversalTime())]);
    }
    public PrivateFeedback? TryAmend(FeedbackAccess? facts,long expectedRevision,FeedbackAnswers? answers,DateTimeOffset now)
    {
        if(!Owns(facts) || expectedRevision!=Revision || Revision==long.MaxValue
            || !Valid(answers) || now<history[^1].SubmittedAt) return null;
        return new(interactionId,authorId,firstId,secondId,
            [..history,new(Revision+1,answers!,now.ToUniversalTime())]);
    }
    public IReadOnlyList<FeedbackRevision>? ReadOwnHistory(FeedbackAccess? facts) => Owns(facts) ? history : null;
    private bool Owns(FeedbackAccess? facts) => Eligible(facts) && facts!.ActorId==authorId
        && facts.InteractionId==interactionId
        && ((facts.FirstParticipant==firstId && facts.SecondParticipant==secondId)
            || (facts.FirstParticipant==secondId && facts.SecondParticipant==firstId));
    private static bool Eligible(FeedbackAccess? facts) => facts is not null
        && Accounts.Allows(facts.Session,AccountRole.Member) && facts.InteractionId!=Guid.Empty
        && facts.FirstParticipant!=Guid.Empty && facts.SecondParticipant!=Guid.Empty
        && facts.FirstParticipant!=facts.SecondParticipant
        && (facts.ActorId==facts.FirstParticipant || facts.ActorId==facts.SecondParticipant)
        && (facts.HadGenuineConnection || facts.HadAgreedMeeting);
    private static bool Valid(FeedbackAnswers? answers) => answers is not null
        && Enum.IsDefined(answers.Context) && Enum.IsDefined(answers.ProfileRepresentedThem)
        && Enum.IsDefined(answers.IntentionsAligned) && Enum.IsDefined(answers.BoundariesRespected)
        && Enum.IsDefined(answers.Reconnect)
        && (answers.Chemistry is null || Enum.IsDefined(answers.Chemistry.Value))
        && (answers.Reconnect==PrivateAnswer.Yes
            ? answers.ReconnectCapacity is {} capacity && Enum.IsDefined(capacity)
            : answers.ReconnectCapacity is null);
}
