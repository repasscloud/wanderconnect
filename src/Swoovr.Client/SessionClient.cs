using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>
/// Reads the caller's session summary. The bearer token comes from the host's
/// server-side session store; this client never stores or logs it.
/// </summary>
public sealed class SessionClient(HttpClient http)
{
    public async Task<SessionSummary?> GetAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/v1/session");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SessionSummary>(cancellationToken)
            ?? throw new InvalidOperationException("API returned an empty session.");
    }
}
