using System.Text;
using SkiaSharp;
using Swoovr.Domain;
using Swoovr.Infrastructure.Media;

/// <summary>
/// Photos with media quarantine (D201, D203; policy §5, §6, §10; procedure §4). Synthetic images only (solid colours,
/// gradients, text); never real people. The screening policy keeps nudity, NSFW and known-content signals apart, never
/// lets local negatives outweigh a known-content match, and leaves anything not checked or not understood pending.
/// </summary>
static class MediaChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Media check failed: " + what); checks++; }
        ScreeningResponse Clean(string? known = "no-known-match", EngineState knownState = EngineState.Completed, IReadOnlyList<NudityDetection>? detections = null, bool nsfw = false) =>
            new(null, EngineState.Completed, detections ?? [], EngineState.Completed, nsfw, knownState, known);

        // Decisions.
        Expect(MediaScreeningPolicy.Decide(Clean(), true).Verdict == ScreeningVerdict.Passed, "clean passes");
        Expect(MediaScreeningPolicy.Decide(Clean("csam"), true).Verdict == ScreeningVerdict.Blocked, "csam blocks");
        Expect(MediaScreeningPolicy.Decide(Clean("harmful-abusive-material"), true).Verdict == ScreeningVerdict.Blocked, "harmful-abusive blocks");
        var failedLocal = new ScreeningResponse(null, EngineState.Failed, [], EngineState.Failed, null, EngineState.Completed, "csam");
        Expect(MediaScreeningPolicy.Decide(failedLocal, true).Verdict == ScreeningVerdict.Blocked, "a match blocks even when local engines failed");
        Expect(MediaScreeningPolicy.Decide(Clean(null, EngineState.Disabled), true) is { Verdict: ScreeningVerdict.Pending, Reason: "known-content-unchecked" }, "no known-content check stays pending when required");
        Expect(MediaScreeningPolicy.Decide(Clean(null, EngineState.Disabled), false) is { Verdict: ScreeningVerdict.Passed, KnownContentChecked: false }, "Development may pass without it, labelled");
        Expect(MediaScreeningPolicy.Decide(Clean("something-new"), true).Verdict == ScreeningVerdict.Pending, "an unknown classification stays pending");
        Expect(MediaScreeningPolicy.Decide(Clean("test"), true).Verdict == ScreeningVerdict.Uncertain, "a provider test result goes to review");
        Expect(MediaScreeningPolicy.Decide(ScreeningResponse.Failed("unreachable"), true).Verdict == ScreeningVerdict.Pending, "unreachable stays pending");
        Expect(MediaScreeningPolicy.Decide(Clean() with { Nudity = EngineState.Failed }, true).Verdict == ScreeningVerdict.Pending, "nudity unchecked stays pending");
        Expect(MediaScreeningPolicy.Decide(Clean() with { IsNsfw = null }, true).Verdict == ScreeningVerdict.Pending, "NSFW unchecked stays pending");
        Expect(MediaScreeningPolicy.Decide(Clean(detections: [new("SOMETHING_ELSE", 0.1)]), true).Verdict == ScreeningVerdict.Pending, "an unknown label stays pending");
        Expect(MediaScreeningPolicy.Decide(Clean(detections: [new("MALE_GENITALIA_EXPOSED", 0.7)]), true) is { Verdict: ScreeningVerdict.Uncertain, Reason: "nudity-signal" }, "a nudity signal goes to review");
        Expect(MediaScreeningPolicy.Decide(Clean(detections: [new("FEMALE_BREAST_EXPOSED", 0.39)]), true).Verdict == ScreeningVerdict.Passed, "below the threshold passes");
        Expect(MediaScreeningPolicy.Decide(Clean(detections: [new("ARMPITS_EXPOSED", 0.99), new("FACE_FEMALE", 0.9), new("MALE_BREAST_EXPOSED", 0.9)]), true).Verdict == ScreeningVerdict.Passed,
            "armpits, faces and bare chests don't count");
        Expect(MediaScreeningPolicy.Decide(Clean(nsfw: true), true) is { Verdict: ScreeningVerdict.Uncertain, Reason: "nsfw-signal" }, "the NSFW signal goes to review");
        Expect(MediaScreeningPolicy.Simulated(null) is null && MediaScreeningPolicy.Simulated("blocked") == ScreeningVerdict.Blocked, "simulate values");
        try { MediaScreeningPolicy.Simulated("passed"); Expect(false, "simulate never passes"); } catch (InvalidOperationException) { checks++; }
        Expect(MediaScreeningPolicy.Decide(new SimulatedScreening(ScreeningVerdict.Blocked).ScreenAsync([], default).Result, false).Verdict == ScreeningVerdict.Blocked, "simulated blocked");
        Expect(MediaScreeningPolicy.Decide(new SimulatedScreening(ScreeningVerdict.Uncertain).ScreenAsync([], default).Result, false).Verdict == ScreeningVerdict.Uncertain, "simulated uncertain");

        // The adapter's parser (shapes from docs/results-reference.md).
        var completed = ImageModerationScreening.Parse("""{"scan_id":"x","status":"completed","results":{"nudenet":{"status":"completed","detections":[{"class":"FACE_MALE","score":0.8,"box":[1,2,3,4]}]},"nsfw":{"status":"completed","is_nsfw":false,"classification":false,"probabilities":[{}]},"arachnid":{"status":"disabled"}}}""");
        Expect(completed is { Unreachable: null, Nudity: EngineState.Completed, Nsfw: EngineState.Completed, IsNsfw: false, KnownContent: EngineState.Disabled } && completed.Detections.Count == 1, "parses a completed scan");
        Expect(ImageModerationScreening.Parse("not json").Unreachable == "not-understood", "not JSON");
        Expect(ImageModerationScreening.Parse("""{"status":"weird","results":{}}""").Unreachable == "not-understood", "unknown overall status");
        Expect(ImageModerationScreening.Parse("""{"status":"completed","results":{"nudenet":{"status":"completed"},"nsfw":{"status":"completed","is_nsfw":"false"},"arachnid":{"status":"completed"}}}""")
            is { Nudity: EngineState.Unknown, Nsfw: EngineState.Unknown, KnownContent: EngineState.Unknown }, "wrong types become unknown, never clean");
        var matched = ImageModerationScreening.Parse("""{"status":"partial","results":{"nudenet":{"status":"failed","error":{"code":"inference_failed","message":"x"}},"nsfw":{"status":"completed","is_nsfw":false},"arachnid":{"status":"completed","classification":"csam","match_type":"near","pdq_quality":90,"raw":{}}}}""");
        Expect(MediaScreeningPolicy.Decide(matched, true).Verdict == ScreeningVerdict.Blocked, "a partial scan with a match blocks");

        // Uploads and limits.
        Expect(MediaPolicy.Sniff([0xFF, 0xD8, 0xFF, 0xE0]) == "image/jpeg" && MediaPolicy.Sniff(Encoding.ASCII.GetBytes("GIF89a")) is null, "type from the bytes");
        Expect(MediaPolicy.CheckSize(199, 400) == MediaUploadError.TooSmall && MediaPolicy.CheckSize(9000, 400) == MediaUploadError.TooLarge && MediaPolicy.CheckSize(800, 600) == MediaUploadError.None, "sizes");
        Expect(MediaPolicy.MemberState(MediaStatus.Blocked) == MediaPolicy.MemberState(MediaStatus.Rejected), "blocked reads exactly like a refusal");
        Expect(!MediaPolicy.Shown(MediaStatus.Pending) && !MediaPolicy.Shown(MediaStatus.Uncertain) && !MediaPolicy.Shown(MediaStatus.Blocked) && MediaPolicy.Shown(MediaStatus.Passed), "only passed shows");
        Expect(MediaPolicy.NextAttempt(1, DateTimeOffset.UnixEpoch) == DateTimeOffset.UnixEpoch.AddMinutes(1) && MediaPolicy.NextAttempt(9, DateTimeOffset.UnixEpoch) == DateTimeOffset.UnixEpoch.AddHours(1), "backoff");

        // Signed links.
        var key = new byte[32]; key[0] = 7;
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var link = MediaPolicy.Link(key, id, "member", now.AddMinutes(10));
        Expect(MediaPolicy.ReadLink(key, link, now) == (id, "member"), "a link reads back");
        Expect(MediaPolicy.ReadLink(key, link, now.AddMinutes(11)) is null, "an expired link is refused");
        Expect(MediaPolicy.ReadLink(key, link.Replace(".member.", ".review."), now) is null, "a tampered variant is refused");
        Expect(MediaPolicy.ReadLink(new byte[32], link, now) is null, "another key's link is refused");

        // Objects are deleted only when nothing holds them (fail closed).
        var item = new MediaItem { AccountId = Guid.NewGuid(), Status = MediaStatus.Removed, ObjectKey = "photos/x" };
        Expect(MediaPolicy.MayDeleteObject(item, new HashSet<Guid>(), false), "a removed photo's object goes");
        Expect(!MediaPolicy.MayDeleteObject(item, null, false), "hold status unknown: kept");
        Expect(!MediaPolicy.MayDeleteObject(item, new HashSet<Guid> { item.AccountId }, false), "held: kept");
        Expect(!MediaPolicy.MayDeleteObject(new MediaItem { Status = MediaStatus.Passed, ObjectKey = "photos/x" }, new HashSet<Guid>(), false), "a shown photo stays");
        Expect(!MediaPolicy.MayDeleteObject(new MediaItem { Status = MediaStatus.Blocked, ObjectKey = "evidence/x" }, new HashSet<Guid>(), false), "blocked without a released hold stays");
        Expect(MediaPolicy.MayDeleteObject(new MediaItem { Status = MediaStatus.Blocked, ObjectKey = "evidence/x", HoldId = Guid.NewGuid() }, new HashSet<Guid>(), true), "blocked after its hold's release goes");
        Expect(MediaKeys.Valid(MediaKeys.Quarantine(id)) && !MediaKeys.Valid("../etc/passwd") && !MediaKeys.Valid("photos/../../x"), "object keys");

        // The sensitive-access log covers the review routes.
        Expect(StaffAccessPolicy.Classify("POST", $"/api/v1/admin/media/{id}/view", 200) == ("media", id.ToString(), "view"), "a review view is logged as a view");
        Expect(StaffAccessPolicy.Classify("GET", "/api/v1/admin/media/review", 200) == ("media", null, "query"), "the queue is logged");

        // The processor: EXIF with GPS and an orientation goes in; a re-encoded JPEG without metadata, turned upright, comes out.
        var processor = new PhotoProcessor();
        var source = SyntheticJpeg(300, 200);
        var withExif = InsertExif(source, orientation: 6);
        Expect(Encoding.ASCII.GetString(withExif).Contains("Exif") && Encoding.ASCII.GetString(withExif).Contains("SWOOVRGPS"), "the synthetic upload carries EXIF and GPS");
        var processed = processor.Process(withExif);
        Expect(processed.Error == MediaUploadError.None && processed.SourceType == "image/jpeg", "the upload is accepted");
        Expect(!Encoding.ASCII.GetString(processed.Data).Contains("Exif") && !Encoding.ASCII.GetString(processed.Data).Contains("SWOOVRGPS"), "EXIF and GPS are gone");
        Expect((processed.Width, processed.Height) == (200, 300), "the orientation is applied");
        Expect(processor.Process(Encoding.ASCII.GetBytes("GIF89a....")).Error == MediaUploadError.UnsupportedType, "GIF refused");
        Expect(processor.Process(SyntheticPng(100, 100)).Error == MediaUploadError.TooSmall, "too small refused");
        Expect(processor.Process([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3]).Error == MediaUploadError.Undecodable, "a broken PNG is refused");
        var big = processor.Process(SyntheticPng(3000, 1000));
        Expect(big.Error == MediaUploadError.None && big.Width == MediaPolicy.StoredMaxSide && big.Height == 683, "large photos are scaled to 2048");
        Console.WriteLine($"PASS: photos and media quarantine ({checks}).");
    }

    /// <summary>A gradient with a text label: synthetic, never a person.</summary>
    public static SKImage Synthetic(int width, int height)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        using var paint = new SKPaint { Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(width, height), [SKColors.SteelBlue, SKColors.Orange], SKShaderTileMode.Clamp) };
        surface.Canvas.DrawRect(0, 0, width, height, paint);
        using var font = new SKFont(SKTypeface.Default, 24);
        using var ink = new SKPaint { Color = SKColors.Black };
        surface.Canvas.DrawText("SWOOVR TEST", 10, 40, font, ink);
        return surface.Snapshot();
    }

    public static byte[] SyntheticJpeg(int width, int height) { using var image = Synthetic(width, height); return image.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray(); }
    public static byte[] SyntheticPng(int width, int height) { using var image = Synthetic(width, height); return image.Encode(SKEncodedImageFormat.Png, 100).ToArray(); }

    /// <summary>Adds an APP1 Exif segment (little-endian TIFF) with an orientation and a GPS IFD holding a marker string.</summary>
    public static byte[] InsertExif(byte[] jpeg, ushort orientation)
    {
        var tiff = new List<byte>();
        void U16(int v) { tiff.Add((byte)v); tiff.Add((byte)(v >> 8)); }
        void U32(int v) { U16(v & 0xFFFF); U16(v >> 16); }
        tiff.AddRange("II"u8.ToArray()); U16(42); U32(8);
        // IFD0 at 8: two entries (orientation, GPS pointer), next IFD 0.
        U16(2);
        U16(0x0112); U16(3); U32(1); U16(orientation); U16(0);
        const int gpsOffset = 8 + 2 + 2 * 12 + 4;
        U16(0x8825); U16(4); U32(1); U32(gpsOffset);
        U32(0);
        // GPS IFD: GPSLatitudeRef "S" inline, and GPSMapDatum pointing at a marker string.
        var marker = "SWOOVRGPS\0"u8.ToArray();
        U16(2);
        U16(0x0001); U16(2); U32(2); tiff.Add((byte)'S'); tiff.Add(0); U16(0);
        U16(0x0012); U16(2); U32(marker.Length); U32(gpsOffset + 2 + 2 * 12 + 4);
        U32(0);
        tiff.AddRange(marker);
        var payload = "Exif\0\0"u8.ToArray().Concat(tiff).ToArray();
        var segment = new List<byte> { 0xFF, 0xE1, (byte)((payload.Length + 2) >> 8), (byte)(payload.Length + 2) };
        segment.AddRange(payload);
        return jpeg.Take(2).Concat(segment).Concat(jpeg.Skip(2)).ToArray();
    }
}
