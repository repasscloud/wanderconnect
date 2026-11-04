using System.Security.Cryptography;
using System.Text;
using Swoovr.Contracts;
using Swoovr.Domain;
namespace Swoovr.Api.Policies;
public sealed record ApprovedPolicyDocument(PublicPolicyVersion Metadata,string Terms,string CommunityGuidelines)
{
    public static string Hash(string terms,string guidelines)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(terms+"\n\n"+guidelines)));
}
/// <summary>API-owned catalogue of approved, effective policy versions (D288/D290/D310). Configuration can only select a
/// version listed here; it can never approve one.</summary>
public sealed class ApprovedPolicyCatalogue
{
    private readonly IReadOnlyList<ApprovedPolicyDocument> documents;
    public ApprovedPolicyCatalogue(IReadOnlyList<ApprovedPolicyDocument>? entries=null)
    {
        documents=(entries??[]).ToArray();
        if(documents.GroupBy(d=>d.Metadata.Id).Any(g=>g.Count()!=1)||documents.Any(d=>!Swoovr.Domain.PolicyNoticePolicy.ValidVersion(d.Metadata.Id)
            ||d.Metadata.Status!=PublicPolicyStatus.Approved||d.Metadata.EffectiveAt is null||string.IsNullOrWhiteSpace(d.Terms)||string.IsNullOrWhiteSpace(d.CommunityGuidelines)
            ||d.Metadata.ContentSha256!=ApprovedPolicyDocument.Hash(d.Terms,d.CommunityGuidelines)))throw new InvalidOperationException("Approved policy catalogue is invalid.");
    }
    /// <summary>The approved terms of use and community guidelines in force since 2 November 2026.</summary>
    public static ApprovedPolicyCatalogue Current()
    {
        var terms=PublicPolicies.Text(PublicPolicies.Terms);var guidelines=PublicPolicies.Text(PublicPolicies.Community);
        return new([new(new(PublicPolicies.Version,PublicPolicyStatus.Approved,PublicPolicies.EffectiveAt,ApprovedPolicyDocument.Hash(terms,guidelines)),terms,guidelines)]);
    }
    public PolicyRequirement Resolve(string? selected,DateTimeOffset now)=>PolicyAcceptancePolicy.Resolve(selected,documents.Select(d=>d.Metadata).ToArray(),now);
    public ApprovedPolicyDocument? Find(string id,DateTimeOffset now)=>documents.SingleOrDefault(d=>d.Metadata.Id==id&&d.Metadata.EffectiveAt<=now);
}
