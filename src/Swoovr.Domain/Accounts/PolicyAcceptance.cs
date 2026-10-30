namespace Swoovr.Domain;
/// <summary>Explicit acceptance of one immutable approved version; never inferred from sign-in or draft reading.</summary>
public sealed class PolicyAcceptance
{
    public Guid AccountId{get;set;}
    public string Version{get;set;}="";
    public string ContentSha256{get;set;}="";
    public DateTimeOffset AcceptedAt{get;set;}
}
