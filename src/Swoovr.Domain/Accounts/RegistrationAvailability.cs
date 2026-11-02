namespace Swoovr.Domain;

/// <summary>D295: creation/adoption policy, distinct from authentication of an existing account.</summary>
public sealed record RegistrationAvailability(bool BetaEnabled,bool PublicEnabled,long Revision)
{
    public string State=>PublicEnabled?(BetaEnabled?"public-with-beta-tracking":"public"):(BetaEnabled?"invite-only":"closed");
    public static bool Allows(RegistrationAvailability? policy,bool activeUnboundInvitation)=>policy is{Revision:>=0}
        &&(policy.PublicEnabled||policy.BetaEnabled&&activeUnboundInvitation);
    public static bool ValidChange(long expectedRevision,bool confirm,string? reason)=>expectedRevision>=0&&expectedRevision<long.MaxValue
        &&confirm&&!string.IsNullOrWhiteSpace(reason)&&reason.Trim().Length<=500;
}
