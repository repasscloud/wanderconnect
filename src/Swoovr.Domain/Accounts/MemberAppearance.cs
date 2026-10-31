namespace Swoovr.Domain;

/// <summary>Private account preference only; never member traits, access, matching or entitlements.</summary>
public sealed class MemberAppearance
{
    public Guid AccountId { get; set; }
    public string Scheme { get; set; } = "standard";
    public string Mode { get; set; } = "system";
    public long Revision { get; set; }
    public Guid LastChangeId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
