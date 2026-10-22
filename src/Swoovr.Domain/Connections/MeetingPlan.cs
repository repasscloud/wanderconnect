namespace Swoovr.Domain;

public enum MeetingPlanState { Proposed, Accepted, Completed, Cancelled }

/// <summary>Private two-party terms. Participants come from the accepted connection.
/// PublicPlaceOrArea is a public venue/coarse area, never an itinerary or device coordinates.
/// StartsAt is an explicit instant; clients must display its zone/offset before consent.</summary>
public sealed record MeetingPlanTerms(DiscoveryContext Context, ConnectionIntention Intention,
    string Activity, string PublicPlaceOrArea, DateTimeOffset StartsAt, string TimeZoneId,
    string CostResponsibility);

/// <summary>
/// Immutable server aggregate, not a transport DTO or attendance/booking/payment verdict.
/// Every operation consumes the current retained connection plus fresh server access facts.
/// Persistence must atomically compare revisions with safety reads; never trust client facts.
/// </summary>
public sealed class MeetingPlan
{
    private MeetingPlan(Guid first, Guid second, MeetingPlanState state, long revision,
        MeetingPlanTerms proposal, long version, MeetingPlanTerms? agreed, long? agreedVersion,
        IEnumerable<Guid> confirmations, IEnumerable<Guid> completionConfirmations)
    {
        FirstId=first; SecondId=second; State=state; Revision=revision;
        Proposal=proposal; ProposalVersion=version; Agreed=agreed; AgreedVersion=agreedVersion;
        Confirmations=Array.AsReadOnly(confirmations.ToArray());
        CompletionConfirmations=Array.AsReadOnly(completionConfirmations.ToArray());
    }
    public Guid FirstId { get; }
    public Guid SecondId { get; }
    public MeetingPlanState State { get; }
    public long Revision { get; }
    public MeetingPlanTerms Proposal { get; }
    public long ProposalVersion { get; }
    public MeetingPlanTerms? Agreed { get; }
    public long? AgreedVersion { get; }
    public IReadOnlyList<Guid> Confirmations { get; }
    // Lifecycle acknowledgements only. Neither participant's attendance is asserted.
    public IReadOnlyList<Guid> CompletionConfirmations { get; }

    public static MeetingPlan? TryCreate(ConnectionThread? connection, ConnectionAccess? access,
        MeetingPlanTerms? terms, DateTimeOffset now)
    {
        if(connection is not {State:ConnectionState.Accepted} || !connection.CanRead(access)
            || !ValidTerms(terms,now)) return null;
        return new(connection.FirstId,connection.SecondId,MeetingPlanState.Proposed,0,
            Normalize(terms!),1,null,null,[],[]);
    }
    public MeetingPlan? TryPropose(ConnectionThread? connection, ConnectionAccess? access,
        long expectedRevision, MeetingPlanTerms? terms, DateTimeOffset now)
    {
        if(!CanChange(expectedRevision) || !CanCollaborate(connection,access) || !ValidTerms(terms,now)
            || ProposalVersion==long.MaxValue || Normalize(terms!)==Proposal) return null;
        return Next(State,Normalize(terms!),ProposalVersion+1,Agreed,AgreedVersion,[],CompletionConfirmations);
    }
    public MeetingPlan? TryConfirm(ConnectionThread? connection, ConnectionAccess? access,
        long expectedRevision, long proposalVersion, DateTimeOffset now)
    {
        if(!CanChange(expectedRevision) || !CanCollaborate(connection,access)
            || proposalVersion!=ProposalVersion || Confirmations.Contains(access!.ActorId)
            || now>=Proposal.StartsAt) return null;
        var approvals=Confirmations.Append(access.ActorId).ToArray();
        return approvals.Length==2
            ? Next(MeetingPlanState.Accepted,Proposal,ProposalVersion,Proposal,ProposalVersion,approvals,[])
            : Next(State,Proposal,ProposalVersion,Agreed,AgreedVersion,approvals,CompletionConfirmations);
    }
    public MeetingPlan? TryComplete(ConnectionThread? connection, ConnectionAccess? access,
        long expectedRevision, long agreedVersion, DateTimeOffset now)
    {
        if(!CanChange(expectedRevision) || !CanCollaborate(connection,access)
            || Agreed is null || agreedVersion!=AgreedVersion || now<Agreed.StartsAt
            || CompletionConfirmations.Contains(access!.ActorId)) return null;
        var completed=CompletionConfirmations.Append(access.ActorId).ToArray();
        return Next(completed.Length==2 ? MeetingPlanState.Completed : State,
            Proposal,ProposalVersion,Agreed,AgreedVersion,Confirmations,completed);
    }
    public MeetingPlan? TryCancel(ConnectionThread? connection, ConnectionAccess? access, long expectedRevision)
    {
        // Cancelling remains possible after block, privacy revocation or conversation closure.
        if(!CanChange(expectedRevision) || !SamePair(connection) || !connection!.CanReport(access)) return null;
        return Next(MeetingPlanState.Cancelled,Proposal,ProposalVersion,Agreed,AgreedVersion,Confirmations,CompletionConfirmations);
    }
    private bool CanChange(long expected) => State is MeetingPlanState.Proposed or MeetingPlanState.Accepted
        && expected==Revision && Revision<long.MaxValue;
    private bool SamePair(ConnectionThread? connection) => connection is not null
        && ((connection.FirstId==FirstId && connection.SecondId==SecondId)
            || (connection.FirstId==SecondId && connection.SecondId==FirstId));
    private bool CanCollaborate(ConnectionThread? connection, ConnectionAccess? access) => SamePair(connection)
        && connection!.State==ConnectionState.Accepted && connection.CanRead(access);
    private static MeetingPlanTerms Normalize(MeetingPlanTerms terms) => terms with {StartsAt=terms.StartsAt.ToUniversalTime()};
    private static bool ValidTerms(MeetingPlanTerms? terms,DateTimeOffset now)
    {
        if(terms is null || !Enum.IsDefined(terms.Context) || !Enum.IsDefined(terms.Intention)
            || !Text(terms.Activity,200) || !Text(terms.PublicPlaceOrArea,200)
            || !Text(terms.CostResponsibility,300) || !Text(terms.TimeZoneId,100) || terms.StartsAt<=now) return false;
        try { _=TimeZoneInfo.FindSystemTimeZoneById(terms.TimeZoneId); return true; }
        catch(TimeZoneNotFoundException) { return false; }
        catch(InvalidTimeZoneException) { return false; }
    }
    private static bool Text(string? text,int maximum) => !string.IsNullOrWhiteSpace(text) && text.Length<=maximum;
    private MeetingPlan Next(MeetingPlanState state, MeetingPlanTerms proposal, long version,
        MeetingPlanTerms? agreed, long? agreedVersion, IEnumerable<Guid> confirmations, IEnumerable<Guid> completions) =>
        new(FirstId,SecondId,state,Revision+1,proposal,version,agreed,agreedVersion,confirmations,completions);
}
