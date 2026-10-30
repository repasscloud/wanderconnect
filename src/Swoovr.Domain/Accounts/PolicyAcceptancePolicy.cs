namespace Swoovr.Domain;

public enum PublicPolicyStatus { Draft, Approved }
/// <summary>Trusted server catalogue entry; never accept approval/status from a member request.</summary>
public sealed record PublicPolicyVersion(string Id, PublicPolicyStatus Status, DateTimeOffset? EffectiveAt, string ContentSha256);
public enum PolicyRequirementState { ExistingAccessUnchanged, Required, ConfigurationInvalid }
public sealed record PolicyRequirement(PolicyRequirementState State,string? Version);

/// <summary>Preparation only: no approved entries or activation are supplied in this slice.</summary>
public static class PolicyAcceptancePolicy
{
    public static PolicyRequirement Resolve(string? configuredApprovedVersion,IReadOnlyList<PublicPolicyVersion> catalogue,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(configuredApprovedVersion))return new(PolicyRequirementState.ExistingAccessUnchanged,null);
        var candidates=catalogue.Where(v=>v.Id==configuredApprovedVersion).ToList();
        if(candidates.Count!=1)return new(PolicyRequirementState.ConfigurationInvalid,null);
        var version=candidates[0];
        if(version.Status!=PublicPolicyStatus.Approved||version.EffectiveAt is null||version.EffectiveAt>now
            ||version.ContentSha256.Length!=64||!version.ContentSha256.All(char.IsAsciiHexDigit))
            return new(PolicyRequirementState.ConfigurationInvalid,null);
        return new(PolicyRequirementState.Required,version.Id);
    }
    public static bool CanRecord(PolicyRequirement requirement,string requestedVersion,bool explicitAcceptance) =>
        requirement.State==PolicyRequirementState.Required&&explicitAcceptance&&requirement.Version==requestedVersion;
}
