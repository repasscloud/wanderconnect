namespace Swoovr.Contracts;

/// <summary>
/// One of the member's own photos (D203). State: `checking`, `in-review`, `approved`, `not-approved` (refused, for any
/// reason) or `removed`. Link is a short-lived signed path (`api/media/{token}`), only once approved.
/// </summary>
public sealed record MyPhotoView(Guid Id, string State, string? Link, DateTimeOffset CreatedAt, IReadOnlyList<string> Scopes, Guid? Replaces = null);

/// <summary>The member's photos, the main photo (shown in every mode, D175) and each mode's own photos in order (#91).</summary>
public sealed record MyPhotosView(IReadOnlyList<MyPhotoView> Photos, Guid? Main, IReadOnlyDictionary<string, IReadOnlyList<Guid>> Modes, int MaxPerMode, int MaxKept);

/// <summary>Where photos show: scope `main` (at most one) or a mode id (up to MaxPerMode). A full replacement for that scope.</summary>
public sealed record PhotoPlacementUpdate(string Scope, IReadOnlyList<Guid> MediaIds);

/// <summary>Another member's approved photo as the viewer may see it: a short-lived signed link, nothing else.</summary>
public sealed record PhotoView(Guid Id, string Link);

/// <summary>An uncertain photo waiting for review (admins only). No image: viewing needs a recorded purpose.</summary>
/// <param name="Purpose">Null for a profile photo; "attachment" for a photo sent in chat (#41).</param>
public sealed record MediaReviewItem(Guid Id, Guid AccountId, DateTimeOffset CreatedAt, string Reason, IReadOnlyList<string> Signals, string Screener, bool KnownContentChecked,
    string? Purpose = null);

/// <summary>The review queue and how many uploads are still waiting for a usable screening answer.</summary>
public sealed record MediaReviewQueue(IReadOnlyList<MediaReviewItem> Items, int Pending, int PendingOverAnHour);

public sealed record MediaViewRequest(string? Purpose);

/// <summary>#41: an admin opens a chat photo named in a case's evidence, with a recorded purpose.</summary>
public sealed record MediaCaseViewRequest(Guid? CaseId, string? Purpose);

/// <summary>A review link (blurred until the reviewer chooses to look), valid for a few minutes.</summary>
public sealed record MediaReviewLink(Guid Id, string Link, DateTimeOffset Expires);

/// <summary>`approve`, `reject` (with a reason the member never sees) or `escalate` (suspected child abuse material: blocked, procedure §4).</summary>
public sealed record MediaDecision(string? Decision, string? Reason);
