using System.Net.Http.Json;
using System.Text.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>
/// The protected admin API routes (D136), used server-side by Swoovr.Admin with the signed-in staff member's token.
/// Never given to the member WebAssembly client or the member BFF. Every rule is the API's; this only carries requests.
/// </summary>
public sealed class AdminClient(HttpClient http)
{
    public Task<ApiResult<List<CaseSummaryView>>> CasesAsync(string? queue = null, string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<CaseSummaryView>>(HttpMethod.Get, $"api/v1/admin/cases?queue={Uri.EscapeDataString(queue ?? "")}&status={Uri.EscapeDataString(status ?? "")}", null, cancellationToken);

    public Task<ApiResult<CaseDetailView>> CaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<CaseDetailView>(HttpMethod.Get, $"api/v1/admin/cases/{id}", null, cancellationToken);

    /// <summary>"review", "acknowledge" or "close".</summary>
    public Task<ApiResult<CaseDetailView>> CaseStepAsync(Guid id, string step, CancellationToken cancellationToken = default) =>
        SendAsync<CaseDetailView>(HttpMethod.Post, $"api/v1/admin/cases/{id}/{Uri.EscapeDataString(step)}", null, cancellationToken);

    public Task<ApiResult<CaseDetailView>> ActAsync(Guid caseId, ModerationActionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CaseDetailView>(HttpMethod.Post, $"api/v1/admin/cases/{caseId}/actions", request, cancellationToken);

    public Task<ApiResult<AccountAdminSummary>> SanctionAccountAsync(Guid accountId, ModerationActionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Post, $"api/v1/admin/accounts/{accountId}/sanctions", request, cancellationToken);

    public Task<ApiResult<SanctionView>> LiftAsync(Guid sanctionId, string? reason, CancellationToken cancellationToken = default) =>
        SendAsync<SanctionView>(HttpMethod.Post, $"api/v1/admin/sanctions/{sanctionId}/lift", new LiftRequest(reason), cancellationToken);

    public Task<ApiResult<List<AppealAdminView>>> AppealsAsync(string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<AppealAdminView>>(HttpMethod.Get, $"api/v1/admin/appeals?status={Uri.EscapeDataString(status ?? "")}", null, cancellationToken);

    public Task<ApiResult<AppealView>> DecideAppealAsync(Guid id, string outcome, CancellationToken cancellationToken = default) =>
        SendAsync<AppealView>(HttpMethod.Post, $"api/v1/admin/appeals/{id}/decide", new AppealDecision(outcome), cancellationToken);

    public Task<ApiResult<List<AccountView>>> FindAccountsAsync(string email, CancellationToken cancellationToken = default) =>
        SendAsync<List<AccountView>>(HttpMethod.Get, "api/v1/admin/accounts?email=" + Uri.EscapeDataString(email), null, cancellationToken);

    public Task<ApiResult<AccountAdminSummary>> AccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Get, $"api/v1/admin/accounts/{id}/summary", null, cancellationToken);

    public Task<ApiResult<AdminVerificationView>> VerificationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<AdminVerificationView>(HttpMethod.Get, $"api/v1/admin/accounts/{id}/verification", null, cancellationToken);

    /// <summary>"allow-again" or "revoke" (D129).</summary>
    public Task<ApiResult<AdminVerificationView>> VerificationActionAsync(Guid id, string action, CancellationToken cancellationToken = default) =>
        SendAsync<AdminVerificationView>(HttpMethod.Post, $"api/v1/admin/accounts/{id}/verification/{Uri.EscapeDataString(action)}", null, cancellationToken);

    public Task<ApiResult<AdminVerificationView>> AgeAssuranceAsync(Guid id, bool needsVerification, CancellationToken cancellationToken = default) =>
        SendAsync<AdminVerificationView>(HttpMethod.Put, $"api/v1/admin/accounts/{id}/age-assurance", new AgeAssuranceRequest(needsVerification), cancellationToken);

    public Task<int> AdultOverrideAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Post, $"api/v1/admin/accounts/{id}/adult-override", null, cancellationToken);

    public Task<ApiResult<AccountAdminSummary>> HomeMoveOverrideAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Post, $"api/v1/admin/accounts/{id}/home-move-override", new HomeMoveOverride(reason), cancellationToken);

    public Task<ApiResult<EntitlementView>> GrantAsync(Guid id, EntitlementGrant grant, CancellationToken cancellationToken = default) =>
        SendAsync<EntitlementView>(HttpMethod.Post, $"api/v1/admin/accounts/{id}/entitlements", grant, cancellationToken);

    public Task<ApiResult<EntitlementView>> ChangeGrantAsync(Guid grantId, EntitlementChange change, CancellationToken cancellationToken = default) =>
        SendAsync<EntitlementView>(HttpMethod.Put, $"api/v1/admin/entitlements/{grantId}", change, cancellationToken);

    public Task<ApiResult<EntitlementView>> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default) =>
        SendAsync<EntitlementView>(HttpMethod.Post, $"api/v1/admin/entitlements/{grantId}/revoke", null, cancellationToken);

    public Task<int> StaffAffiliationAsync(Guid id, bool active, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Put, $"api/v1/admin/accounts/{id}/staff-affiliation", new StaffAffiliationRequest(active), cancellationToken);

    public Task<int> StaffLinkAsync(Guid staffId, Guid? memberId, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Put, $"api/v1/admin/staff-links/{staffId}", new StaffLinkRequest(memberId), cancellationToken);

    /// <summary>A member's trips as support sees them (D149 rule 7): market, dates and arrival, never the city.</summary>
    public Task<ApiResult<List<TripAdminView>>> TripsAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<List<TripAdminView>>(HttpMethod.Get, $"api/v1/admin/accounts/{id}/trips", null, cancellationToken);

    /// <summary>Support confirms an arrival after a review, with a reason (D149 rule 7).</summary>
    public Task<int> ConfirmArrivalAsync(Guid tripId, string reason, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Post, $"api/v1/admin/trips/{tripId}/arrival", new ArrivalReview(reason), cancellationToken);

    /// <summary>An admin clears the home-location review after looking (D150).</summary>
    public Task<ApiResult<AccountAdminSummary>> ClearHomeReviewAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Post, $"api/v1/admin/accounts/{id}/home-review/clear", new HomeReviewClear(reason), cancellationToken);

    /// <summary>D166: open date-of-birth reviews (admins only).</summary>
    public Task<ApiResult<List<DobReviewView>>> DobReviewsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<DobReviewView>>(HttpMethod.Get, "api/v1/admin/dob-reviews", null, cancellationToken);

    public Task<ApiResult<DobReviewView>> ResolveDobReviewAsync(Guid id, string outcome, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<DobReviewView>(HttpMethod.Post, $"api/v1/admin/dob-reviews/{id}/resolve", new DobReviewDecision(outcome, reason), cancellationToken);

    /// <summary>D191: opens one verified identity record (`legal` only) with a recorded purpose.</summary>
    public Task<ApiResult<VerifiedIdentityView>> IdentityAsync(Guid accountId, string? purpose, CancellationToken cancellationToken = default) =>
        SendAsync<VerifiedIdentityView>(HttpMethod.Post, $"api/v1/admin/accounts/{accountId}/identity", new IdentityAccessRequest(purpose), cancellationToken);

    /// <summary>D187, #87: who held a number or address and when (admins only), never the values.</summary>
    public Task<ApiResult<ContactHistoryResult>> ContactHistoryAsync(string type, string value, CancellationToken cancellationToken = default) =>
        SendAsync<ContactHistoryResult>(HttpMethod.Get, $"api/v1/admin/contact-history?type={Uri.EscapeDataString(type)}&value={Uri.EscapeDataString(value)}", null, cancellationToken);

    // Legal holds and evidence (D189): `legal` only, except the overview and an admin's provisional hold from a case.
    public Task<ApiResult<HoldOverview>> HoldOverviewAsync(CancellationToken cancellationToken = default) =>
        SendAsync<HoldOverview>(HttpMethod.Get, "api/v1/admin/holds/overview", null, cancellationToken);

    public Task<ApiResult<List<LegalHoldSummary>>> HoldsAsync(string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<LegalHoldSummary>>(HttpMethod.Get, "api/v1/admin/holds?status=" + Uri.EscapeDataString(status ?? ""), null, cancellationToken);

    public Task<ApiResult<LegalHoldSummary>> PlaceHoldAsync(NewLegalHold hold, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldSummary>(HttpMethod.Post, "api/v1/admin/holds", hold, cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> HoldAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Get, $"api/v1/admin/holds/{id}", null, cancellationToken);

    /// <summary>"confirm" or "capture".</summary>
    public Task<ApiResult<LegalHoldDetail>> HoldStepAsync(Guid id, string step, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/v1/admin/holds/{id}/{Uri.EscapeDataString(step)}", null, cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> ReviewHoldAsync(Guid id, DateTimeOffset? due, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/v1/admin/holds/{id}/review", new HoldReviewRequest(due), cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> ReleaseHoldAsync(Guid id, string? reason, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/v1/admin/holds/{id}/release", new HoldReleaseRequest(reason), cancellationToken);

    public Task<ApiResult<EvidenceExportLink>> CreateExportAsync(Guid id, EvidenceExportRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<EvidenceExportLink>(HttpMethod.Post, $"api/v1/admin/holds/{id}/exports", request, cancellationToken);

    public Task<ApiResult<List<DisclosureView>>> DisclosuresAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<DisclosureView>>(HttpMethod.Get, "api/v1/admin/disclosures", null, cancellationToken);

    /// <summary>The one-time evidence package (JSON bytes), or null with the API's status.</summary>
    public async Task<(int Status, byte[]? Body)> DownloadExportAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/v1/admin/evidence-exports/" + Uri.EscapeDataString(token), cancellationToken);
        return ((int)response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null);
    }

    private async Task<int> StatusAsync(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body) };
        using var response = await http.SendAsync(request, cancellationToken);
        return (int)response.StatusCode;
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path) { Content = body is null ? null : JsonContent.Create(body, body.GetType()) };
        using var response = await http.SendAsync(request, cancellationToken);
        var status = (int)response.StatusCode;
        if (response.IsSuccessStatusCode)
            return new(status, await response.Content.ReadFromJsonAsync<T>(cancellationToken) ?? throw new InvalidOperationException("API returned an empty body."), null);
        string? code = null;
        if (response.Content.Headers.ContentType?.MediaType is "application/problem+json" or "application/json")
            try
            {
                using var problem = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
                code = problem.RootElement.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() : null;
            }
            catch (JsonException) { }
        return new(status, default, code);
    }
}
