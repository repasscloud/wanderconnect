using System.Net.Http.Json;
using System.Text.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>A response, or the HTTP status and API problem code that refused it.</summary>
public sealed record ApiResult<T>(int Status, T? Value, string? Code)
{
    public bool Ok => Value is not null && Status is >= 200 and < 300;
}

/// <summary>
/// The signed-in member's own account routes (relative paths). The browser client gives it an
/// HttpClient rooted at the web host's backend-for-frontend (/bff/), which holds the tokens
/// server-side; a native client would root it at the API with its own bearer handler.
/// </summary>
public sealed class MemberClient(HttpClient http)
{
    public Task<ApiResult<SessionSummary>> SessionAsync(CancellationToken cancellationToken = default) =>
        SendAsync<SessionSummary>(new HttpRequestMessage(HttpMethod.Get, "api/v1/session"), cancellationToken);

    public Task<ApiResult<MemberAccount>> MeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me"), cancellationToken);

    public Task<ApiResult<MemberAccount>> SaveAgeAsync(AgeUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Put, "api/v1/me/age") { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<ApiResult<MemberAccount>> RedeemAsync(string code, CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Post, "api/v1/me/coupons") { Content = JsonContent.Create(new CouponRequest(code)) }, cancellationToken);

    /// <summary>The connection modes as configured (D92), in display order, with their intention chips.</summary>
    public Task<ApiResult<List<ConnectionModeView>>> ModesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ConnectionModeView>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/modes"), cancellationToken);

    public Task<ApiResult<MyProfileView>> ProfileAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MyProfileView>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me/profile"), cancellationToken);

    public Task<ApiResult<MyProfileView>> SaveProfileAsync(ProfileUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<MyProfileView>(new HttpRequestMessage(HttpMethod.Put, "api/v1/me/profile") { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<ApiResult<MyProfileView>> SaveModeAsync(string modeId, ModeUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<MyProfileView>(new HttpRequestMessage(HttpMethod.Put, "api/v1/me/modes/" + Uri.EscapeDataString(modeId)) { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<ApiResult<ExploreResult>> ExploreAsync(string modeId, CancellationToken cancellationToken = default) =>
        SendAsync<ExploreResult>(new HttpRequestMessage(HttpMethod.Get, "api/v1/explore?mode=" + Uri.EscapeDataString(modeId)), cancellationToken);

    public Task<ApiResult<PersonProfile>> PersonAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<PersonProfile>(new HttpRequestMessage(HttpMethod.Get, "api/v1/profiles/" + id), cancellationToken);

    private async Task<ApiResult<T>> SendAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await http.SendAsync(request, cancellationToken))
        {
            var status = (int)response.StatusCode;
            if (response.IsSuccessStatusCode)
                return new(status, await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                    ?? throw new InvalidOperationException("API returned an empty body."), null);
            return new(status, default, await ProblemCodeAsync(response, cancellationToken));
        }
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentType?.MediaType is not ("application/problem+json" or "application/json")) return null;
        try
        {
            using var body = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return body.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String ? code.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
}
