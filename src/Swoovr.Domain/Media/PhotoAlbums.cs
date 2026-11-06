namespace Swoovr.Domain;

/// <summary>
/// #231 (owner, 6–7 November 2026): a member's private album. Every member has up to <see cref="PhotoAlbumPolicy.MaxAlbums"/>,
/// whatever the plan. Album photos are library photos (they use library spots); an adult photo approved for albums only
/// (<see cref="MediaItem.AlbumOnly"/>) lives in the one album it was uploaded into.
/// </summary>
public sealed class PhotoAlbum
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A photo in an album, in order. Removing a library photo from an album keeps it in the library.</summary>
public sealed class PhotoAlbumItem
{
    public Guid AlbumId { get; set; }
    public Guid MediaId { get; set; }
    public int Position { get; set; }
}

/// <summary>A mode whose chats the album may be shared in. Sharing itself is a later slice.</summary>
public sealed class PhotoAlbumMode
{
    public Guid AlbumId { get; set; }
    public string ModeId { get; set; } = "";
}

/// <summary>
/// #231: "Receive explicit media", one row per profile (mode). Off unless a row says otherwise, and only effective while
/// the member is ID Verified (<see cref="ExplicitMediaPolicy.Effective"/>).
/// </summary>
public sealed class ExplicitMediaPreference
{
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
    public bool Receive { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public static class PhotoAlbumPolicy
{
    /// <summary>CONFIRMED (owner, 6 November 2026): four albums for every plan.</summary>
    public const int MaxAlbums = 4;
    /// <summary>CONFIRMED (#231): up to ten photos per album.</summary>
    public const int MaxPhotos = 10;
    public const int NameMax = 40;

    /// <summary>A trimmed name of 1–40 characters without control characters, or null.</summary>
    public static string? Name(string? name)
    {
        var trimmed = name?.Trim();
        return trimmed is { Length: > 0 and <= NameMax } && !trimmed.Any(char.IsControl) ? trimmed : null;
    }

    /// <summary>
    /// Only approved library photos that aren't album-only can be added from the library; an album-only photo stays in
    /// the album it was uploaded into.
    /// </summary>
    public static bool Addable(MediaItem item) => item is { Purpose: null, Status: MediaStatus.Passed, RemovedAt: null, AlbumOnly: false };

    /// <summary>A library photo can be removed from an album; an album-only one can only be deleted.</summary>
    public static bool Removable(MediaItem item) => !item.AlbumOnly;
}

public static class ExplicitMediaPolicy
{
    /// <summary>
    /// CONFIRMED (owner, 6 November 2026; supersedes D178): explicit media needs ID Verified status. A preference stored
    /// while verified stops counting if verification lapses.
    /// </summary>
    public static bool Allowed(MemberProfile profile) => profile.IdStatus == VerificationStatus.Verified;

    public static bool Effective(MemberProfile profile, bool stored) => stored && Allowed(profile);

    /// <summary>
    /// A reviewer may approve a photo for private albums only when it was uploaded into an album and its owner is ID
    /// Verified; explicit media is never approved for a profile mode.
    /// </summary>
    public static bool AlbumApprovalAllowed(MediaItem item, MemberProfile owner) => item.AlbumId is not null && item.Purpose is null && Allowed(owner);
}
