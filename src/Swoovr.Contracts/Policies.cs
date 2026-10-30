namespace Swoovr.Contracts;
public sealed record ApprovedPolicyDocumentView(string Version,string ContentSha256,DateTimeOffset EffectiveAt,string Terms,string CommunityGuidelines);
public sealed record MemberPolicyView(string? RequiredVersion,string? ContentSha256,DateTimeOffset? EffectiveAt,bool Accepted,DateTimeOffset? AcceptedAt,string? ReadPath);
public sealed record PolicyAcceptanceRequest(string? Version,bool Confirm);
