namespace Swoovr.Domain;

public enum ConnectionState { None, Pending, Accepted, Declined, Closed }
public sealed record ConnectionMessage(Guid SenderId, string Text, DateTimeOffset SentAt);

/// <summary>
/// Internal fresh server facts, never request DTOs. ActorId and Session must come from
/// the same validated context; member safety, block and conversation privacy are current
/// authoritative reads. Ordinary discovery preferences/location are intentionally separate.
/// </summary>
public sealed record ConnectionAccess(AccessSession? Session, Guid ActorId,
    DiscoveryMember First, DiscoveryMember Second, bool BlockedEitherDirection,
    bool ConversationPrivacyRevoked);

/// <summary>
/// Immutable server aggregate, NOT an authorized response projection. Persist/load one
/// retained unordered pair across contexts, with transactional revision compare-and-swap.
/// Every read/send/delivery must evaluate fresh access, including realtime/push recipients.
/// Historical report eligibility grants neither chat history nor current profile/media.
/// </summary>
public sealed class ConnectionThread
{
    private static readonly AccountAccessPolicy Accounts = new();
    private static readonly DiscoveryAccessPolicy Discovery = new();
    private ConnectionThread(Guid firstId, Guid secondId, ConnectionState state, long revision,
        IReadOnlyList<ConnectionMessage> messages)
    {
        FirstId=firstId; SecondId=secondId; State=state; Revision=revision; Messages=messages;
    }
    public Guid FirstId { get; }
    public Guid SecondId { get; }
    public ConnectionState State { get; }
    public long Revision { get; }
    // For trusted persistence/orchestration only. Never serialize the aggregate to a client.
    public IReadOnlyList<ConnectionMessage> Messages { get; }
    public static ConnectionThread? Empty(Guid firstId, Guid secondId) =>
        firstId==Guid.Empty || secondId==Guid.Empty || firstId==secondId ? null :
        new(firstId,secondId,ConnectionState.None,0,Array.AsReadOnly(Array.Empty<ConnectionMessage>()));

    public ConnectionThread? TryRequest(DiscoveryAccessRequest? facts, long expectedRevision,
        string? text, DateTimeOffset now)
    {
        if(State!=ConnectionState.None || !CanChange(expectedRevision) || !Contact(facts,now)
            || !ValidText(text,1000)) return null;
        return Next(ConnectionState.Pending,[new(facts!.AuthenticatedAccountId,text!,now)]);
    }
    public ConnectionThread? TryAccept(DiscoveryAccessRequest? facts, long expectedRevision, DateTimeOffset now)
    {
        // Conservative Q05 default: acceptance also requires current contact eligibility.
        if(State!=ConnectionState.Pending || !CanChange(expectedRevision) || !Contact(facts,now)
            || facts!.AuthenticatedAccountId==Messages[0].SenderId || now<Messages[^1].SentAt) return null;
        return Next(ConnectionState.Accepted,Messages);
    }
    public ConnectionThread? TryDecline(ConnectionAccess? facts, long expectedRevision)
    {
        if(State!=ConnectionState.Pending || !CanChange(expectedRevision) || !Participant(facts)
            || facts!.ActorId==Messages[0].SenderId) return null;
        return Next(ConnectionState.Declined,Messages);
    }
    public ConnectionThread? TryClose(ConnectionAccess? facts, long expectedRevision)
    {
        if(State is not (ConnectionState.Pending or ConnectionState.Accepted)
            || !CanChange(expectedRevision) || !Participant(facts)) return null;
        return Next(ConnectionState.Closed,Messages);
    }
    public ConnectionThread? TrySend(ConnectionAccess? facts, long expectedRevision,
        string? text, DateTimeOffset now)
    {
        if(State!=ConnectionState.Accepted || !CanChange(expectedRevision) || !CanRead(facts)
            || !ValidText(text,4000) || now<Messages[^1].SentAt) return null;
        return Next(State,[..Messages,new(facts!.ActorId,text!,now)]);
    }
    public bool CanRead(ConnectionAccess? facts) => State is ConnectionState.Pending or ConnectionState.Accepted
        && Participant(facts) && Safe(facts!.First) && Safe(facts.Second)
        && !facts.BlockedEitherDirection && !facts.ConversationPrivacyRevoked;
    public bool CanReport(ConnectionAccess? facts) => State!=ConnectionState.None && Participant(facts);

    private bool CanChange(long expected) => expected==Revision && Revision<long.MaxValue;
    private bool Pair(Guid a,Guid b) => (a==FirstId && b==SecondId) || (a==SecondId && b==FirstId);
    private bool Participant(ConnectionAccess? facts) => facts is {First:{},Second:{}}
        && Accounts.Allows(facts.Session,AccountRole.Member) && Pair(facts.First.Id,facts.Second.Id)
        && (facts.ActorId==FirstId || facts.ActorId==SecondId);
    private bool Contact(DiscoveryAccessRequest? facts,DateTimeOffset now) => facts is {Viewer:{},Subject:{},Resource:DiscoveryResource.Contact}
        && Pair(facts.Viewer.Id,facts.Subject.Id) && Discovery.Allows(facts,now);
    private static bool Safe(DiscoveryMember member) => member.IsActive && member.IsAdult && member.IsVerified;
    private static bool ValidText(string? text,int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length<=maximum;
    private ConnectionThread Next(ConnectionState state,IEnumerable<ConnectionMessage> messages) =>
        new(FirstId,SecondId,state,Revision+1,Array.AsReadOnly(messages.ToArray()));
}
