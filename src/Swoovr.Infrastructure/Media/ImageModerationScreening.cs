using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Media;

/// <summary>Screening settings: `Media:Screening:BaseUrl` and the optional `Media:Screening:ApiKey` (a Bearer key).</summary>
public sealed record ScreeningOptions(Uri? BaseUrl, string? ApiKey)
{
    public bool Configured => BaseUrl is not null;
}

/// <summary>
/// The owner's image-moderation-api (D201; its README, docs/openapi.json and docs/results-reference.md, read 28 October
/// 2026). Swoovr sends the stored (re-encoded) image as one multipart `file` to POST /v1/scan, after checking GET /ready.
/// The response is parsed strictly: a field that is missing, has the wrong type or a value the reference doesn't list
/// becomes Unknown, which the policy treats as not checked. Nothing about the image, hashes or raw provider fields is logged.
/// </summary>
public sealed class ImageModerationScreening(HttpClient http, ScreeningOptions options, ILogger<ImageModerationScreening> log) : IMediaScreening
{
    public string Name => "image-moderation-api";

    public async Task<ScreeningResponse> ScreenAsync(byte[] image, CancellationToken cancellationToken)
    {
        if (!options.Configured) return ScreeningResponse.Failed("not-configured");
        try
        {
            using (var ready = await http.GetAsync(new Uri(options.BaseUrl!, "ready"), cancellationToken))
                if (!ready.IsSuccessStatusCode) return ScreeningResponse.Failed("not-ready");
            using var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            form.Add(file, "file", "photo.jpg");
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUrl!, "v1/scan")) { Content = form };
            if (!string.IsNullOrWhiteSpace(options.ApiKey)) request.Headers.Authorization = new("Bearer", options.ApiKey);
            using var response = await http.SendAsync(request, cancellationToken);
            var status = (int)response.StatusCode;
            // 200 carries completed or partial scans; 502 may carry an all-failed scan envelope (results-reference §2).
            if (status is not (200 or 502))
            {
                log.LogWarning("Screening returned {Status}.", status);
                return ScreeningResponse.Failed($"http-{status}");
            }
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return Parse(body);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            log.LogWarning("Screening unreachable: {Type}.", exception.GetType().Name);
            return ScreeningResponse.Failed("unreachable");
        }
    }

    /// <summary>Parses a scan envelope; anything that isn't one is "not-understood".</summary>
    public static ScreeningResponse Parse(string body)
    {
        JsonDocument document;
        try { document = JsonDocument.Parse(body); }
        catch (JsonException) { return ScreeningResponse.Failed("not-understood"); }
        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("status", out var overall) || overall.ValueKind != JsonValueKind.String
                || overall.GetString() is not ("completed" or "partial" or "failed"))
                return ScreeningResponse.Failed("not-understood");
            var scanId = root.TryGetProperty("scan_id", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString() : null;

            var (nudity, detections) = (EngineState.Unknown, new List<NudityDetection>());
            if (Engine(results, "nudenet", out var nudenet) is var nudityState)
            {
                nudity = nudityState;
                if (nudity == EngineState.Completed)
                {
                    if (!nudenet.TryGetProperty("detections", out var list) || list.ValueKind != JsonValueKind.Array) nudity = EngineState.Unknown;
                    else
                        foreach (var detection in list.EnumerateArray())
                        {
                            if (detection.ValueKind != JsonValueKind.Object || !detection.TryGetProperty("class", out var label) || label.ValueKind != JsonValueKind.String
                                || !detection.TryGetProperty("score", out var score) || score.ValueKind != JsonValueKind.Number)
                            { nudity = EngineState.Unknown; break; }
                            detections.Add(new NudityDetection(label.GetString()!, score.GetDouble()));
                        }
                }
            }

            var nsfw = Engine(results, "nsfw", out var nsfwResult);
            bool? isNsfw = null;
            if (nsfw == EngineState.Completed)
            {
                if (nsfwResult.TryGetProperty("is_nsfw", out var flag) && flag.ValueKind is JsonValueKind.True or JsonValueKind.False) isNsfw = flag.GetBoolean();
                else nsfw = EngineState.Unknown;
            }

            var known = Engine(results, "arachnid", out var arachnid);
            string? classification = null, matchType = null;
            if (known == EngineState.Completed)
            {
                if (arachnid.TryGetProperty("classification", out var c) && c.ValueKind == JsonValueKind.String) classification = c.GetString();
                else known = EngineState.Unknown;
                if (arachnid.TryGetProperty("match_type", out var m) && m.ValueKind == JsonValueKind.String) matchType = m.GetString();
            }
            return new ScreeningResponse(null, nudity, detections, nsfw, isNsfw, known, classification, matchType, scanId);
        }
    }

    private static EngineState Engine(JsonElement results, string name, out JsonElement engine)
    {
        engine = default;
        if (!results.TryGetProperty(name, out engine) || engine.ValueKind != JsonValueKind.Object
            || !engine.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.String) return EngineState.Unknown;
        return status.GetString() switch
        {
            "completed" => EngineState.Completed,
            "failed" => EngineState.Failed,
            "disabled" => EngineState.Disabled,
            "not_requested" => EngineState.NotRequested,
            _ => EngineState.Unknown,
        };
    }
}

/// <summary>
/// The labelled Development-only override (`Media:Screening:Simulate=uncertain|blocked`): every upload gets that outcome
/// without calling anything. The host refuses to start with it outside Development. It never passes a photo.
/// </summary>
public sealed class SimulatedScreening(ScreeningVerdict verdict) : IMediaScreening
{
    public string Name => "simulated";

    public Task<ScreeningResponse> ScreenAsync(byte[] image, CancellationToken cancellationToken) => Task.FromResult(verdict switch
    {
        ScreeningVerdict.Blocked => new ScreeningResponse(null, EngineState.Completed, [], EngineState.Completed, false, EngineState.Completed, "csam", "exact", "simulated"),
        _ => new ScreeningResponse(null, EngineState.Completed, [new NudityDetection("BUTTOCKS_EXPOSED", 0.9)], EngineState.Completed, true,
            EngineState.Completed, "no-known-match", null, "simulated"),
    });
}
