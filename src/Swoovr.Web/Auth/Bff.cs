using Swoovr.Contracts;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;

namespace Swoovr.Web.Auth;

/// <summary>
/// Backend-for-frontend (D59): the WebAssembly client calls /bff/api/... with the session
/// cookie; this host adds the member's access token from its server-side ticket and forwards to
/// the API. Tokens never reach the browser. Only the members' own routes are forwarded, and
/// only with the X-Swoovr-Bff header, which a cross-site page cannot send without a CORS
/// preflight this host never grants.
/// </summary>
public static partial class Bff
{
    public const string Header = "X-Swoovr-Bff";
    public const string ApiClient = "bff-api";

    // Member routes only, never admin ones. Mode ids and profile ids are checked again by the API.
    private const string Uuid = "[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}";

    private const string MeetingSafetyPaths="me/meeting-safety(?:/contacts(?:/"+Uuid+"(?:/(?:resend|compose))?)?|/sessions(?:/"+Uuid+"/actions)?)?";
    [GeneratedRegex(@"^api/(session|me|me/policies|me/policies/accept|me/age|me/registered-names|me/coupons|me/safety-advice|me/appearance|me/profile|modes|explore|me/modes/[a-z]{1,32}|profiles/" + Uuid
        + "|requests|requests/" + Uuid + "/(accept|decline|withdraw|disconnect)|connections|blocks|blocks/" + Uuid
        + "|me/mfa|me/mfa/disable|me/phone|me/phone/confirm|me/contact|me/sign-ins|me/sign-ins/sessions/sign-out-others|me/sign-ins/sessions/[A-Za-z0-9_-]{22}/sign-out|me/sign-ins/linked/[a-z0-9][a-z0-9_-]{0,63}/remove|me/deletion|me/deletion/now|me/export|me/export/[A-Za-z0-9_-]{43}|me/market-interest|safety/resources|me/photos|me/photos/placement|me/photos/decisions|me/photos/decisions/" + Uuid + "/appeal|me/photos/" + Uuid + "|me/photos/albums|me/photos/albums/" + Uuid + "|me/explicit-media|me/verification|me/verification/date-of-birth|report-categories|reports|me/reports|me/safety/people|me/notices|me/notices/" + Uuid + "/read|me/notifications|me/notifications/read|me/notifications/" + Uuid + "/read|me/notification-settings|me/standing|me/tickets|me/tickets/options|me/tickets/[0-9]{1,18}|me/tickets/[0-9]{1,18}/replies|me/tickets/upload|me/tickets/[0-9]{1,18}/replies/upload|me/tickets/[0-9]{1,18}/close|me/tickets/[0-9]{1,18}/articles/[0-9]{1,18}/attachments/[0-9]{1,18}|" + MeetingSafetyPaths + "|me/appeals|me/plan|profile-options|me/identity|me/modes/[a-z]{1,32}/filters|places|me/trips|me/trips/" + Uuid + "|me/trips/" + Uuid + "/cancel|me/trips/" + Uuid + "/arrival|me/itineraries|travel/visitors|travel/search|me/home-check|conversations|conversations/" + Uuid + "|conversations/" + Uuid + "/(messages|hide|photos|end|starters)|messages/" + Uuid + "|messages/" + Uuid + "/hide)$")]
    private static partial Regex Allowed();

    public static void AddBff(this IServiceCollection services, IConfiguration configuration) =>
        services.AddHttpClient(ApiClient, http =>
        {
            Swoovr.Client.ApiContractClient.Configure(http);
            http.BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
            // D203: a photo upload may take longer than an ordinary call.
            http.Timeout = TimeSpan.FromSeconds(60);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

    /// <summary>Issue 220: support files to and from the helpdesk may take minutes; only the BFF's upload and attachment routes use this.</summary>
    public const string ApiTransferClient = "bff-api-transfer";

    public static void AddBffTransfers(this IServiceCollection services, IConfiguration configuration) =>
        services.AddHttpClient(ApiTransferClient, http =>
        {
            Swoovr.Client.ApiContractClient.Configure(http);
            http.BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
            http.Timeout = TimeSpan.FromMinutes(11);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

    public const long PhotoUploadLimit = 10 * 1024 * 1024 + 64 * 1024;

    /// <summary>
    /// D203: a photo's short-lived signed link, passed to the API as is. The token is the permission (made after the API
    /// checked the viewer's access), so an &lt;img&gt; can load it without the BFF header; nothing else goes through here.
    /// </summary>
    public static void MapMedia(WebApplication app) =>
        app.MapGet("/media/{token}", async (string token, HttpContext context, IHttpClientFactory clients) =>
        {
            if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } pages) pages.Enabled = false;
            if (!MediaToken().IsMatch(token)) return Results.NotFound();
            using var response = await clients.CreateClient(ApiClient).GetAsync("api/media/" + token, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            if (!response.IsSuccessStatusCode) return Results.NotFound();
            context.Response.Headers.CacheControl = "private, max-age=300";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            return Results.File(await response.Content.ReadAsByteArrayAsync(context.RequestAborted), "image/jpeg");
        });

    public const long TicketLimit = 300_000;

    [GeneratedRegex("^api/me/tickets/[0-9]{1,18}/replies$")]
    private static partial Regex TicketReply();

    [GeneratedRegex("^api/me/tickets(/[0-9]{1,18}/replies)?/upload$")]
    private static partial Regex TicketUpload();

    [GeneratedRegex("^api/me/tickets/[0-9]{1,18}/articles/[0-9]{1,18}/attachments/[0-9]{1,18}$")]
    private static partial Regex TicketAttachment();

    [GeneratedRegex("^api/conversations/" + Uuid + "/photos$")]
    private static partial Regex ChatPhoto();

    // Issue 231: "own" is the owner's preview of their own refused photo; the API still checks signature, expiry and status.
    [GeneratedRegex(@"^[0-9a-f]{32}\.(?:member|own)\.[0-9]{1,12}\.[A-Za-z0-9_-]{43}$")]
    private static partial Regex MediaToken();

    public static void MapBff(WebApplication app) =>
        app.MapMethods("/bff/{**path}", ["GET", "POST", "PUT", "DELETE"], async (string? path, HttpContext context, IHttpClientFactory clients) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            // The BFF answers with its own statuses; the site's not-found page must not replace them (a re-executed POST
            // would otherwise fail antiforgery and turn a 401/404 into a 400).
            if (context.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodePagesFeature>() is { } pages) pages.Enabled = false;
            if (context.Request.Headers[Header] != "1") return Results.BadRequest();
            if (path is null || !Allowed().IsMatch(path)) return Results.NotFound();
            // The cookie handler has already refreshed an expiring token (MemberSignIn.RefreshWhenExpiring).
            if (context.User.Identity?.IsAuthenticated != true || await context.GetTokenAsync("access_token") is not { } token)
                return Results.Unauthorized();
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path + context.Request.QueryString);
            request.Headers.Authorization = new("Bearer", token);
            // Forward explicit dates unchanged, including invalid/duplicate values: the API owns rejection.
            if (context.Request.Headers.TryGetValue(ApiContract.Header, out var versions))
                request.Headers.TryAddWithoutValidation(ApiContract.Header, versions.ToArray());
            if(path=="api/me/appearance"&&context.Request.Headers.TryGetValue(AppearanceOwner.Header,out var owner))request.Headers.TryAddWithoutValidation(AppearanceOwner.Header,owner.ToArray());
            // D181: the browser's address for the API's per-address limits (believed only from a trusted proxy).
            if (context.Connection.RemoteIpAddress is { } address) request.Headers.TryAddWithoutValidation("X-Forwarded-For", address.ToString());
            if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                // D203, #41: only photo uploads (profile and chat) may carry a large body (10 MB plus the multipart overhead).
                var limit = context.Request.Method == "POST" && (path == "api/me/photos" || ChatPhoto().IsMatch(path)) ? PhotoUploadLimit
                    // Issue 220 (D330): support files go as multipart to the two upload routes, up to the owner's limits.
                    : context.Request.Method == "POST" && TicketUpload().IsMatch(path) ? Swoovr.Contracts.SupportAttachmentPolicy.MaxUploadRequestBytes
                    // Support requests carry up to 16,000 characters and three 64 KB text files as JSON (the API caps them at 300,000 bytes).
                    : context.Request.Method == "POST" && (path == "api/me/tickets" || TicketReply().IsMatch(path)) ? TicketLimit : 16 * 1024;
                if (context.Request.ContentLength > limit) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                // The web server's own body limit (30 MB by default) follows the route's limit.
                if (context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } size) size.MaxRequestBodySize = limit;
                request.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is { } type) request.Content.Headers.TryAddWithoutValidation("Content-Type", type);
            }
            // Support files (issue 220) may take minutes to reach the helpdesk or come back from it; everything else keeps 60 seconds.
            var client = clients.CreateClient(TicketUpload().IsMatch(path) || TicketAttachment().IsMatch(path) ? ApiTransferClient : ApiClient);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            // #201: the API refused a token this host still holds, so a session cutoff retired it. End the local session and tell
            // the app where to go (sign in again, which then shows the account's real state, such as the locked screen).
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                context.Response.Headers[RetiredSession.Header] = await RetiredSessions.EndAsync(context);
            context.Response.Headers.Append("Vary", ApiContract.Header);
            if (response.Headers.TryGetValues(ApiContract.Header, out var selectedVersions))
                context.Response.Headers[ApiContract.Header] = selectedVersions.ToArray();
            if(path=="api/me/appearance"&&response.Headers.TryGetValues(AppearanceOwner.Header,out var owners)&&owners.Take(2).ToArray() is{Length:1} selected&&AppearanceOwner.Valid(selected[0]))context.Response.Headers[AppearanceOwner.Header]=selected[0];
            // Body and content type only: no API cookies or authentication challenges reach the browser.
            if (response.Content.Headers.ContentType is { } contentType) context.Response.ContentType = contentType.ToString();
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            return Results.Empty;
        }).DisableAntiforgery();
}
