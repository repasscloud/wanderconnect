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
public sealed class MemberClient(HttpClient transport)
{
    private readonly HttpClient http = ApiContractClient.Configure(transport);
    public Task<ApiResult<ApprovedPolicyDocumentView>> ApprovedPoliciesAsync(string version,CancellationToken token=default)=>SendAsync<ApprovedPolicyDocumentView>(new HttpRequestMessage(HttpMethod.Get,"api/policies/"+Uri.EscapeDataString(version)),token);
    public Task<ApiResult<MemberPolicyView>> PoliciesAsync(CancellationToken token=default)=>SendAsync<MemberPolicyView>(new HttpRequestMessage(HttpMethod.Get,"api/me/policies"),token);
    public Task<ApiResult<MemberPolicyView>> AcceptPoliciesAsync(string version,bool confirm,CancellationToken token=default)=>SendAsync<MemberPolicyView>(new HttpRequestMessage(HttpMethod.Post,"api/me/policies/accept"){Content=JsonContent.Create(new PolicyAcceptanceRequest(version,confirm))},token);
    public Task<ApiResult<SessionSummary>> SessionAsync(CancellationToken cancellationToken = default) =>
        SendAsync<SessionSummary>(new HttpRequestMessage(HttpMethod.Get, "api/session"), cancellationToken);

    public Task<ApiResult<MemberAccount>> MeAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Get, "api/me"), cancellationToken);

    public Task<ApiResult<MemberAccount>> SaveAgeAsync(AgeUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Put, "api/me/age") { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<ApiResult<MemberAccount>> RedeemAsync(string code, CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Post, "api/me/coupons") { Content = JsonContent.Create(new CouponRequest(code)) }, cancellationToken);

    /// <summary>The member's ID verification (D126); re-reads an open Didit session, so it is also the return check.</summary>
    public Task<ApiResult<IdVerificationView>> VerificationAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IdVerificationView>(new HttpRequestMessage(HttpMethod.Get, "api/me/verification"), cancellationToken);

    /// <summary>Starts (or shows the open) Didit session; client is "web" or "app" and only picks the return address.</summary>
    public Task<ApiResult<IdVerificationView>> StartVerificationAsync(string client, CancellationToken cancellationToken = default) =>
        SendAsync<IdVerificationView>(new HttpRequestMessage(HttpMethod.Post, "api/me/verification") { Content = JsonContent.Create(new StartVerification(client)) }, cancellationToken);

    /// <summary>D166: the one-time correction of the date of birth to the one on the verified ID; the API re-checks it with Didit.</summary>
    public Task<ApiResult<MemberAccount>> CorrectDateOfBirthAsync(DateOnly? dateOfBirth, CancellationToken cancellationToken = default) =>
        SendAsync<MemberAccount>(new HttpRequestMessage(HttpMethod.Post, "api/me/verification/date-of-birth") { Content = JsonContent.Create(new DobCorrectionRequest(dateOfBirth)) }, cancellationToken);

    /// <summary>The report categories as configured (D131); serious gives the Serious Safety Incident ones (D190).</summary>
    public Task<ApiResult<List<ReportCategoryView>>> ReportCategoriesAsync(bool serious = false, CancellationToken cancellationToken = default) =>
        SendAsync<List<ReportCategoryView>>(new HttpRequestMessage(HttpMethod.Get, "api/report-categories" + (serious ? "?kind=serious" : "")), cancellationToken);

    /// <summary>D190: everyone the member has dealt with, reportable even after a block, disconnect or deletion.</summary>
    public Task<ApiResult<List<SafetyPerson>>> SafetyPeopleAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<SafetyPerson>>(new HttpRequestMessage(HttpMethod.Get, "api/me/safety/people"), cancellationToken);

    /// <summary>Reports a profile, request or bot (D131), optionally blocking in the same step.</summary>
    public Task<ApiResult<MyReportView>> ReportAsync(NewReport report, CancellationToken cancellationToken = default) =>
        SendAsync<MyReportView>(new HttpRequestMessage(HttpMethod.Post, "api/reports") { Content = JsonContent.Create(report) }, cancellationToken);

    /// <summary>The member's own reports with what they may see: submitted, under review, reviewed or closed.</summary>
    public Task<ApiResult<List<MyReportView>>> MyReportsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<MyReportView>>(new HttpRequestMessage(HttpMethod.Get, "api/me/reports"), cancellationToken);

    /// <summary>Notices about actions on the member's account (D135); open even while suspended.</summary>
    public Task<ApiResult<List<NoticeView>>> NoticesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<NoticeView>>(new HttpRequestMessage(HttpMethod.Get, "api/me/notices"), cancellationToken);

    public Task<int> ReadNoticeAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/me/notices/{id}/read"), cancellationToken);

    /// <summary>#50: account standing; open even while restricted or suspended.</summary>
    public Task<ApiResult<AccountStandingView>> StandingAsync(CancellationToken cancellationToken = default) =>
        SendAsync<AccountStandingView>(new HttpRequestMessage(HttpMethod.Get, "api/me/standing"), cancellationToken);

    /// <summary>#49: the notification centre, worded for the signed-in member, with the unread count.</summary>
    public Task<ApiResult<NotificationCentre>> NotificationsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<NotificationCentre>(new HttpRequestMessage(HttpMethod.Get, "api/me/notifications"), cancellationToken);

    public Task<int> ReadNotificationAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/me/notifications/{id}/read"), cancellationToken);

    public Task<int> ReadAllNotificationsAsync(CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, "api/me/notifications/read"), cancellationToken);

    public Task<ApiResult<NotificationSettingsView>> NotificationSettingsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<NotificationSettingsView>(new HttpRequestMessage(HttpMethod.Get, "api/me/notification-settings"), cancellationToken);

    public Task<ApiResult<NotificationSettingsView>> SaveNotificationSettingsAsync(NotificationSettingsUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<NotificationSettingsView>(new HttpRequestMessage(HttpMethod.Put, "api/me/notification-settings") { Content = JsonContent.Create(update) }, cancellationToken);

    /// <summary>#49, Q83: registers an owned device for configured push; simulation is Development only. App only.</summary>
    public Task<ApiResult<PushDeviceView>> RegisterDeviceAsync(string platform, string token, CancellationToken cancellationToken = default, Guid? installationId = null) =>
        SendAsync<PushDeviceView>(new HttpRequestMessage(HttpMethod.Post, "api/me/devices") { Content = JsonContent.Create(new PushDeviceRegistration(platform, token, installationId)) }, cancellationToken);

    public Task<ApiResult<object>> RemoveDeviceAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<object>(new HttpRequestMessage(HttpMethod.Delete, $"api/me/devices/{id}"), cancellationToken);

    /// <summary>Appeals one action, once, within 30 days (D135).</summary>
    public Task<ApiResult<AppealView>> AppealAsync(Guid sanctionId, string? text, CancellationToken cancellationToken = default) =>
        SendAsync<AppealView>(new HttpRequestMessage(HttpMethod.Post, "api/me/appeals") { Content = JsonContent.Create(new NewAppeal(sanctionId, text)) }, cancellationToken);

    /// <summary>The member's effective plan from the entitlement service (D151).</summary>
    public Task<ApiResult<PlanView>> PlanAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PlanView>(new HttpRequestMessage(HttpMethod.Get, "api/me/plan"), cancellationToken);

    /// <summary>Gender and orientation option lists (D138).</summary>
    public Task<ApiResult<ProfileOptionsView>> ProfileOptionsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ProfileOptionsView>(new HttpRequestMessage(HttpMethod.Get, "api/profile-options"), cancellationToken);

    public Task<ApiResult<IdentityView>> IdentityAsync(CancellationToken cancellationToken = default) =>
        SendAsync<IdentityView>(new HttpRequestMessage(HttpMethod.Get, "api/me/identity"), cancellationToken);

    public Task<ApiResult<IdentityView>> SaveIdentityAsync(IdentityUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<IdentityView>(new HttpRequestMessage(HttpMethod.Put, "api/me/identity") { Content = JsonContent.Create(update) }, cancellationToken);

    /// <summary>One mode's filters (D137); Plus filters need Plus from the entitlement service.</summary>
    public Task<ApiResult<FilterView>> FiltersAsync(string modeId, CancellationToken cancellationToken = default) =>
        SendAsync<FilterView>(new HttpRequestMessage(HttpMethod.Get, $"api/me/modes/{Uri.EscapeDataString(modeId)}/filters"), cancellationToken);

    public Task<ApiResult<FilterView>> SaveFiltersAsync(string modeId, FilterUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<FilterView>(new HttpRequestMessage(HttpMethod.Put, $"api/me/modes/{Uri.EscapeDataString(modeId)}/filters") { Content = JsonContent.Create(update) }, cancellationToken);

    /// <summary>The connection modes as configured (D92), in display order, with their intention chips.</summary>
    public Task<ApiResult<List<ConnectionModeView>>> ModesAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ConnectionModeView>>(new HttpRequestMessage(HttpMethod.Get, "api/modes"), cancellationToken);

    public Task<ApiResult<MyProfileView>> ProfileAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MyProfileView>(new HttpRequestMessage(HttpMethod.Get, "api/me/profile"), cancellationToken);

    public Task<ApiResult<MyProfileView>> SaveProfileAsync(ProfileUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<MyProfileView>(new HttpRequestMessage(HttpMethod.Put, "api/me/profile") { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<ApiResult<MyProfileView>> SaveModeAsync(string modeId, ModeUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<MyProfileView>(new HttpRequestMessage(HttpMethod.Put, "api/me/modes/" + Uri.EscapeDataString(modeId)) { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<ApiResult<ExploreResult>> ExploreAsync(string modeId, CancellationToken cancellationToken = default) =>
        SendAsync<ExploreResult>(new HttpRequestMessage(HttpMethod.Get, "api/explore?mode=" + Uri.EscapeDataString(modeId)), cancellationToken);

    /// <summary>Asks to hear (or stops asking) when Swoovr opens where the member lives (D125 rule 37); returns the status.</summary>
    public Task<int> MarketInterestAsync(bool interested, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Put, "api/me/market-interest") { Content = JsonContent.Create(new MarketInterestUpdate(interested)) }, cancellationToken);

    /// <summary>Another member's profile as seen from one mode (D175): that mode plus modes you're connected in.</summary>
    public Task<ApiResult<PersonProfile>> PersonAsync(Guid id, string? mode = null, CancellationToken cancellationToken = default) =>
        SendAsync<PersonProfile>(new HttpRequestMessage(HttpMethod.Get, "api/profiles/" + id + (mode is null ? "" : "?mode=" + Uri.EscapeDataString(mode))), cancellationToken);

    /// <summary>Ends one mode's connection without blocking (D176); the other member isn't told.</summary>
    public Task<ApiResult<ConnectionRequestView>> DisconnectAsync(Guid connectionId, CancellationToken cancellationToken = default) =>
        SendAsync<ConnectionRequestView>(new HttpRequestMessage(HttpMethod.Post, $"api/requests/{connectionId}/disconnect"), cancellationToken);

    /// <summary>Sends a connection request in one mode to a person shown to the member (D112).</summary>
    public Task<ApiResult<ConnectionRequestView>> SendRequestAsync(Guid to, string modeId, Guid? trip = null, CancellationToken cancellationToken = default) =>
        SendAsync<ConnectionRequestView>(new HttpRequestMessage(HttpMethod.Post, "api/requests") { Content = JsonContent.Create(new NewConnectionRequest(to, modeId, trip)) }, cancellationToken);

    /// <summary>GeoNames city search (D111), largest places first.</summary>
    public Task<ApiResult<List<PlaceView>>> PlacesAsync(string query, CancellationToken cancellationToken = default) =>
        SendAsync<List<PlaceView>>(new HttpRequestMessage(HttpMethod.Get, "api/places?q=" + Uri.EscapeDataString(query)), cancellationToken);

    public Task<ApiResult<TripPage>> TripsAsync(int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<TripPage>(new HttpRequestMessage(HttpMethod.Get, $"api/me/trips?page={page}"), cancellationToken);

    /// <summary>Creates a trip (id null) or replaces one of the member's trips.</summary>
    public Task<ApiResult<TripView>> SaveTripAsync(Guid? id, TripUpdate update, CancellationToken cancellationToken = default) =>
        SendAsync<TripView>(new HttpRequestMessage(id is null ? HttpMethod.Post : HttpMethod.Put, id is null ? "api/me/trips" : $"api/me/trips/{id}")
            { Content = JsonContent.Create(update) }, cancellationToken);

    public Task<int> CancelTripAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/me/trips/{id}/cancel"), cancellationToken);

    public Task<int> RemoveTripAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/me/trips/{id}"), cancellationToken);

    public Task<ApiResult<ItineraryView>> NewItineraryAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ItineraryView>(new HttpRequestMessage(HttpMethod.Post, "api/me/itineraries"), cancellationToken);

    /// <summary>Visiting travellers for a local (Q17); 403 not-local for anyone whose home city isn't in an open market (D125).</summary>
    public Task<ApiResult<VisitorPage>> VisitorsAsync(string modeId, int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<VisitorPage>(new HttpRequestMessage(HttpMethod.Get, $"api/travel/visitors?mode={Uri.EscapeDataString(modeId)}&page={page}"), cancellationToken);

    /// <summary>Confirm-arrival (D149): the trip's arrival date must be reached and the device's fresh location inside the destination market.</summary>
    public Task<ApiResult<TripView>> ConfirmArrivalAsync(Guid tripId, LocationCheck? reading, CancellationToken cancellationToken = default) =>
        SendAsync<TripView>(new HttpRequestMessage(HttpMethod.Post, $"api/me/trips/{tripId}/arrival") { Content = JsonContent.Create(reading) }, cancellationToken);

    /// <summary>Traveller Search (D142): the destination's locals in one mode, through one of the member's own trips.</summary>
    public Task<ApiResult<TravellerSearchResult>> TravellerSearchAsync(Guid tripId, string modeId, CancellationToken cancellationToken = default) =>
        SendAsync<TravellerSearchResult>(new HttpRequestMessage(HttpMethod.Get, $"api/travel/search?trip={tripId}&mode={Uri.EscapeDataString(modeId)}"), cancellationToken);

    /// <summary>A home-location consistency check (D150): the API compares the reading with the home market and keeps only the outcome.</summary>
    public Task<ApiResult<HomeCheckView>> HomeCheckAsync(LocationCheck reading, CancellationToken cancellationToken = default) =>
        SendAsync<HomeCheckView>(new HttpRequestMessage(HttpMethod.Post, "api/me/home-check") { Content = JsonContent.Create(reading) }, cancellationToken);

    /// <summary>The member's open conversations, most recent first (D143).</summary>
    public Task<ApiResult<List<ConversationView>>> ConversationsAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<ConversationView>>(new HttpRequestMessage(HttpMethod.Get, "api/conversations"), cancellationToken);

    /// <summary>Opens (or returns) the conversation with a connected member.</summary>
    public Task<ApiResult<ConversationView>> StartConversationAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        SendAsync<ConversationView>(new HttpRequestMessage(HttpMethod.Post, "api/conversations") { Content = JsonContent.Create(new StartConversation(profileId)) }, cancellationToken);

    public Task<ApiResult<ConversationView>> ConversationAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<ConversationView>(new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{id}"), cancellationToken);

    /// <summary>Newest first; pass the previous page's Before for older messages.</summary>
    public Task<ApiResult<MessagePage>> MessagesAsync(Guid conversationId, DateTimeOffset? before = null, CancellationToken cancellationToken = default) =>
        SendAsync<MessagePage>(new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{conversationId}/messages"
            + (before is { } b ? "?before=" + Uri.EscapeDataString(b.ToString("O")) : "")), cancellationToken);

    /// <summary>#41: sends a photo in chat; the recipient sees it once it passed screening, the sender sees its state.</summary>
    public Task<ApiResult<MessageView>> SendChatPhotoAsync(Guid conversationId, byte[] data, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(file, "file", string.IsNullOrWhiteSpace(fileName) ? "photo" : fileName);
        return SendAsync<MessageView>(new HttpRequestMessage(HttpMethod.Post, $"api/conversations/{conversationId}/photos") { Content = form }, cancellationToken);
    }

    /// <summary>#41, Q85: ends the conversation politely (an optional fixed closing message), disconnecting every mode.</summary>
    public Task<ApiResult<ConversationView>> EndConversationAsync(Guid conversationId, string? closing, CancellationToken cancellationToken = default) =>
        SendAsync<ConversationView>(new HttpRequestMessage(HttpMethod.Post, $"api/conversations/{conversationId}/end") { Content = JsonContent.Create(new EndConversation(closing)) }, cancellationToken);

    /// <summary>#41: suggested first lines from intention chips both picked in a shared mode.</summary>
    public Task<ApiResult<List<ConversationStarter>>> StartersAsync(Guid conversationId, CancellationToken cancellationToken = default) =>
        SendAsync<List<ConversationStarter>>(new HttpRequestMessage(HttpMethod.Get, $"api/conversations/{conversationId}/starters"), cancellationToken);

    public Task<ApiResult<MessageView>> SendMessageAsync(Guid conversationId, string text, CancellationToken cancellationToken = default) =>
        SendAsync<MessageView>(new HttpRequestMessage(HttpMethod.Post, $"api/conversations/{conversationId}/messages") { Content = JsonContent.Create(new NewMessage(text)) }, cancellationToken);

    /// <summary>Delete for both, within five minutes of sending (D143).</summary>
    public Task<int> DeleteMessageAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/messages/{id}"), cancellationToken);

    public Task<int> HideMessageAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/messages/{id}/hide"), cancellationToken);

    public Task<int> HideConversationAsync(Guid id, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, $"api/conversations/{id}/hide"), cancellationToken);

    /// <summary>Answers a request: "accept" or "decline" (the recipient) or "withdraw" (the sender).</summary>
    public Task<ApiResult<ConnectionRequestView>> AnswerRequestAsync(Guid id, string answer, CancellationToken cancellationToken = default) =>
        SendAsync<ConnectionRequestView>(new HttpRequestMessage(HttpMethod.Post, $"api/requests/{id}/{Uri.EscapeDataString(answer)}"), cancellationToken);

    /// <summary>Pending requests in the "received" or "sent" box.</summary>
    public Task<ApiResult<RequestPage>> RequestsAsync(string box, int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<RequestPage>(new HttpRequestMessage(HttpMethod.Get, $"api/requests?box={Uri.EscapeDataString(box)}&page={page}"), cancellationToken);

    public Task<ApiResult<RequestPage>> ConnectionsAsync(int page = 1, CancellationToken cancellationToken = default) =>
        SendAsync<RequestPage>(new HttpRequestMessage(HttpMethod.Get, $"api/connections?page={page}"), cancellationToken);

    public Task<ApiResult<List<BlockedPerson>>> BlocksAsync(CancellationToken cancellationToken = default) =>
        SendAsync<List<BlockedPerson>>(new HttpRequestMessage(HttpMethod.Get, "api/blocks"), cancellationToken);

    public Task<int> BlockAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Post, "api/blocks") { Content = JsonContent.Create(new BlockRequest(profileId)) }, cancellationToken);

    public Task<int> UnblockAsync(Guid profileId, CancellationToken cancellationToken = default) =>
        StatusAsync(new HttpRequestMessage(HttpMethod.Delete, $"api/blocks/{profileId}"), cancellationToken);

    /// <summary>The member's mobile number (D148): masked, verified or waiting for a code, and which sender delivers codes.</summary>
    public Task<ApiResult<PhoneView>> PhoneAsync(CancellationToken cancellationToken = default) =>
        SendAsync<PhoneView>(new HttpRequestMessage(HttpMethod.Get, "api/me/phone"), cancellationToken);

    /// <summary>Sends a six-digit code to the number (cooldown and hourly limits apply).</summary>
    public Task<ApiResult<PhoneView>> SendPhoneCodeAsync(string number, CancellationToken cancellationToken = default) =>
        SendAsync<PhoneView>(new HttpRequestMessage(HttpMethod.Post, "api/me/phone") { Content = JsonContent.Create(new PhoneStart(number)) }, cancellationToken);

    /// <summary>Confirms the code sent to the number.</summary>
    public Task<ApiResult<PhoneView>> ConfirmPhoneAsync(string number, string code, CancellationToken cancellationToken = default) =>
        SendAsync<PhoneView>(new HttpRequestMessage(HttpMethod.Post, "api/me/phone/confirm") { Content = JsonContent.Create(new PhoneConfirm(number, code)) }, cancellationToken);

    /// <summary>The member's account deletion state (D170).</summary>
    public Task<ApiResult<DeletionView>> DeletionAsync(CancellationToken cancellationToken = default) =>
        SendAsync<DeletionView>(new HttpRequestMessage(HttpMethod.Get, "api/me/deletion"), cancellationToken);

    /// <summary>Asks for deletion (needs a sign-in within five minutes, else 403 stale-session).</summary>
    public Task<ApiResult<DeletionView>> RequestDeletionAsync(DeletionRequest request, CancellationToken cancellationToken = default) =>
        SendAsync<DeletionView>(new HttpRequestMessage(HttpMethod.Post, "api/me/deletion") { Content = JsonContent.Create(request) }, cancellationToken);

    /// <summary>Skips the rest of the 30-day recovery period: final deletion on the next purge run.</summary>
    public Task<ApiResult<DeletionView>> DeleteNowAsync(CancellationToken cancellationToken = default) =>
        SendAsync<DeletionView>(new HttpRequestMessage(HttpMethod.Post, "api/me/deletion/now"), cancellationToken);

    /// <summary>Cancels a pending deletion; the account comes back as it was.</summary>
    public Task<ApiResult<DeletionView>> CancelDeletionAsync(CancellationToken cancellationToken = default) =>
        SendAsync<DeletionView>(new HttpRequestMessage(HttpMethod.Delete, "api/me/deletion"), cancellationToken);

    /// <summary>Asks for a one-time export link (D172): once a day, after a fresh sign-in.</summary>
    public Task<ApiResult<ExportTicket>> RequestExportAsync(CancellationToken cancellationToken = default) =>
        SendAsync<ExportTicket>(new HttpRequestMessage(HttpMethod.Post, "api/me/export"), cancellationToken);

    /// <summary>Downloads the export the ticket names (its relative path); works once.</summary>
    public async Task<ApiResult<byte[]>> DownloadExportAsync(ExportTicket ticket, CancellationToken cancellationToken = default)
    {
        if (!ticket.Path.StartsWith("api/me/export/", StringComparison.Ordinal)) return new(400, null, ProblemCodes.ExportUnavailable);
        using var request = new HttpRequestMessage(HttpMethod.Get, ticket.Path);
        using var response = await http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return new((int)response.StatusCode, null, await ProblemCodeAsync(response, cancellationToken));
        return new((int)response.StatusCode, await response.Content.ReadAsByteArrayAsync(cancellationToken), null);
    }

    /// <summary>#103: the Safety &amp; Support pack for a country (null: the API's default for this request).</summary>
    public Task<ApiResult<SafetyPack>> SafetyAsync(string? country, CancellationToken cancellationToken = default) =>
        SendAsync<SafetyPack>(new HttpRequestMessage(HttpMethod.Get, "api/safety/resources" + (country is null ? "" : "?country=" + Uri.EscapeDataString(country))), cancellationToken);

    /// <summary>D203: the member's photos, main photo and per-mode photos.</summary>
    public Task<ApiResult<MyPhotosView>> PhotosAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MyPhotosView>(new HttpRequestMessage(HttpMethod.Get, "api/me/photos"), cancellationToken);

    /// <summary>Uploads a photo into quarantine (202, state "checking"); replaces names an approved photo it should take over from.</summary>
    public Task<ApiResult<MyPhotoView>> UploadPhotoAsync(byte[] data, string fileName, string contentType, Guid? replaces = null, CancellationToken cancellationToken = default)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(data);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType);
        form.Add(file, "file", string.IsNullOrWhiteSpace(fileName) ? "photo" : fileName);
        if (replaces is { } id) form.Add(new StringContent(id.ToString()), "replaces");
        return SendAsync<MyPhotoView>(new HttpRequestMessage(HttpMethod.Post, "api/me/photos") { Content = form }, cancellationToken);
    }

    public Task<ApiResult<MyPhotosView>> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default) =>
        SendAsync<MyPhotosView>(new HttpRequestMessage(HttpMethod.Delete, $"api/me/photos/{id}"), cancellationToken);

    /// <summary>Scope "main" (one photo) or a mode id (up to the limit), in order.</summary>
    public Task<ApiResult<MyPhotosView>> PlacePhotosAsync(string scope, IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default) =>
        SendAsync<MyPhotosView>(new HttpRequestMessage(HttpMethod.Put, "api/me/photos/placement") { Content = JsonContent.Create(new PhotoPlacementUpdate(scope, ids)) }, cancellationToken);

    private async Task<int> StatusAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var response = await http.SendAsync(request, cancellationToken)) return (int)response.StatusCode;
    }

    public Task<ApiResult<MeetingSafetyDashboard>> MeetingSafetyAsync(CancellationToken token=default)=>SendAsync<MeetingSafetyDashboard>(new(HttpMethod.Get,"api/me/meeting-safety"),token);
    public Task<ApiResult<TrustedContactView>> AddTrustedContactAsync(TrustedContactAdd request,CancellationToken token=default)=>SendAsync<TrustedContactView>(new(HttpMethod.Post,"api/me/meeting-safety/contacts"){Content=JsonContent.Create(request)},token);
    public Task<ApiResult<Dictionary<string,bool>>> RemoveTrustedContactAsync(Guid id,CancellationToken token=default)=>SendAsync<Dictionary<string,bool>>(new(HttpMethod.Delete,$"api/me/meeting-safety/contacts/{id}"),token);
    public Task<ApiResult<Dictionary<string,bool>>> ResendTrustedContactAsync(Guid id,CancellationToken token=default)=>SendAsync<Dictionary<string,bool>>(new(HttpMethod.Post,$"api/me/meeting-safety/contacts/{id}/resend"),token);
    public Task<ApiResult<TrustedContactCompose>> TrustedContactComposeAsync(Guid id,CancellationToken token=default)=>SendAsync<TrustedContactCompose>(new(HttpMethod.Get,$"api/me/meeting-safety/contacts/{id}/compose"),token);
    public Task<ApiResult<MeetingSafetySessionView>> StartMeetingSafetyAsync(MeetingSafetyCreate request,CancellationToken token=default)=>SendAsync<MeetingSafetySessionView>(new(HttpMethod.Post,"api/me/meeting-safety/sessions"){Content=JsonContent.Create(request)},token);
    public Task<ApiResult<MeetingSafetySessionView>> MeetingSafetyActionAsync(Guid session,MeetingSafetyAction request,CancellationToken token=default)=>SendAsync<MeetingSafetySessionView>(new(HttpMethod.Post,$"api/me/meeting-safety/sessions/{session}/actions"){Content=JsonContent.Create(request)},token);

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
