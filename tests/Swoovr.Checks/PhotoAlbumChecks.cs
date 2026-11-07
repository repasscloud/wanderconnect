using Swoovr.Client;
using Swoovr.Domain;

/// <summary>
/// #231 (owner, 6–7 November 2026; supersedes D178): private albums and explicit media. Four albums for every plan, ten
/// photos each; only approved, non-album-only library photos can be added; adult photos approved for albums only can't be
/// removed (only deleted); approval for albums only needs an album upload from an ID Verified owner; explicit media needs
/// ID Verified status; the owner's preview of a refused photo never covers blocked material.
/// </summary>
static class PhotoAlbumChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Photo album check failed: " + what); checks++; }
        var now = DateTimeOffset.UtcNow;

        Expect(PhotoAlbumPolicy.MaxAlbums == 4 && PhotoAlbumPolicy.MaxPhotos == 10, "four albums of ten photos for every plan");
        Expect(PhotoAlbumPolicy.Name("  Weekend away ") == "Weekend away", "names are trimmed");
        Expect(PhotoAlbumPolicy.Name("") is null && PhotoAlbumPolicy.Name("   ") is null && PhotoAlbumPolicy.Name(null) is null, "a name is required");
        Expect(PhotoAlbumPolicy.Name(new string('a', 41)) is null && PhotoAlbumPolicy.Name(new string('a', 40)) is not null, "names are at most 40 characters");
        Expect(PhotoAlbumPolicy.Name("Bad\u0007name") is null, "control characters are refused");

        MediaItem Item(MediaStatus status, bool albumOnly = false, string? purpose = null, DateTimeOffset? removed = null, Guid? album = null) =>
            new() { Id = Guid.NewGuid(), Status = status, AlbumOnly = albumOnly, Purpose = purpose, RemovedAt = removed, AlbumId = album };
        Expect(PhotoAlbumPolicy.Addable(Item(MediaStatus.Passed)), "an approved library photo can be added");
        foreach (var status in Enum.GetValues<MediaStatus>().Where(s => s != MediaStatus.Passed))
            Expect(!PhotoAlbumPolicy.Addable(Item(status)), $"a {status} photo can't be added");
        Expect(!PhotoAlbumPolicy.Addable(Item(MediaStatus.Passed, albumOnly: true)), "an album-only photo can't be added to another album");
        Expect(!PhotoAlbumPolicy.Addable(Item(MediaStatus.Passed, purpose: "attachment")), "a chat photo can't be added");
        Expect(!PhotoAlbumPolicy.Addable(Item(MediaStatus.Passed, removed: now)), "a deleted photo can't be added");
        Expect(PhotoAlbumPolicy.Removable(Item(MediaStatus.Passed)) && !PhotoAlbumPolicy.Removable(Item(MediaStatus.Passed, albumOnly: true)),
            "library photos can be removed from an album; album-only photos can only be deleted");

        MemberProfile Owner(VerificationStatus id) => new() { AccountId = Guid.NewGuid(), IdStatus = id };
        foreach (var id in Enum.GetValues<VerificationStatus>())
        {
            Expect(ExplicitMediaPolicy.Allowed(Owner(id)) == (id == VerificationStatus.Verified), $"explicit media needs ID Verified ({id})");
            Expect(ExplicitMediaPolicy.Effective(Owner(id), true) == (id == VerificationStatus.Verified), $"a stored choice only counts while ID Verified ({id})");
            Expect(!ExplicitMediaPolicy.Effective(Owner(id), false), $"off stays off ({id})");
        }
        var album = Guid.NewGuid();
        Expect(ExplicitMediaPolicy.AlbumApprovalAllowed(Item(MediaStatus.Uncertain, album: album), Owner(VerificationStatus.Verified)), "an album upload by an ID Verified owner can be approved for albums only");
        Expect(!ExplicitMediaPolicy.AlbumApprovalAllowed(Item(MediaStatus.Uncertain), Owner(VerificationStatus.Verified)), "never a library upload");
        Expect(!ExplicitMediaPolicy.AlbumApprovalAllowed(Item(MediaStatus.Uncertain, album: album), Owner(VerificationStatus.Pending)), "never without ID Verified");
        Expect(!ExplicitMediaPolicy.AlbumApprovalAllowed(Item(MediaStatus.Uncertain, purpose: "attachment", album: album), Owner(VerificationStatus.Verified)), "never a chat photo");

        var reason = MediaReviewReasons.All.Single(r => r.Code == "APPROVED_ALBUM_EXPLICIT");
        Expect(reason.Decision == "approve" && reason.Selectable && !reason.RequiresNotes, "the albums-only approval is a structured approve reason");
        Expect(MediaReviewReasons.Create("approve", "APPROVED_ALBUM_EXPLICIT", null) is { Code: "APPROVED_ALBUM_EXPLICIT" }, "it can be chosen");
        Expect(MediaReviewReasons.Create("reject", "APPROVED_ALBUM_EXPLICIT", null) is null, "only as an approval");

        // The owner's preview of a refused photo: only rejected ones, only while appealable, never blocked material.
        MediaItem Refused(MediaStatus status, DateTimeOffset? until) => new() { Status = status, RejectedRetainUntil = until };
        Expect(MediaPolicy.OwnerPreview(Refused(MediaStatus.Rejected, now.AddDays(3)), now), "a rejected photo can be previewed by its owner while it can be appealed");
        Expect(!MediaPolicy.OwnerPreview(Refused(MediaStatus.Rejected, now.AddSeconds(-1)), now), "not after the appeal window");
        Expect(!MediaPolicy.OwnerPreview(Refused(MediaStatus.Blocked, now.AddDays(3)), now), "never a blocked one");
        Expect(!MediaPolicy.OwnerPreview(new MediaItem { Status = MediaStatus.Rejected, RejectedRetainUntil = now.AddDays(3), RemovedAt = now }, now), "never after the owner deleted it");
        var key = new byte[32]; var media = Guid.NewGuid();
        Expect(MediaPolicy.ReadLink(key, MediaPolicy.Link(key, media, "own", now.AddMinutes(10)), now) == (media, "own"), "owner preview links are signed like the others");
        Expect(MediaPolicy.ReadLink(key, MediaPolicy.Link(key, media, "own", now.AddMinutes(10)).Replace(".own.", ".member."), now) is null, "an owner link can't be turned into a member link");

        Expect(PhotoText.Short("approved", true) == "Approved for private albums only" && PhotoText.Short("approved", false) == "Approved", "library wording");
        Expect(!PhotoText.AlbumRules(false).Contains("Adult content is allowed", StringComparison.Ordinal), "unverified members are never told adult content is allowed");
        Console.WriteLine($"PASS: private photo albums and explicit media ({checks} checks).");
    }
}
