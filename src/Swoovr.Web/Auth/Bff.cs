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

    [GeneratedRegex(@"^api/(session|me|me/age|me/coupons|me/profile|modes|explore|me/modes/[a-z]{1,32}|profiles/" + Uuid
        + "|requests|requests/" + Uuid + "/(accept|decline|withdraw|disconnect)|connections|blocks|blocks/" + Uuid
        + "|me/phone|me/phone/confirm|me/deletion|me/deletion/now|me/export|me/export/[A-Za-z0-9_-]{43}|me/market-interest|safety/resources|me/photos|me/photos/placement|me/photos/" + Uuid + "|me/verification|me/verification/date-of-birth|report-categories|reports|me/reports|me/safety/people|me/notices|me/notices/" + Uuid + "/read|me/notifications|me/notifications/read|me/notifications/" + Uuid + "/read|me/notification-settings|me/standing|me/appeals|me/plan|profile-options|me/identity|me/modes/[a-z]{1,32}/filters|places|me/trips|me/trips/" + Uuid + "|me/trips/" + Uuid + "/cancel|me/trips/" + Uuid + "/arrival|me/itineraries|travel/visitors|travel/search|me/home-check|conversations|conversations/" + Uuid + "|conversations/" + Uuid + "/(messages|hide|photos|end|starters)|messages/" + Uuid + "|messages/" + Uuid + "/hide)$")]
    private static partial Regex Allowed();

    public static void AddBff(this IServiceCollection services, IConfiguration configuration) =>
        services.AddHttpClient(ApiClient, http =>
        {
            Swoovr.Client.ApiContractClient.Configure(http);
            http.BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
            // D203: a photo upload may take longer than an ordinary call.
            http.Timeout = TimeSpan.FromSeconds(60);
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

    [GeneratedRegex("^api/conversations/" + Uuid + "/photos$")]
    private static partial Regex ChatPhoto();

    [GeneratedRegex(@"^[0-9a-f]{32}\.member\.[0-9]{1,12}\.[A-Za-z0-9_-]{43}$")]
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
            // D181: the browser's address for the API's per-address limits (believed only from a trusted proxy).
            if (context.Connection.RemoteIpAddress is { } address) request.Headers.TryAddWithoutValidation("X-Forwarded-For", address.ToString());
            if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                // D203, #41: only photo uploads (profile and chat) may carry a large body (10 MB plus the multipart overhead).
                var limit = context.Request.Method == "POST" && (path == "api/me/photos" || ChatPhoto().IsMatch(path)) ? PhotoUploadLimit : 16 * 1024;
                if (context.Request.ContentLength > limit) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                request.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is { } type) request.Content.Headers.TryAddWithoutValidation("Content-Type", type);
            }
            using var response = await clients.CreateClient(ApiClient).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.Headers.Append("Vary", ApiContract.Header);
            if (response.Headers.TryGetValues(ApiContract.Header, out var selectedVersions))
                context.Response.Headers[ApiContract.Header] = selectedVersions.ToArray();
            // Body and content type only: no API cookies or authentication challenges reach the browser.
            if (response.Content.Headers.ContentType is { } contentType) context.Response.ContentType = contentType.ToString();
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            return Results.Empty;
        }).DisableAntiforgery();
}
