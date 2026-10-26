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

    /// <summary>The member's ID verification (D126); re-reads an open Didit session, so it is also the return check.</summary>
    public Task<ApiResult<IdVerificationView>> VerificationAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IdVerificationView>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me/verification"), cancellationToken);

    /// <summary>Starts (or shows the open) Didit session; client is "web" or "app" and only picks the return address.</summary>
    public Task<ApiResult<IdVerificationView>> StartVerificationAsync(string client, CancellationToken cancellationToken = default) =>
        SendAsync<IdVerificationView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/me/verification") { Content = JsonContent.Create(new StartVerification(client)) }, cancellationToken);

    /// <summary>D166: the one-time correction of the date of birth to the one on the verified ID; the API re-checks it with Didit.</summary>
    public Task<ApiResult<MemberAccount>> CorrectDateOfBirthAsync(DateOnly? dateOfBirth, CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Post, "api/v1/me/verification/date-of-birth") { Content = JsonContent.Create(new DobCorrectionRequest(dateOfBirth)) }, cancellationToken);

    /// <summary>The report categories as configured (D131).</summary>
    public Task<ApiResult<List<ReportCategoryView>>> ReportCategoriesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportCategoryView>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/report-categories"), cancellationToken);

    /// <summary>Reports a profile, request or bot (D131), optionally blocking in the same step.</summary>
    public Task<ApiResult<MyReportView>> ReportAsync(NewReport report, CancellationToken cancellationToken = default) =>
        SendAsync<MyReportView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/reports") { Content = JsonContent.Create(report) }, cancellationToken);

    /// <summary>The member's own reports with what they may see: submitted, under review, reviewed or closed.</summary>
    public Task<ApiResult<List<MyReportView>>> MyReportsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<MyReportView>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me/reports"), cancellationToken);

    /// <summary>Notices about actions on the member's account (D135); open even while suspended.</summary>
    public Task<ApiResult<List<NoticeView>>> NoticesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<NoticeView>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me/notices"), cancellationToken);

    public Task<int> ReadNoticeAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/v1/me/notices/{id}/read"), cancellationToken);

    /// <summary>Appeals one action, once, within 30 days (D135).</summary>
    public Task<ApiResult<AppealView>> AppealAsync(Guid sanctionId, string? text, CancellationToken cancellationToken = default) =>
        SendAsync<AppealView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/me/appeals") { Content = JsonContent.Create(new NewAppeal(sanctionId, text)) }, cancellationToken);

    /// <summary>The member's effective plan from the entitlement service (D151).</summary>
    public Task<ApiResult<PlanView>> PlanAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PlanView>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me/plan"), cancellationToken);

    /// <summary>Gender and orientation option lists (D138).</summary>
    public Task<ApiResult<ProfileOptionsView>> ProfileOptionsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ProfileOptionsView>(new HttpRequestMessage(HttpMethod.Get, "api/v1/profile-options"), cancellationToken);

    public Task<ApiResult<IdentityView>> IdentityAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IdentityView>(new HttpRequestMessage(HttpMethod.Get, "api/v1/me/identity"), cancellationToken);

    public Task<ApiResult<IdentityView>> SaveIdentityAsync(IdentityUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<IdentityView>(new HttpRequestMessage(HttpMethod.Put, "api/v1/me/identity") { Content = JsonContent.Create(update) }, cancellationToken);

    /// <summary>One mode's filters (D137); Plus filters need Plus from the entitlement service.</summary>
    public Task<ApiResult<FilterView>> FiltersAsync(string modeId, CancellationToken cancellationToken = default) =>
        SendAsync<FilterView>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/me/modes/{Uri.EscapeDataString(modeId)}/filters"), cancellationToken);

    public Task<ApiResult<FilterView>> SaveFiltersAsync(string modeId, FilterUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<FilterView>(new HttpRequestMessage(HttpMethod.Put, $"api/v1/me/modes/{Uri.EscapeDataString(modeId)}/filters") { Content = JsonContent.Create(update) }, cancellationToken);

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

    /// <summary>Asks to hear (or stops asking) when Swoovr opens where the member lives (D125 rule 37); returns the status.</summary>
    public Task<int> MarketInterestAsync(bool interested, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Put, "api/v1/me/market-interest") { Content = JsonContent.Create(new MarketInterestUpdate(interested)) }, cancellationToken);

    public Task<ApiResult<PersonProfile>> PersonAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<PersonProfile>(new HttpRequestMessage(HttpMethod.Get, "api/v1/profiles/" + id), cancellationToken);

    /// <summary>Sends a connection request in one mode to a person shown to the member (D112).</summary>
    public Task<ApiResult<ConnectionRequestView>> SendRequestAsync(Guid to, string modeId, Guid? trip = null, CancellationToken cancellationToken = default) =>
        SendAsync<ConnectionRequestView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/requests") { Content = JsonContent.Create(new NewConnectionRequest(to, modeId, trip)) }, cancellationToken);

    /// <summary>GeoNames city search (D111), largest places first.</summary>
    public Task<ApiResult<List<PlaceView>>> PlacesAsync(string query, CancellationToken cancellationToken = default) =>
        SendAsync<List<PlaceView>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/places?q=" + Uri.EscapeDataString(query)), cancellationToken);

    public Task<ApiResult<TripPage>> TripsAsync(int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<TripPage>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/me/trips?page={page}"), cancellationToken);

    /// <summary>Creates a trip (id null) or replaces one of the member's trips.</summary>
    public Task<ApiResult<TripView>> SaveTripAsync(Guid? id, TripUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<TripView>(new HttpRequestMessage(id is null ? HttpMethod.Post : HttpMethod.Put, id is null ? "api/v1/me/trips" : $"api/v1/me/trips/{id}")
            { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<int> CancelTripAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/v1/me/trips/{id}/cancel"), cancellationToken);

    public Task<int> RemoveTripAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/v1/me/trips/{id}"), cancellationToken);

    public Task<ApiResult<ItineraryView>> NewItineraryAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ItineraryView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/me/itineraries"), cancellationToken);

    /// <summary>Visiting travellers for a local (Q17); 403 not-local for anyone whose home city isn't in an open market (D125).</summary>
    public Task<ApiResult<VisitorPage>> VisitorsAsync(string modeId, int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<VisitorPage>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/travel/visitors?mode={Uri.EscapeDataString(modeId)}&page={page}"), cancellationToken);

    /// <summary>Confirm-arrival (D149): the trip's arrival date must be reached and the device's fresh location inside the destination market.</summary>
    public Task<ApiResult<TripView>> ConfirmArrivalAsync(Guid tripId, LocationCheck? reading, CancellationToken cancellationToken = default) =>
        SendAsync<TripView>(new HttpRequestMessage(HttpMethod.Post, $"api/v1/me/trips/{tripId}/arrival") { Content = JsonContent.Create(reading) }, cancellationToken);

    /// <summary>Traveller Search (D142): the destination's locals in one mode, through one of the member's own trips.</summary>
    public Task<ApiResult<TravellerSearchResult>> TravellerSearchAsync(Guid tripId, string modeId, CancellationToken cancellationToken = default) =>
        SendAsync<TravellerSearchResult>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/travel/search?trip={tripId}&mode={Uri.EscapeDataString(modeId)}"), cancellationToken);

    /// <summary>A home-location consistency check (D150): the API compares the reading with the home market and keeps only the outcome.</summary>
    public Task<ApiResult<HomeCheckView>> HomeCheckAsync(LocationCheck reading, CancellationToken cancellationToken = default) =>
        SendAsync<HomeCheckView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/me/home-check") { Content = JsonContent.Create(reading) }, cancellationToken);

    /// <summary>The member's open conversations, most recent first (D143).</summary>
    public Task<ApiResult<List<ConversationView>>> ConversationsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ConversationView>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/conversations"), cancellationToken);

    /// <summary>Opens (or returns) the conversation with a connected member.</summary>
    public Task<ApiResult<ConversationView>> StartConversationAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        SendAsync<ConversationView>(new HttpRequestMessage(HttpMethod.Post, "api/v1/conversations") { Content = JsonContent.Create(new StartConversation(profileId)) }, cancellationToken);

    public Task<ApiResult<ConversationView>> ConversationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<ConversationView>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/conversations/{id}"), cancellationToken);

    /// <summary>Newest first; pass the previous page's Before for older messages.</summary>
    public Task<ApiResult<MessagePage>> MessagesAsync(Guid conversationId, DateTimeOffset? before = null, CancellationToken cancellationToken = default) =>
        SendAsync<MessagePage>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/conversations/{conversationId}/messages"
            + (before is { } b ? "?before=" + Uri.EscapeDataString(b.ToString("O")) : "")), cancellationToken);

    public Task<ApiResult<MessageView>> SendMessageAsync(Guid conversationId, string text, CancellationToken cancellationToken = default) =>
        SendAsync<MessageView>(new HttpRequestMessage(HttpMethod.Post, $"api/v1/conversations/{conversationId}/messages") { Content = JsonContent.Create(new NewMessage(text)) }, cancellationToken);

    /// <summary>Delete for both, within five minutes of sending (D143).</summary>
    public Task<int> DeleteMessageAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/v1/messages/{id}"), cancellationToken);

    public Task<int> HideMessageAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/v1/messages/{id}/hide"), cancellationToken);

    public Task<int> HideConversationAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/v1/conversations/{id}/hide"), cancellationToken);

    /// <summary>Answers a request: "accept" or "decline" (the recipient) or "withdraw" (the sender).</summary>
    public Task<ApiResult<ConnectionRequestView>> AnswerRequestAsync(Guid id, string answer, CancellationToken cancellationToken = default) =>
        SendAsync<ConnectionRequestView>(new HttpRequestMessage(HttpMethod.Post, $"api/v1/requests/{id}/{Uri.EscapeDataString(answer)}"), cancellationToken);

    /// <summary>Pending requests in the "received" or "sent" box.</summary>
    public Task<ApiResult<RequestPage>> RequestsAsync(string box, int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<RequestPage>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/requests?box={Uri.EscapeDataString(box)}&page={page}"), cancellationToken);

    public Task<ApiResult<RequestPage>> ConnectionsAsync(int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<RequestPage>(new HttpRequestMessage(HttpMethod.Get, $"api/v1/connections?page={page}"), cancellationToken);

    public Task<ApiResult<List<BlockedPerson>>> BlocksAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<BlockedPerson>>(new HttpRequestMessage(HttpMethod.Get, "api/v1/blocks"), cancellationToken);

    public Task<int> BlockAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, "api/v1/blocks") { Content = JsonContent.Create(new BlockRequest(profileId)) }, cancellationToken);

    public Task<int> UnblockAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/v1/blocks/{profileId}"), cancellationToken);

    private async Task<int> StatusAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await http.SendAsync(request, cancellationToken)) return (int)response.StatusCode;
    }

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
