using System.Net.Http.Json;
using System.Text.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>
/// The protected admin API routes (D136), used server-side by Swoovr.Admin with the signed-in staff member's token.
/// Never given to the member WebAssembly client or the member BFF. Every rule is the API's; this only carries requests.
/// </summary>
public sealed class AdminClient(HttpClient transport)
{
    private readonly HttpClient http = ApiContractClient.Configure(transport);

    public async Task<ApiResult<BannedIdentifierPage>> RegistryAsync(Guid? caseId = null,bool due = true,Guid? after = null,CancellationToken cancellationToken = default)
    {
        var path = "api/admin/banned-identifiers?due="+(due ? "true" : "false")
            +(caseId is {} review ? "&caseId="+review : "")+(after is {} cursor ? "&after="+cursor : "");
        using var response = await http.GetAsync(path,cancellationToken);
        var items = await ReadAsync<List<BannedIdentifierView>>(response,cancellationToken);
        if (!items.Ok) return new(items.Status,null,items.Code);
        Guid? next = null;
        if (response.Headers.TryGetValues("Swoovr-Next-Cursor",out var values))
        {
            var headers = values.ToArray();
            if (headers.Length != 1 || !Guid.TryParse(headers[0],out var selected)) throw new InvalidOperationException("API returned an invalid registry continuation.");
            next = selected;
        }
        return new(items.Status,new BannedIdentifierPage(items.Value!,next),null);
    }
    public Task<ApiResult<List<BannedIdentifierView>>> RegistryLookupAsync(IdentifierLookup request,CancellationToken cancellationToken = default) =>
        SendAsync<List<BannedIdentifierView>>(HttpMethod.Post,"api/admin/banned-identifiers/lookup",request,cancellationToken);
    public Task<ApiResult<BannedIdentifierDetail>> RegistryEntryAsync(Guid id,CancellationToken cancellationToken = default) =>
        SendAsync<BannedIdentifierDetail>(HttpMethod.Get,$"api/admin/banned-identifiers/{id}",null,cancellationToken);
    public Task<ApiResult<BannedIdentifierView>> RegistryReviewAsync(Guid id,string reason,CancellationToken cancellationToken = default) =>
        SendAsync<BannedIdentifierView>(HttpMethod.Post,$"api/admin/banned-identifiers/{id}/review",new IdentifierReviewRequest(reason),cancellationToken);
    public Task<ApiResult<IdentifierWorkflowResult>> RegistryReleaseAsync(IdentifierReleaseRequest request,CancellationToken cancellationToken = default) =>
        SendAsync<IdentifierWorkflowResult>(HttpMethod.Post,"api/admin/banned-identifiers/release",request,cancellationToken);
    public Task<ApiResult<IdentifierWorkflowResult>> RetireMobileBindingAsync(Guid id,MobileBindingRetirementRequest request,CancellationToken cancellationToken = default) =>
        SendAsync<IdentifierWorkflowResult>(HttpMethod.Post,$"api/admin/banned-identifiers/{id}/retire-mobile",request,cancellationToken);
    public Task<ApiResult<EmailRetirementAvailability>> EmailRetirementAvailabilityAsync(CancellationToken cancellationToken = default) =>
        SendAsync<EmailRetirementAvailability>(HttpMethod.Get,"api/admin/banned-identifiers/email-retirement-availability",null,cancellationToken);
    public Task<ApiResult<EmailBindingRetirementView>> EmailRetirementAsync(Guid id,CancellationToken cancellationToken = default) =>
        RetirementAsync(HttpMethod.Get,$"api/admin/banned-identifiers/{id}/email-retirement",null,cancellationToken);
    public Task<ApiResult<EmailBindingRetirementView>> RetireEmailBindingAsync(Guid id,EmailBindingRetirementRequest request,CancellationToken cancellationToken = default) =>
        RetirementAsync(HttpMethod.Post,$"api/admin/banned-identifiers/{id}/retire-email",request,cancellationToken);
    public Task<ApiResult<EmailBindingRetirementView>> ReviewEmailRetirementAsync(Guid id,EmailBindingRetirementRequest request,CancellationToken cancellationToken = default) =>
        RetirementAsync(HttpMethod.Post,$"api/admin/banned-identifiers/{id}/email-retirement/review",request,cancellationToken);
    private async Task<ApiResult<EmailBindingRetirementView>> RetirementAsync(HttpMethod method,string path,object? body,CancellationToken token)
    {
        var result=await SendAsync<EmailBindingRetirementView>(method,path,body,token);
        if(result.Ok)
        {
            var view=result.Value!;
            if(view.OperationId==Guid.Empty||view.RequestedAt==default||view.UpdatedAt<view.RequestedAt
                ||view.State is not("Pending" or "NeedsReview" or "Completed")||(view.State=="Completed")!=(view.CompletedAt is not null)
                ||view.CompletedAt is{}completed&&completed<view.RequestedAt)
                throw new InvalidOperationException("Invalid retirement metadata.");
        }
        return result;
    }
    public Task<ApiResult<AccountReinstatementPreview>> ReinstatementAsync(Guid id,CancellationToken cancellationToken = default) =>
        SendAsync<AccountReinstatementPreview>(HttpMethod.Get,$"api/admin/accounts/{id}/reinstatement",null,cancellationToken);
    public Task<ApiResult<IdentifierWorkflowResult>> ReinstateAsync(Guid id,AccountReinstatementRequest request,CancellationToken cancellationToken = default) =>
        SendAsync<IdentifierWorkflowResult>(HttpMethod.Post,$"api/admin/accounts/{id}/reinstate",request,cancellationToken);
    public Task<ApiResult<List<CaseSummaryView>>> CasesAsync(string? queue = null, string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<CaseSummaryView>>(HttpMethod.Get, $"api/admin/cases?queue={Uri.EscapeDataString(queue ?? "")}&status={Uri.EscapeDataString(status ?? "")}", null, cancellationToken);

    public Task<ApiResult<CaseDetailView>> CaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<CaseDetailView>(HttpMethod.Get, $"api/admin/cases/{id}", null, cancellationToken);

    public Task<ApiResult<List<ContentRemovalSource>>> ContentSourcesAsync(Guid caseId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ContentRemovalSource>>(HttpMethod.Get, $"api/admin/cases/{caseId}/content-sources", null, cancellationToken);

    public Task<ApiResult<ContentRemovalReceipt>> RemoveContentAsync(Guid caseId, ContentRemovalRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<ContentRemovalReceipt>(HttpMethod.Post, $"api/admin/cases/{caseId}/content-removals", request, cancellationToken);

    public Task<ApiResult<ContentRemovalEvidence>> ContentEvidenceAsync(Guid caseId, Guid removalId, CancellationToken cancellationToken = default) =>
        SendAsync<ContentRemovalEvidence>(HttpMethod.Get, $"api/admin/cases/{caseId}/content-removals/{removalId}", null, cancellationToken);

    /// <summary>"review", "acknowledge" or "close".</summary>
    public Task<ApiResult<CaseDetailView>> CaseStepAsync(Guid id, string step, CancellationToken cancellationToken = default) =>
        SendAsync<CaseDetailView>(HttpMethod.Post, $"api/admin/cases/{id}/{Uri.EscapeDataString(step)}", null, cancellationToken);

    public Task<ApiResult<CaseDetailView>> ActAsync(Guid caseId, ModerationActionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<CaseDetailView>(HttpMethod.Post, $"api/admin/cases/{caseId}/actions", request, cancellationToken);

    public Task<ApiResult<AccountAdminSummary>> SanctionAccountAsync(Guid accountId, ModerationActionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Post, $"api/admin/accounts/{accountId}/sanctions", request, cancellationToken);

    public Task<ApiResult<SanctionView>> LiftAsync(Guid sanctionId, string? reason, CancellationToken cancellationToken = default) =>
        SendAsync<SanctionView>(HttpMethod.Post, $"api/admin/sanctions/{sanctionId}/lift", new LiftRequest(reason), cancellationToken);

    public Task<ApiResult<List<AppealAdminView>>> AppealsAsync(string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<AppealAdminView>>(HttpMethod.Get, $"api/admin/appeals?status={Uri.EscapeDataString(status ?? "")}", null, cancellationToken);

    public Task<ApiResult<AppealView>> DecideAppealAsync(Guid id, string outcome, CancellationToken cancellationToken = default) =>
        SendAsync<AppealView>(HttpMethod.Post, $"api/admin/appeals/{id}/decide", new AppealDecision(outcome), cancellationToken);

    public Task<ApiResult<List<AccountView>>> FindAccountsAsync(string email, CancellationToken cancellationToken = default) =>
        SendAsync<List<AccountView>>(HttpMethod.Get, "api/admin/accounts?email=" + Uri.EscapeDataString(email), null, cancellationToken);

    public Task<ApiResult<AccountAdminSummary>> AccountAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Get, $"api/admin/accounts/{id}/summary", null, cancellationToken);

    public Task<ApiResult<AdminVerificationView>> VerificationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<AdminVerificationView>(HttpMethod.Get, $"api/admin/accounts/{id}/verification", null, cancellationToken);

    /// <summary>"allow-again" or "revoke" (D129).</summary>
    public Task<ApiResult<AdminVerificationView>> VerificationActionAsync(Guid id, string action, CancellationToken cancellationToken = default) =>
        SendAsync<AdminVerificationView>(HttpMethod.Post, $"api/admin/accounts/{id}/verification/{Uri.EscapeDataString(action)}", null, cancellationToken);

    public Task<ApiResult<AdminVerificationView>> AgeAssuranceAsync(Guid id, bool needsVerification, CancellationToken cancellationToken = default) =>
        SendAsync<AdminVerificationView>(HttpMethod.Put, $"api/admin/accounts/{id}/age-assurance", new AgeAssuranceRequest(needsVerification), cancellationToken);

    public Task<int> AdultOverrideAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Post, $"api/admin/accounts/{id}/adult-override", null, cancellationToken);

    public Task<ApiResult<AccountAdminSummary>> HomeMoveOverrideAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Post, $"api/admin/accounts/{id}/home-move-override", new HomeMoveOverride(reason), cancellationToken);

    public Task<ApiResult<EntitlementView>> GrantAsync(Guid id, EntitlementGrant grant, CancellationToken cancellationToken = default) =>
        SendAsync<EntitlementView>(HttpMethod.Post, $"api/admin/accounts/{id}/entitlements", grant, cancellationToken);

    public Task<ApiResult<EntitlementView>> ChangeGrantAsync(Guid grantId, EntitlementChange change, CancellationToken cancellationToken = default) =>
        SendAsync<EntitlementView>(HttpMethod.Put, $"api/admin/entitlements/{grantId}", change, cancellationToken);

    public Task<ApiResult<EntitlementView>> RevokeGrantAsync(Guid grantId, CancellationToken cancellationToken = default) =>
        SendAsync<EntitlementView>(HttpMethod.Post, $"api/admin/entitlements/{grantId}/revoke", null, cancellationToken);

    public Task<int> StaffAffiliationAsync(Guid id, bool active, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Put, $"api/admin/accounts/{id}/staff-affiliation", new StaffAffiliationRequest(active), cancellationToken);

    public Task<int> StaffLinkAsync(Guid staffId, Guid? memberId, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Put, $"api/admin/staff-links/{staffId}", new StaffLinkRequest(memberId), cancellationToken);

    /// <summary>A member's trips as support sees them (D149 rule 7): market, dates and arrival, never the city.</summary>
    public Task<ApiResult<List<TripAdminView>>> TripsAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<List<TripAdminView>>(HttpMethod.Get, $"api/admin/accounts/{id}/trips", null, cancellationToken);

    /// <summary>Support confirms an arrival after a review, with a reason (D149 rule 7).</summary>
    public Task<int> ConfirmArrivalAsync(Guid tripId, string reason, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Post, $"api/admin/trips/{tripId}/arrival", new ArrivalReview(reason), cancellationToken);

    /// <summary>An admin clears the home-location review after looking (D150).</summary>
    public Task<ApiResult<AccountAdminSummary>> ClearHomeReviewAsync(Guid id, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<AccountAdminSummary>(HttpMethod.Post, $"api/admin/accounts/{id}/home-review/clear", new HomeReviewClear(reason), cancellationToken);

    /// <summary>D166: open date-of-birth reviews (admins only).</summary>
    /// <summary>#50: the member's own account-standing view, for staff helping them.</summary>
    public Task<ApiResult<AccountStandingView>> StandingAsync(Guid accountId, CancellationToken cancellationToken = default) =>
        SendAsync<AccountStandingView>(HttpMethod.Get, $"api/admin/accounts/{accountId}/standing", null, cancellationToken);

    /// <summary>#50: a member's support reference (SW-XXXXXX) to its report and case.</summary>
    public Task<ApiResult<SupportLookupView>> SupportLookupAsync(string reference, CancellationToken cancellationToken = default) =>
        SendAsync<SupportLookupView>(HttpMethod.Get, $"api/admin/support/{Uri.EscapeDataString(reference)}", null, cancellationToken);

    public Task<ApiResult<List<DobReviewView>>> DobReviewsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<DobReviewView>>(HttpMethod.Get, "api/admin/dob-reviews", null, cancellationToken);

    public Task<ApiResult<DobReviewView>> ResolveDobReviewAsync(Guid id, string outcome, string reason, CancellationToken cancellationToken = default) =>
        SendAsync<DobReviewView>(HttpMethod.Post, $"api/admin/dob-reviews/{id}/resolve", new DobReviewDecision(outcome, reason), cancellationToken);

    /// <summary>D191: opens one verified identity record (`legal` only) with a recorded purpose.</summary>
    public Task<ApiResult<VerifiedIdentityView>> IdentityAsync(Guid accountId, string? purpose, CancellationToken cancellationToken = default) =>
        SendAsync<VerifiedIdentityView>(HttpMethod.Post, $"api/admin/accounts/{accountId}/identity", new IdentityAccessRequest(purpose), cancellationToken);

    /// <summary>D187, #87: who held a number or address and when (admins only), never the values.</summary>
    public Task<ApiResult<ContactHistoryResult>> ContactHistoryAsync(string type, string value, CancellationToken cancellationToken = default) =>
        SendAsync<ContactHistoryResult>(HttpMethod.Get, $"api/admin/contact-history?type={Uri.EscapeDataString(type)}&value={Uri.EscapeDataString(value)}", null, cancellationToken);

    // Legal holds and evidence (D189): `legal` only, except the overview and an admin's provisional hold from a case.
    public Task<ApiResult<HoldOverview>> HoldOverviewAsync(CancellationToken cancellationToken = default) =>
        SendAsync<HoldOverview>(HttpMethod.Get, "api/admin/holds/overview", null, cancellationToken);

    public Task<ApiResult<List<LegalHoldSummary>>> HoldsAsync(string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<LegalHoldSummary>>(HttpMethod.Get, "api/admin/holds?status=" + Uri.EscapeDataString(status ?? ""), null, cancellationToken);

    public Task<ApiResult<LegalHoldSummary>> PlaceHoldAsync(NewLegalHold hold, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldSummary>(HttpMethod.Post, "api/admin/holds", hold, cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> HoldAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Get, $"api/admin/holds/{id}", null, cancellationToken);

    /// <summary>"confirm" or "capture".</summary>
    public Task<ApiResult<LegalHoldDetail>> HoldStepAsync(Guid id, string step, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/admin/holds/{id}/{Uri.EscapeDataString(step)}", null, cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> RetentionDispositionAsync(Guid id, string? reason, bool confirmed, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/admin/holds/{id}/retention-disposition", new HoldRetentionDispositionRequest(reason, confirmed), cancellationToken);

    public Task<ApiResult<MediaReviewLink>> PreservedPhotoAsync(Guid hold, Guid media, string? purpose, CancellationToken cancellationToken = default) =>
        SendAsync<MediaReviewLink>(HttpMethod.Post, $"api/admin/holds/{hold}/media/{media}/link", new MediaViewRequest(purpose), cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> ReviewHoldAsync(Guid id, DateTimeOffset? due, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/admin/holds/{id}/review", new HoldReviewRequest(due), cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> ReleaseHoldAsync(Guid id, string? reason, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/admin/holds/{id}/release", new HoldReleaseRequest(reason), cancellationToken);

    public Task<ApiResult<EvidenceExportLink>> CreateExportAsync(Guid id, EvidenceExportRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<EvidenceExportLink>(HttpMethod.Post, $"api/admin/holds/{id}/exports", request, cancellationToken);

    public Task<ApiResult<List<DisclosureView>>> DisclosuresAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<DisclosureView>>(HttpMethod.Get, "api/admin/disclosures", null, cancellationToken);

    public Task<ApiResult<LegalHoldDetail>> ReleaseHoldAsync(Guid id, string? reason, bool overlapsChecked, CancellationToken cancellationToken = default) =>
        SendAsync<LegalHoldDetail>(HttpMethod.Post, $"api/admin/holds/{id}/release", new HoldReleaseRequest(reason, overlapsChecked), cancellationToken);

    // #88: staff access controls.
    public Task<ApiResult<List<AccessGrantView>>> GrantsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<AccessGrantView>>(HttpMethod.Get, "api/admin/access/grants", null, cancellationToken);

    public Task<ApiResult<AccessGrantView>> RequestGrantAsync(AccessGrantRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<AccessGrantView>(HttpMethod.Post, "api/admin/access/grants", request, cancellationToken);

    /// <summary>step: renew or revoke.</summary>
    public Task<ApiResult<AccessGrantView>> GrantStepAsync(Guid id, string step, CancellationToken cancellationToken = default) =>
        SendAsync<AccessGrantView>(HttpMethod.Post, $"api/admin/access/grants/{id}/{Uri.EscapeDataString(step)}", null, cancellationToken);

    public Task<ApiResult<SensitiveAccessLogView>> SensitiveLogAsync(CancellationToken cancellationToken = default) =>
        SendAsync<SensitiveAccessLogView>(HttpMethod.Get, "api/admin/access/log?limit=200", null, cancellationToken);

    public Task<ApiResult<List<StaffExclusionView>>> ExclusionsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<StaffExclusionView>>(HttpMethod.Get, "api/admin/access/exclusions", null, cancellationToken);

    public Task<int> ExcludeAsync(StaffExclusionRequest request, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Post, "api/admin/access/exclusions", request, cancellationToken);

    public Task<ApiResult<List<ExportApprovalView>>> ApprovalsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ExportApprovalView>>(HttpMethod.Get, "api/admin/access/approvals", null, cancellationToken);

    public Task<ApiResult<ExportApprovalView>> ApproveExportAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<ExportApprovalView>(HttpMethod.Post, $"api/admin/access/approvals/{id}/approve", null, cancellationToken);

    public Task<ApiResult<StaffRevokeResult>> RevokeStaffAsync(Guid id, string? reason, CancellationToken cancellationToken = default) =>
        SendAsync<StaffRevokeResult>(HttpMethod.Post, $"api/admin/staff/{id}/revoke", new StaffRevokeRequest(reason), cancellationToken);

    /// <summary>#91: the subject's profile as visible when the case's first report was made.</summary>
    public Task<ApiResult<ProfileAtReport>> ProfileAtReportAsync(Guid caseId, CancellationToken cancellationToken = default) =>
        SendAsync<ProfileAtReport>(HttpMethod.Get, $"api/admin/cases/{caseId}/profile-at-report", null, cancellationToken);

    // #89: security incidents.
    public Task<ApiResult<List<SecurityIncidentView>>> IncidentsAsync(string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<SecurityIncidentView>>(HttpMethod.Get, "api/admin/security/incidents?status=" + Uri.EscapeDataString(status ?? ""), null, cancellationToken);

    public Task<ApiResult<SecurityIncidentView>> OpenIncidentAsync(NewSecurityIncident request, CancellationToken cancellationToken = default) =>
        SendAsync<SecurityIncidentView>(HttpMethod.Post, "api/admin/security/incidents", request, cancellationToken);

    public Task<ApiResult<SecurityIncidentView>> AssessIncidentAsync(Guid id, IncidentAssessment request, CancellationToken cancellationToken = default) =>
        SendAsync<SecurityIncidentView>(HttpMethod.Post, $"api/admin/security/incidents/{id}/assess", request, cancellationToken);

    /// <summary>step: contain or close.</summary>
    public Task<ApiResult<SecurityIncidentView>> IncidentStepAsync(Guid id, string step, CancellationToken cancellationToken = default) =>
        SendAsync<SecurityIncidentView>(HttpMethod.Post, $"api/admin/security/incidents/{id}/{Uri.EscapeDataString(step)}", null, cancellationToken);

    // D195: law-enforcement case management (`legal` only).
    private const string Le = "api/admin/law-enforcement";

    public Task<ApiResult<LeOverview>> LeOverviewAsync(CancellationToken cancellationToken = default) =>
        SendAsync<LeOverview>(HttpMethod.Get, Le + "/overview", null, cancellationToken);

    public Task<ApiResult<List<LeCaseSummary>>> LeCasesAsync(string? status = null, CancellationToken cancellationToken = default) =>
        SendAsync<List<LeCaseSummary>>(HttpMethod.Get, Le + "/cases?status=" + Uri.EscapeDataString(status ?? ""), null, cancellationToken);

    public Task<ApiResult<LeCaseDetail>> OpenLeCaseAsync(NewLeCase request, CancellationToken cancellationToken = default) =>
        SendAsync<LeCaseDetail>(HttpMethod.Post, Le + "/cases", request, cancellationToken);

    public Task<ApiResult<LeCaseDetail>> LeCaseAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<LeCaseDetail>(HttpMethod.Get, $"{Le}/cases/{id}", null, cancellationToken);

    /// <summary>One step on a case: verify, assessment, app65, flags (PUT), responses, requests, people, owner, notifications, transfers, close.</summary>
    public Task<ApiResult<LeCaseDetail>> LeStepAsync(Guid id, string step, object body, CancellationToken cancellationToken = default) =>
        SendAsync<LeCaseDetail>(step == "flags" ? HttpMethod.Put : HttpMethod.Post, $"{Le}/cases/{id}/{step}", body, cancellationToken);

    public Task<ApiResult<TransparencyReport>> TransparencyAsync(int year, CancellationToken cancellationToken = default) =>
        SendAsync<TransparencyReport>(HttpMethod.Get, $"{Le}/transparency?year={year}", null, cancellationToken);

    /// <summary>The quarterly audit export (JSON bytes, no case content), or null with the API's status.</summary>
    public async Task<(int Status, byte[]? Body)> LeAuditExportAsync(string quarter, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync($"{Le}/audit-export?quarter=" + Uri.EscapeDataString(quarter), cancellationToken);
        return ((int)response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null);
    }

    /// <summary>#103: Safety &amp; Support packs (admins only).</summary>
    public Task<ApiResult<List<SafetyCountryAdminView>>> SafetyCountriesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<SafetyCountryAdminView>>(HttpMethod.Get, "api/admin/safety/countries", null, cancellationToken);

    public Task<ApiResult<SafetyCountryAdminView>> SaveSafetyCountryAsync(string code, SafetyCountryUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<SafetyCountryAdminView>(HttpMethod.Put, $"api/admin/safety/countries/{Uri.EscapeDataString(code)}", update, cancellationToken);

    public Task<ApiResult<List<SafetyResourceAdminView>>> SafetyResourcesAsync(string code, CancellationToken cancellationToken = default) =>
        SendAsync<List<SafetyResourceAdminView>>(HttpMethod.Get, $"api/admin/safety/countries/{Uri.EscapeDataString(code)}/resources", null, cancellationToken);

    public Task<ApiResult<SafetyResourceAdminView>> SaveSafetyResourceAsync(Guid? id, SafetyResourceEdit edit, CancellationToken cancellationToken = default) =>
        SendAsync<SafetyResourceAdminView>(id is null ? HttpMethod.Post : HttpMethod.Put, id is null ? "api/admin/safety/resources" : $"api/admin/safety/resources/{id}", edit, cancellationToken);

    public Task<ApiResult<List<SafetyRevisionView>>> SafetyRevisionsAsync(string? country, CancellationToken cancellationToken = default) =>
        SendAsync<List<SafetyRevisionView>>(HttpMethod.Get, "api/admin/safety/revisions?country=" + Uri.EscapeDataString(country ?? ""), null, cancellationToken);

    /// <summary>D203: uncertain photos waiting for review (admins only).</summary>
    public Task<ApiResult<MediaReviewQueue>> MediaReviewAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MediaReviewQueue>(HttpMethod.Get, "api/admin/media/review", null, cancellationToken);

    /// <summary>A short-lived review link, recorded with the purpose in the sensitive-access log.</summary>
    /// <summary>#41: a chat photo named in a case's evidence, with a recorded purpose (admins only).</summary>
    public Task<ApiResult<MediaReviewLink>> CaseMediaAsync(Guid id, Guid caseId, string? purpose, CancellationToken cancellationToken = default) =>
        SendAsync<MediaReviewLink>(HttpMethod.Post, $"api/admin/media/{id}/case-view", new MediaCaseViewRequest(caseId, purpose), cancellationToken);

    public Task<ApiResult<MediaReviewLink>> ViewMediaAsync(Guid id, string? purpose, CancellationToken cancellationToken = default) =>
        SendAsync<MediaReviewLink>(HttpMethod.Post, $"api/admin/media/{id}/view", new MediaViewRequest(purpose), cancellationToken);

    /// <summary>approve, reject or escalate; the status code only.</summary>
    public Task<int> DecideMediaAsync(Guid id, string decision, string? reason, CancellationToken cancellationToken = default) =>
        StatusAsync(HttpMethod.Post, $"api/admin/media/{id}/decision", new MediaDecision(decision, reason), cancellationToken);

    /// <summary>The image behind a review link, or null.</summary>
    public async Task<byte[]?> MediaAsync(string link, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync(link, cancellationToken);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null;
    }

    /// <summary>The one-time evidence package (JSON bytes), or null with the API's status.</summary>
    public async Task<(int Status, byte[]? Body)> DownloadExportAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await http.GetAsync("api/admin/evidence-exports/" + Uri.EscapeDataString(token), cancellationToken);
        return ((int)response.StatusCode, response.IsSuccessStatusCode ? await response.Content.ReadAsByteArrayAsync(cancellationToken) : null);
    }

    public Task<ApiResult<List<TextHarmCandidateSummary>>> TextHarmCandidatesAsync(Guid caseId,CancellationToken token=default)=>SendAsync<List<TextHarmCandidateSummary>>(HttpMethod.Get,$"api/admin/cases/{caseId}/text-candidates",null,token);
    public Task<ApiResult<TextHarmCandidateEvidence>> TextHarmEvidenceAsync(Guid caseId,Guid reviewId,CancellationToken token=default)=>SendAsync<TextHarmCandidateEvidence>(HttpMethod.Get,$"api/admin/cases/{caseId}/text-candidates/{reviewId}",null,token);
    public Task<ApiResult<TextHarmCandidateReviewReceipt>> ReviewTextHarmAsync(Guid caseId,Guid reviewId,TextHarmCandidateReviewRequest request,CancellationToken token=default)=>SendAsync<TextHarmCandidateReviewReceipt>(HttpMethod.Post,$"api/admin/cases/{caseId}/text-candidates/{reviewId}",request,token);

    public Task<ApiResult<BetaScorecardResult>> BetaScorecardAsync(int weeksAgo=1,CancellationToken token=default)=>SendAsync<BetaScorecardResult>(HttpMethod.Get,$"api/admin/beta-scorecard?weeksAgo={weeksAgo}",null,token);
    public Task<ApiResult<ModerationTransparencyExport>> ModerationReportAsync(int year,CancellationToken token=default)=>SendAsync<ModerationTransparencyExport>(HttpMethod.Get,$"api/admin/moderation-transparency/{year}",null,token);
    public Task<ApiResult<int>> SaveModerationReportAsync(int year,CancellationToken token=default)=>SendAsync<int>(HttpMethod.Post,$"api/admin/moderation-transparency/{year}/revisions",null,token);
    public async Task<(int Status,byte[]? Body)> ModerationReportExportAsync(int year,CancellationToken token=default)
    {
        using var response=await http.GetAsync($"api/admin/moderation-transparency/{year}/export",token);
        return ((int)response.StatusCode,response.IsSuccessStatusCode?await response.Content.ReadAsByteArrayAsync(token):null);
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
        return await ReadAsync<T>(response,cancellationToken);
    }

    private static async Task<ApiResult<T>> ReadAsync<T>(HttpResponseMessage response,CancellationToken cancellationToken)
    {
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
