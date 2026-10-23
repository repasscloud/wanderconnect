using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;

namespace Swoovr.Web.Auth;

/// <summary>
/// Backend-for-frontend (D59): the WebAssembly client calls /bff/api/v1/... with the session
/// cookie; this host adds the member's access token from its server-side ticket and forwards to
/// the API. Tokens never reach the browser. Only the members' own routes are forwarded, and
/// only with the X-Swoovr-Bff header, which a cross-site page cannot send without a CORS
/// preflight this host never grants.
/// </summary>
public static partial class Bff
{
    public const string Header = "X-Swoovr-Bff";
    public const string ApiClient = "bff-api";

    [GeneratedRegex(@"^api/v1/(session|me|me/age|me/coupons)$")]
    private static partial Regex Allowed();

    public static void AddBff(this IServiceCollection services, IConfiguration configuration) =>
        services.AddHttpClient(ApiClient, http =>
        {
            http.BaseAddress = new Uri(configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
            http.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });

    public static void MapBff(WebApplication app) =>
        app.MapMethods("/bff/{**path}", ["GET", "POST", "PUT", "DELETE"], async (string? path, HttpContext context, IHttpClientFactory clients) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (context.Request.Headers[Header] != "1") return Results.BadRequest();
            if (path is null || !Allowed().IsMatch(path)) return Results.NotFound();
            // The cookie handler has already refreshed an expiring token (MemberSignIn.RefreshWhenExpiring).
            if (context.User.Identity?.IsAuthenticated != true || await context.GetTokenAsync("access_token") is not { } token)
                return Results.Unauthorized();
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), path + context.Request.QueryString);
            request.Headers.Authorization = new("Bearer", token);
            if (context.Request.ContentLength > 0 || context.Request.Headers.TransferEncoding.Count > 0)
            {
                if (context.Request.ContentLength > 16 * 1024) return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                request.Content = new StreamContent(context.Request.Body);
                if (context.Request.ContentType is { } type) request.Content.Headers.TryAddWithoutValidation("Content-Type", type);
            }
            using var response = await clients.CreateClient(ApiClient).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);
            context.Response.StatusCode = (int)response.StatusCode;
            // Body and content type only: no API cookies or authentication challenges reach the browser.
            if (response.Content.Headers.ContentType is { } contentType) context.Response.ContentType = contentType.ToString();
            await response.Content.CopyToAsync(context.Response.Body, context.RequestAborted);
            return Results.Empty;
        }).DisableAntiforgery();
}
