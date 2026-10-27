using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

/// <summary>
/// Where an uploaded photo is in the quarantine pipeline (policy §5; D201). Only <see cref="Passed"/> is ever shown to
/// anyone else. Pending is also where anything the screener couldn't answer, or answered in a way Swoovr doesn't
/// understand, stays (fail closed).
/// </summary>
public enum MediaStatus
{
    Pending,
    Passed,
    /// <summary>Restricted admin review (blurred by default, every view with a recorded purpose).</summary>
    Uncertain,
    /// <summary>Known abuse material: sealed as evidence, a Serious Safety Incident, never viewable by staff (procedure §4).</summary>
    Blocked,
    /// <summary>A reviewer refused it (for example explicit content, D178).</summary>
    Rejected,
    /// <summary>The member deleted it or replaced it, or the account was deleted.</summary>
    Removed,
}

/// <summary>
/// One uploaded photo. The stored object is the re-encoded image (EXIF and GPS stripped), never the original upload;
/// <see cref="Sha256"/> is of the stored bytes and <see cref="UploadSha256"/> of what the member sent. No foreign key to
/// accounts, so a held or blocked item outlives the account it came from.
/// </summary>
public sealed class MediaItem
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public MediaStatus Status { get; set; }
    /// <summary>The private object's key (quarantine/, photos/ or evidence/); null once the object is deleted.</summary>
    public string? ObjectKey { get; set; }
    public byte[] Sha256 { get; set; } = [];
    public byte[] UploadSha256 { get; set; } = [];
    public string ContentType { get; set; } = "image/jpeg";
    public int Width { get; set; }
    public int Height { get; set; }
    public int Bytes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int ScreenAttempts { get; set; }
    public DateTimeOffset? NextScreenAt { get; set; }
    /// <summary>Why the last attempt left it pending (a machine code such as `unreachable` or `unknown-label`).</summary>
    public string? LastScreenError { get; set; }
    public DateTimeOffset? ScreenedAt { get; set; }
    /// <summary>`image-moderation-api` or `simulated` (Development only, labelled).</summary>
    public string? Screener { get; set; }
    public string? PolicyVersion { get; set; }
    /// <summary>The signals behind the decision, as small JSON (states, matched classes and scores; never the image or hashes).</summary>
    public string? Signals { get; set; }
    public bool KnownContentChecked { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewReason { get; set; }
    /// <summary>The photo this one replaces once it passes (re-scanned like any upload).</summary>
    public Guid? ReplacesId { get; set; }
    public Guid? CaseId { get; set; }
    public Guid? HoldId { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public DateTimeOffset? ObjectDeletedAt { get; set; }
}

/// <summary>
/// Where a passed photo shows: scope `main` (the one main photo, shown in every mode the member is shown in, D175) or a
/// mode id (photos the member picked for that mode only, #91). Position orders a mode's photos.
/// </summary>
public sealed class MediaPlacement
{
    public Guid AccountId { get; set; }
    public string Scope { get; set; } = "";
    public int Position { get; set; }
    public Guid MediaId { get; set; }
}

/// <summary>A staff view of an uncertain photo, with the recorded purpose (policy §11). Never possible for a blocked one.</summary>
public sealed class MediaReviewView
{
    public long Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid ActorAccountId { get; set; }
    public string Purpose { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public static class MediaPolicy
{
    public const string Main = "main";
    /// <summary>DEFAULT (D203): limits on what the member sends and what Swoovr keeps.</summary>
    public const int MaxUploadBytes = 10 * 1024 * 1024;
    public const int MinSide = 200;
    public const int MaxSide = 8192;
    public const long MaxPixels = 40_000_000;
    /// <summary>The stored copy's longest side and JPEG quality.</summary>
    public const int StoredMaxSide = 2048;
    public const int JpegQuality = 85;
    public const int MaxPerMode = 6;
    public const int MaxKept = 30;
    public const int UploadsPerHour = 20;
    public static readonly TimeSpan LinkLifetime = TimeSpan.FromMinutes(10);
    public const int PurposeMax = 500;
    public const int ReasonMax = 500;

    /// <summary>The content types Swoovr decodes, recognised from the bytes (never the file name or the client's claim).</summary>
    public static string? Sniff(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF) return "image/jpeg";
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })) return "image/png";
        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8)) return "image/webp";
        return null;
    }

    public static MediaUploadError CheckSize(int width, int height) =>
        width < MinSide || height < MinSide ? MediaUploadError.TooSmall
        : width > MaxSide || height > MaxSide || (long)width * height > MaxPixels ? MediaUploadError.TooLarge
        : MediaUploadError.None;

    /// <summary>A placement scope: `main` or a mode id the catalogue offers.</summary>
    public static bool ValidScope(string scope, IReadOnlyCollection<string> modes) => scope == Main || modes.Contains(scope);

    /// <summary>When to try a pending item again: 1, 5, 15 minutes, then hourly. It never becomes visible while pending.</summary>
    public static DateTimeOffset NextAttempt(int attempts, DateTimeOffset now) => now + (attempts switch
    {
        <= 1 => TimeSpan.FromMinutes(1),
        2 => TimeSpan.FromMinutes(5),
        3 => TimeSpan.FromMinutes(15),
        _ => TimeSpan.FromHours(1),
    });

    /// <summary>Only passed photos may be shown to anyone but staff reviewing an uncertain one.</summary>
    public static bool Shown(MediaStatus status) => status == MediaStatus.Passed;

    /// <summary>
    /// Whether the stored object may be deleted now: never while a live hold names the account or hold status is
    /// unknown (null, fail closed), and a blocked item only after the hold placed for it was released.
    /// </summary>
    public static bool MayDeleteObject(MediaItem item, IReadOnlySet<Guid>? heldAccounts, bool ownHoldReleased)
    {
        if (heldAccounts is null || item.ObjectKey is null) return false;
        if (heldAccounts.Contains(item.AccountId)) return false;
        if (item.Status == MediaStatus.Blocked) return item.HoldId is not null && ownHoldReleased;
        return item.Status is MediaStatus.Rejected or MediaStatus.Removed;
    }

    /// <summary>What the member is told about their own photo. Blocked reads exactly like a rejection (procedure §4: never tip anyone off).</summary>
    public static string MemberState(MediaStatus status) => status switch
    {
        MediaStatus.Passed => "approved",
        MediaStatus.Pending => "checking",
        MediaStatus.Uncertain => "in-review",
        MediaStatus.Removed => "removed",
        _ => "not-approved",
    };

    public static byte[] Hash(ReadOnlySpan<byte> data) => SHA256.HashData(data);

    // Signed links: the id, variant and expiry, signed with HMAC-SHA256. Access is checked when the link is made.
    public static string Link(byte[] key, Guid media, string variant, DateTimeOffset expires)
    {
        var payload = $"{media:N}.{variant}.{expires.ToUnixTimeSeconds()}";
        var mac = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes(payload));
        return payload + "." + Base64Url(mac);
    }

    public static (Guid Media, string Variant)? ReadLink(byte[] key, string token, DateTimeOffset now)
    {
        var parts = token.Split('.');
        if (parts.Length != 4 || !Guid.TryParseExact(parts[0], "N", out var media) || parts[1] is not ("member" or "review")
            || !long.TryParse(parts[2], out var seconds)) return null;
        var expected = HMACSHA256.HashData(key, Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}.{parts[2]}"));
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Base64Url(expected)), Encoding.ASCII.GetBytes(parts[3]))) return null;
        return DateTimeOffset.FromUnixTimeSeconds(seconds) < now ? null : (media, parts[1]);
    }

    private static string Base64Url(byte[] data) => Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public enum MediaUploadError { None, Empty, TooBig, UnsupportedType, Undecodable, Animated, TooSmall, TooLarge }

/// <summary>
/// Private object storage (D201: Cloudflare R2; a local private folder for tests). Nothing in it is public: Swoovr reads
/// objects itself and serves them only through short-lived signed links after checking access.
/// </summary>
public interface IMediaStore
{
    /// <summary>`r2` or `local`, recorded in evidence and the logs.</summary>
    string Name { get; }
    Task PutAsync(string key, byte[] data, string contentType, CancellationToken cancellationToken);
    /// <summary>The object, or null when it doesn't exist.</summary>
    Task<byte[]?> GetAsync(string key, CancellationToken cancellationToken);
    /// <summary>True when the object is gone afterwards (deleting a missing object is success).</summary>
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken);
}

/// <summary>The screening service (D201: image-moderation-api). It returns signals; <see cref="MediaScreeningPolicy"/> decides.</summary>
public interface IMediaScreening
{
    /// <summary>`image-moderation-api` or `simulated`.</summary>
    string Name { get; }
    /// <summary>Never throws for service trouble: an unusable answer comes back with <see cref="ScreeningResponse.Unreachable"/> set.</summary>
    Task<ScreeningResponse> ScreenAsync(byte[] image, CancellationToken cancellationToken);
}

/// <summary>The stored copy of an upload: re-encoded JPEG without any metadata (EXIF, GPS, XMP, ICC comments).</summary>
public sealed record ProcessedPhoto(MediaUploadError Error, byte[] Data, int Width, int Height, string? SourceType);

public interface IPhotoProcessor
{
    ProcessedPhoto Process(byte[] upload);
}

/// <summary>An engine's own state, as the screening service reports it (results-reference §2).</summary>
public enum EngineState { Completed, Failed, Disabled, NotRequested, Unknown }

/// <summary>One NudeNet detection: the exact label and score (the box isn't kept).</summary>
public sealed record NudityDetection(string Label, double Score);

/// <summary>
/// The screening service's answer, parsed and validated by the adapter (never its raw JSON). Malformed fields arrive as
/// <see cref="EngineState.Unknown"/>. <see cref="Unreachable"/> means no usable scan envelope at all (network, 5xx without
/// an envelope, timeout, not ready, not JSON) with a machine reason.
/// </summary>
public sealed record ScreeningResponse(
    string? Unreachable,
    EngineState Nudity,
    IReadOnlyList<NudityDetection> Detections,
    EngineState Nsfw,
    bool? IsNsfw,
    EngineState KnownContent,
    string? KnownClassification,
    string? MatchType = null,
    string? ScanId = null)
{
    public static ScreeningResponse Failed(string reason) => new(reason, EngineState.Unknown, [], EngineState.Unknown, null, EngineState.Unknown, null);
}

public enum ScreeningVerdict { Pending, Passed, Uncertain, Blocked }

public sealed record ScreeningDecision(ScreeningVerdict Verdict, string Reason, bool KnownContentChecked, IReadOnlyList<NudityDetection> Evidence);

/// <summary>
/// Swoovr's own screening policy over the service's raw signals (D203, DEFAULT; thresholds are Q81). The service decides
/// nothing (results-reference §1): nudity, NSFW and known-content matching stay separate signals, a known-content match is
/// never outweighed by local negatives, and anything not checked, failed or not understood is Pending, never clean.
/// </summary>
public static class MediaScreeningPolicy
{
    public const string Version = "swoovr-photos-2026-10-28";
    /// <summary>Exposed intimate anatomy (results-reference §3). Bare chest, covered anatomy, faces, feet, belly and armpits never count.</summary>
    public static readonly IReadOnlySet<string> NudityClasses = new HashSet<string>
        { "FEMALE_GENITALIA_EXPOSED", "MALE_GENITALIA_EXPOSED", "ANUS_EXPOSED", "FEMALE_BREAST_EXPOSED", "BUTTOCKS_EXPOSED" };
    /// <summary>Every label the pinned NudeNet 3.4.2 can return; any other means the answer isn't understood.</summary>
    public static readonly IReadOnlySet<string> KnownLabels = new HashSet<string>
    {
        "FEMALE_GENITALIA_EXPOSED", "MALE_GENITALIA_EXPOSED", "ANUS_EXPOSED", "FEMALE_BREAST_EXPOSED", "BUTTOCKS_EXPOSED", "MALE_BREAST_EXPOSED",
        "FEMALE_GENITALIA_COVERED", "FEMALE_BREAST_COVERED", "BUTTOCKS_COVERED", "ANUS_COVERED", "BELLY_EXPOSED", "BELLY_COVERED",
        "ARMPITS_EXPOSED", "ARMPITS_COVERED", "FEET_EXPOSED", "FEET_COVERED", "FACE_FEMALE", "FACE_MALE",
    };
    /// <summary>DEFAULT, uncalibrated: a selected class at or above this score sends the photo to review (Q81).</summary>
    public const double NudityThreshold = 0.40;

    public static ScreeningDecision Decide(ScreeningResponse response, bool requireKnownContent)
    {
        // 1. A known-content match blocks, whatever else happened (results-reference §5, §6).
        if (response.KnownContent == EngineState.Completed && response.KnownClassification is "csam" or "harmful-abusive-material")
            return new(ScreeningVerdict.Blocked, "known-content-match", true, []);
        if (response.Unreachable is { } reason) return Pending(reason);
        if (response.Nudity != EngineState.Completed) return Pending("nudity-unchecked");
        if (response.Nsfw != EngineState.Completed || response.IsNsfw is null) return Pending("nsfw-unchecked");
        var known = response.KnownContent == EngineState.Completed;
        if (known && response.KnownClassification is not ("no-known-match" or "test")) return Pending("unknown-classification");
        if (!known && requireKnownContent) return Pending("known-content-unchecked");
        if (response.Detections.Any(d => !KnownLabels.Contains(d.Label) || double.IsNaN(d.Score))) return Pending("unknown-label");
        // 2. A provider test classification is neither a real match nor a clean result: a person looks at it.
        if (known && response.KnownClassification == "test") return new(ScreeningVerdict.Uncertain, "known-content-test", true, []);
        var evidence = response.Detections.Where(d => NudityClasses.Contains(d.Label) && d.Score >= NudityThreshold).ToList();
        if (evidence.Count > 0) return new(ScreeningVerdict.Uncertain, "nudity-signal", known, evidence);
        if (response.IsNsfw == true) return new(ScreeningVerdict.Uncertain, "nsfw-signal", known, []);
        return new(ScreeningVerdict.Passed, known ? "no-signal" : "no-signal-known-content-not-run", known, []);
    }

    private static ScreeningDecision Pending(string reason) => new(ScreeningVerdict.Pending, reason, false, []);

    public static MediaStatus StatusFor(ScreeningVerdict verdict) => verdict switch
    {
        ScreeningVerdict.Passed => MediaStatus.Passed,
        ScreeningVerdict.Uncertain => MediaStatus.Uncertain,
        ScreeningVerdict.Blocked => MediaStatus.Blocked,
        _ => MediaStatus.Pending,
    };

    /// <summary>The Development-only labelled override (`Media:Screening:Simulate`); anything else is refused at start-up.</summary>
    public static ScreeningVerdict? Simulated(string? setting) => setting switch
    {
        null or "" => null,
        "uncertain" => ScreeningVerdict.Uncertain,
        "blocked" => ScreeningVerdict.Blocked,
        _ => throw new InvalidOperationException("Media:Screening:Simulate must be uncertain or blocked."),
    };
}
