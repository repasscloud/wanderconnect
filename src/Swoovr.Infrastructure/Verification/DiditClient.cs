using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Verification;

/// <summary>Didit settings (D147): from configuration only (locally .local/docker.env through the host's environment).</summary>
public sealed record DiditOptions(string? ApiKey, string? WorkflowId, string? WebhookSecret, Uri BaseUrl)
{
    public bool Configured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(WorkflowId);
}

/// <summary>
/// Didit's Sessions API v3 (docs.didit.me/sessions-api, read 25 October 2026): POST /v3/session/ creates a hosted
/// session (response field `url`); GET /v3/session/{id}/decision/ reads its decision. Both authenticate with the
/// x-api-key header. Only the status, vendor_data, workflow id and an adult yes/no are taken from a decision; document
/// fields, images and biometric scores are never read into Swoovr records (D128).
/// </summary>
public sealed class DiditClient(HttpClient http, DiditOptions options, ILogger<DiditClient> log) : IIdVerificationProvider
{
    public bool Configured => options.Configured;
    public string WorkflowId => options.WorkflowId ?? "";

    public async Task<ProviderSession?> CreateSessionAsync(Guid accountId, string callbackUrl, CancellationToken cancellationToken)
    {
        if (!Configured) return null;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUrl, "v3/session/"))
        {
            Content = JsonContent.Create(new { workflow_id = options.WorkflowId, vendor_data = accountId.ToString(), callback = callbackUrl }),
        };
        return await SendAsync(request, "create", cancellationToken);
    }

    public async Task<ProviderSession?> GetDecisionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!Configured || !Guid.TryParse(sessionId, out var id)) return null;
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.BaseUrl, $"v3/session/{id}/decision/"));
        return await SendAsync(request, "decision", cancellationToken);
    }

    /// <summary>
    /// DELETE /v3/session/{id}/delete/ (docs.didit.me/sessions-api/delete-session, read 27 October 2026; the key needs
    /// `delete:sessions`). 2xx means deleted; 404 means Didit no longer has it, which is the same outcome.
    /// </summary>
    public async Task<bool> DeleteSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (!Configured) return false;
        if (!Guid.TryParse(sessionId, out var id)) return true;
        using var request = new HttpRequestMessage(HttpMethod.Delete, new Uri(options.BaseUrl, $"v3/session/{id}/delete/"));
        request.Headers.Add("x-api-key", options.ApiKey);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound) return true;
            log.LogWarning("Didit delete returned {Status}.", (int)response.StatusCode);
            return false;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning("Didit delete failed: {Kind}.", exception.GetType().Name);
            return false;
        }
    }

    private async Task<ProviderSession?> SendAsync(HttpRequestMessage request, string what, CancellationToken cancellationToken)
    {
        request.Headers.Add("x-api-key", options.ApiKey);
        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                // Status only: Didit's error bodies can echo request data.
                log.LogWarning("Didit {What} returned {Status}.", what, (int)response.StatusCode);
                return null;
            }
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return Read(body.RootElement);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            log.LogWarning("Didit {What} failed: {Kind}.", what, exception.GetType().Name);
            return null;
        }
    }

    /// <summary>The fields Swoovr keeps from a session, decision or webhook body (the same shape in all three).</summary>
    public static ProviderSession? Read(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || Text(root, "session_id") is not { } session) return null;
        var decision = root.TryGetProperty("decision", out var d) ? d : root;
        return new(session, Text(root, "status") ?? "", Text(root, "vendor_data"), Text(root, "workflow_id"), Adult(decision), Text(root, "url"), DateOfBirth(decision));
    }

    /// <summary>
    /// The date of birth on the first approved document check, only so the caller can compare it with the profile's
    /// (D158). The caller never stores it.
    /// </summary>
    public static DateOnly? DateOfBirth(JsonElement decision)
    {
        if (decision.ValueKind != JsonValueKind.Object || !decision.TryGetProperty("id_verifications", out var checks) || checks.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var check in checks.EnumerateArray())
            if (string.Equals(Text(check, "status"), "Approved", StringComparison.OrdinalIgnoreCase) && Text(check, "date_of_birth") is { } dob
                && DateOnly.TryParseExact(dob, "yyyy-MM-dd", out var born))
                return born;
        return null;
    }

    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>
    /// Adult or not from an approved document check: Didit's `age`, or failing that the date of birth (used here and
    /// dropped). Null when no approved document check reports either. The date itself is never returned.
    /// </summary>
    public static bool? Adult(JsonElement decision)
    {
        if (decision.ValueKind != JsonValueKind.Object || !decision.TryGetProperty("id_verifications", out var checks) || checks.ValueKind != JsonValueKind.Array)
            return null;
        bool? adult = null;
        foreach (var check in checks.EnumerateArray())
        {
            if (!string.Equals(Text(check, "status"), "Approved", StringComparison.OrdinalIgnoreCase)) continue;
            int? age = check.TryGetProperty("age", out var a) && a.ValueKind == JsonValueKind.Number && a.TryGetInt32(out var years) ? years : null;
            if (age is null && Text(check, "date_of_birth") is { } dob && DateOnly.TryParse(dob, out var born))
                age = AgePolicy.YearsOn(born, DateOnly.FromDateTime(DateTime.UtcNow));
            if (age is null) continue;
            // Any approved document showing under 18 decides it (the safer answer).
            if (age < AgePolicy.Adult) return false;
            adult = true;
        }
        return adult;
    }
}
