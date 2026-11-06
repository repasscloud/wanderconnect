namespace Swoovr.Contracts;

/// <summary>
/// One of the member's own photos (D203). State: `checking`, `in-review`, `approved`, `not-approved` (refused, for any
/// reason) or `removed`. Link is a short-lived signed path (`api/media/{token}`), only once approved.
/// </summary>
/// <param name="AlbumOnly">#231: an adult photo approved for private albums only; it can't go in a mode and can only be deleted.</param>
/// <param name="AlbumId">#231: the album it was uploaded straight into.</param>
/// <param name="Preview">#231: the owner's own not-approved photo while it can be appealed (show blurred by default).</param>
public sealed record MyPhotoView(Guid Id, string State, string? Link, DateTimeOffset CreatedAt, IReadOnlyList<string> Scopes, Guid? Replaces = null,
    bool AlbumOnly = false, Guid? AlbumId = null, string? Preview = null);

/// <summary>D293 shared library, full retained mode galleries and explicitly selected mode mains. Legacy Main is always null.</summary>
/// #231 adds private albums (MaxAlbums each holding MaxAlbumPhotos) and whether the member is ID Verified (adult album photos).
public sealed record MyPhotosView(IReadOnlyList<MyPhotoView> Photos, Guid? Main, IReadOnlyDictionary<string, IReadOnlyList<Guid>> Modes, int MaxPerMode, int MaxKept, IReadOnlyDictionary<string, Guid>? Mains = null, string Tier = "free",
    IReadOnlyList<PhotoAlbumView>? Albums = null, int MaxAlbums = 4, int MaxAlbumPhotos = 10, bool IdVerified = false);

/// <summary>#231: a private album. Photos are library photo ids in order; Modes are the profiles it may be shared in.</summary>
public sealed record PhotoAlbumView(Guid Id, string Name, IReadOnlyList<Guid> Photos, IReadOnlyList<string> Modes, DateTimeOffset CreatedAt);

/// <summary>#231: create an album (name 1–40 characters) allowed in the given modes.</summary>
public sealed record PhotoAlbumCreate(string? Name, IReadOnlyList<string>? Modes);

/// <summary>
/// #231: replace an album's name, modes and photo order. Photos may only add approved library photos; an album-only photo
/// can't be dropped here (delete it instead).
/// </summary>
public sealed record PhotoAlbumUpdate(string? Name, IReadOnlyList<string>? Modes, IReadOnlyList<Guid>? Photos);

/// <summary>#231: "Receive explicit media" per profile (mode). Only ID Verified members can turn it on.</summary>
public sealed record ExplicitMediaSettings(bool IdVerified, IReadOnlyDictionary<string, bool> Modes);
public sealed record ExplicitMediaUpdate(string? Mode, bool Receive);

/// <summary>A full replacement for one mode gallery, including its optional main selection. Null clears the main.</summary>
public sealed record PhotoPlacementUpdate(string Scope, IReadOnlyList<Guid> MediaIds, Guid? MainMediaId = null);

/// <summary>Another member's approved photo as the viewer may see it: a short-lived signed link, nothing else.</summary>
public sealed record PhotoView(Guid Id, string Link);

/// <summary>An uncertain photo waiting for review (admins only). No image: viewing needs a recorded purpose.</summary>
/// <param name="Purpose">Null for a profile photo; "attachment" for a photo sent in chat (#41).</param>
/// <param name="Destination">#231: "album" when uploaded straight into a private album, else "library".</param>
/// <param name="OwnerIdVerified">#231: whether APPROVED_ALBUM_EXPLICIT is possible (album upload from an ID Verified owner).</param>
public sealed record MediaReviewItem(Guid Id, Guid AccountId, DateTimeOffset CreatedAt, string Reason, IReadOnlyList<string> Signals, string Screener, bool KnownContentChecked,
    string? Purpose = null, string? Destination = null, bool OwnerIdVerified = false);

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
