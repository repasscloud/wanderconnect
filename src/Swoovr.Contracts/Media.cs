namespace Swoovr.Contracts;

/// <summary>
/// One of the member's own photos (D203). State: `checking`, `in-review`, `approved`, `not-approved` (refused, for any
/// reason) or `removed`. Link is a short-lived signed path (`api/media/{token}`), only once approved.
/// </summary>
public sealed record MyPhotoView(Guid Id, string State, string? Link, DateTimeOffset CreatedAt, IReadOnlyList<string> Scopes, Guid? Replaces = null);

/// <summary>D293 shared library, full retained mode galleries and explicitly selected mode mains. Legacy Main is always null.</summary>
public sealed record MyPhotosView(IReadOnlyList<MyPhotoView> Photos, Guid? Main, IReadOnlyDictionary<string, IReadOnlyList<Guid>> Modes, int MaxPerMode, int MaxKept, IReadOnlyDictionary<string, Guid>? Mains = null, string Tier = "free");

/// <summary>A full replacement for one mode gallery, including its optional main selection. Null clears the main.</summary>
public sealed record PhotoPlacementUpdate(string Scope, IReadOnlyList<Guid> MediaIds, Guid? MainMediaId = null);

/// <summary>Another member's approved photo as the viewer may see it: a short-lived signed link, nothing else.</summary>
public sealed record PhotoView(Guid Id, string Link);

/// <summary>An uncertain photo waiting for review (admins only). No image: viewing needs a recorded purpose.</summary>
/// <param name="Purpose">Null for a profile photo; "attachment" for a photo sent in chat (#41).</param>
public sealed record MediaReviewItem(Guid Id, Guid AccountId, DateTimeOffset CreatedAt, string Reason, IReadOnlyList<string> Signals, string Screener, bool KnownContentChecked,
    string? Purpose = null);

/// <summary>The review queue and how many uploads are still waiting for a usable screening answer.</summary>
public sealed record MediaReviewReasonView(string Code,string Decision,string Label,bool RequiresNotes,bool Selectable=true);
public sealed record MediaReviewQueue(IReadOnlyList<MediaReviewItem> Items, int Pending, int PendingOverAnHour,
    IReadOnlyList<MediaReviewReasonView>? Reasons=null,IReadOnlyDictionary<string,int>? DecisionCounts=null,DateTimeOffset? CountsSince=null, int Page=1, int Total=0, int PageSize=25, DateTimeOffset? AcknowledgedUntil=null, string? NoticeVersion=null, string? SafetyNotice=null);

public sealed record MediaViewRequest(string? Purpose);

/// <summary>#41: an admin opens a chat photo named in a case's evidence, with a recorded purpose.</summary>
public sealed record MediaCaseViewRequest(Guid? CaseId, string? Purpose);

/// <summary>A review link (blurred until the reviewer chooses to look), valid for a few minutes.</summary>
public sealed record MediaReviewLink(Guid Id, string Link, DateTimeOffset Expires);

/// <summary>`approve`, `reject` (with a reason the member never sees) or `escalate` (suspected child abuse material: blocked, procedure §4).</summary>
public sealed record MediaDecision(string? Decision, string? Reason,string? ReasonCode=null);

public sealed record MediaDecisionResult(Guid Id,string State);

public sealed record MediaSafetyAck(string Version, bool Confirmed);
public sealed record PhotoWorkload(int ManualReview, int PendingScreening, int Specialist, int Appeals, int Rejected);
public sealed record PhotoDecisionView(Guid Id, Guid MediaId, string Decision, string Reason, DateTimeOffset At, bool CanAppeal,
    Guid? AppealId = null, string? AppealOutcome = null);
public sealed record PhotoAppealRequest(string? Text);
public sealed record PhotoAppealResolution(string? Outcome, string? Reason);
public sealed record PhotoAppealView(Guid Id, Guid DecisionId, Guid MediaId, Guid AccountId, DateTimeOffset SubmittedAt, string Text, string? Outcome, DateTimeOffset? DecidedAt);
public sealed record PhotoAppealPage(IReadOnlyList<PhotoAppealView> Items, int Page, int Total, int PageSize = 25);
public sealed record PhotoDecisionHistory(Guid Id, Guid MediaId, Guid AccountId, Guid? ActorId, string Decision, string Code, DateTimeOffset At);
public sealed record PhotoDecisionPage(IReadOnlyList<PhotoDecisionHistory> Items, int Page, int Total, int PageSize = 25);
