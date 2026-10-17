namespace DatingApp.Domain;

public enum DiscoveryContext { Connect, Friends, Travel }
public enum DiscoveryResource { List, Profile, Media, SavedProfile, Contact }

/// <summary>
/// Internal server facts for the selected context. ApplicableIntentions must include
/// hidden selections; enabled contexts and substance are authoritative assessments.
/// Never construct this snapshot from client assertions or expose it as a profile DTO.
/// </summary>
public sealed record DiscoveryMember(Guid Id, bool IsActive, bool IsAdult, bool IsVerified,
    bool IsSubstantive, IReadOnlySet<DiscoveryContext> EnabledContexts,
    IReadOnlySet<ConnectionIntention> ApplicableIntentions);

public sealed record DiscoveryRules(bool AllowViewing, bool AllowContact,
    bool AcceptIncognito, bool HideSparse, IntentionRule Intentions);

/// <summary>
/// A trusted location service must supply bounded timestamps, consent and risk checks.
/// Planned visits, browser IP or a selected destination cannot construct this evidence.
/// </summary>
public sealed record DestinationPresence(Guid AccountId, Guid AreaId,
    DateTimeOffset ObservedAt, DateTimeOffset ExpiresAt, bool HasConsent, bool RiskApproved);

/// <summary>
/// Server-only evaluation snapshot. Session and AuthenticatedAccountId must come from
/// the same validated authentication context. Blocks, area matches, request limits and
/// all member/rule facts must be read afresh. No API binds this record from a request.
/// RequiresDestinationPresence is derived from travel context by the server, not a toggle
/// the caller's client may disable. Existing accepted chat membership uses a separate policy.
/// </summary>
public sealed record DiscoveryAccessRequest(AccessSession? Session, Guid AuthenticatedAccountId,
    DiscoveryMember Viewer, DiscoveryMember Subject, DiscoveryRules ViewerRules,
    DiscoveryRules SubjectRules, DiscoveryContext Context, DiscoveryResource Resource,
    bool IsIncognito, bool BlockedEitherDirection, bool AreaPreferencesSatisfied,
    bool RequestLimitAllows, bool IncognitoDisclosureConfirmed, bool RequiresDestinationPresence,
    Guid DiscoveryAreaId, DestinationPresence? Presence);

/// <summary>
/// Reuse before list/profile/media/saved projections and contact. Boolean denial reveals
/// no private filter details. This does not authenticate or fetch any of its input facts.
/// </summary>
public sealed class DiscoveryAccessPolicy
{
    private readonly AccountAccessPolicy accountAccess = new();

    public bool Allows(DiscoveryAccessRequest? request, DateTimeOffset now)
    {
        if (request is not { Viewer: { } viewer, Subject: { } subject,
                ViewerRules: { } viewerRules, SubjectRules: { } subjectRules }
            || !accountAccess.Allows(request.Session, AccountRole.Member)
            || request.AuthenticatedAccountId == Guid.Empty
            || request.AuthenticatedAccountId != viewer.Id
            || viewer.Id == subject.Id || !SafeMember(viewer) || !SafeMember(subject)
            || !Enum.IsDefined(request.Context) || !Enum.IsDefined(request.Resource)
            || !viewer.EnabledContexts.Contains(request.Context)
            || !subject.EnabledContexts.Contains(request.Context)
            || request.BlockedEitherDirection || !request.AreaPreferencesSatisfied
            || request.DiscoveryAreaId == Guid.Empty
            || !viewerRules.AllowViewing || !subjectRules.AllowViewing
            || (request.IsIncognito && !subjectRules.AcceptIncognito)
            || (viewerRules.HideSparse && !subject.IsSubstantive)
            || viewerRules.Intentions is null || subjectRules.Intentions is null
            || !viewerRules.Intentions.Allows(subject.ApplicableIntentions)
            || !subjectRules.Intentions.Allows(viewer.ApplicableIntentions))
            return false;

        if (request.Resource != DiscoveryResource.Contact) return true;
        if (!subjectRules.AllowContact || !request.RequestLimitAllows
            || (request.IsIncognito && !request.IncognitoDisclosureConfirmed))
            return false;

        // Travel context itself requires presence; an inconsistent false flag cannot bypass it.
        if (request.Context != DiscoveryContext.Travel && !request.RequiresDestinationPresence) return true;
        return request.Presence is { HasConsent: true, RiskApproved: true } presence
            && presence.AccountId == viewer.Id && presence.AreaId == request.DiscoveryAreaId
            && presence.ObservedAt <= now && presence.ExpiresAt > now
            && presence.ExpiresAt > presence.ObservedAt;
    }

    private static bool SafeMember(DiscoveryMember member) => member.Id != Guid.Empty
        && member.IsActive && member.IsAdult && member.IsVerified
        && member.EnabledContexts is not null
        && member.EnabledContexts.All(context => Enum.IsDefined(context));
}
