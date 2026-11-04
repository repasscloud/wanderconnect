using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;

namespace Swoovr.Api.Ticketing;

/// <summary>
/// Guest support API for the separately built public website (contract: docs/delivery/zammad/GUEST-API.md). Anonymous code
/// requests and exchanges; every ticket route needs `Authorization: SwoovrGuest {grant}`. No cookies, no member sessions.
/// </summary>
public static class GuestSupportEndpoints
{
    public const string Prefix = "/api/support/guest";

    public static void Map(WebApplication app)
    {
        var guest = app.MapGroup(Prefix).AllowAnonymous();
        guest.MapGet("/categories", (HttpContext c) => { Headers(c); return Results.Ok(TicketingGuestPolicy.Categories.Keys.Select(k => new GuestCategory(k, Label(k))).ToArray()); });
        guest.MapPost("/codes", (GuestCodeRequest request, HttpContext c, GuestSupport s, IConfiguration configuration) =>
            Run(c, async ct => Results.Accepted(value: await s.RequestCodeAsync(request, PhoneEndpoints.ClientAddress(c, configuration), ct))));
        guest.MapPost("/grants", (GuestGrantRequest request, HttpContext c, GuestSupport s) => Run(c, async ct => Results.Ok(await s.ExchangeAsync(request, ct))));
        guest.MapDelete("/grants/current", (HttpContext c, GuestSupport s) => Run(c, async ct => { await s.RevokeAsync(GuestSupport.Presented(c), ct); return Results.NoContent(); }));
        guest.MapGet("/tickets", (HttpContext c, GuestSupport s) => Run(c, async ct => Results.Ok(await s.ListAsync(await s.GuestAsync(GuestSupport.Presented(c), ct), Page(c), ct))));
        guest.MapPost("/tickets", (GuestTicketCreate request, HttpContext c, GuestSupport s) => Run(c, async ct => Results.Ok(await s.CreateAsync(await s.GuestAsync(GuestSupport.Presented(c), ct), request, ct))));
        guest.MapGet("/tickets/{id:long}", (long id, HttpContext c, GuestSupport s) => Run(c, async ct => Results.Ok(await s.DetailAsync(await s.GuestAsync(GuestSupport.Presented(c), ct), id, ct))));
        guest.MapPost("/tickets/{id:long}/replies", (long id, TicketReply request, HttpContext c, GuestSupport s) => Run(c, async ct => Results.Ok(await s.ReplyAsync(await s.GuestAsync(GuestSupport.Presented(c), ct), id, request, ct))));
        guest.MapGet("/tickets/{id:long}/articles/{article:long}/attachments/{attachment:long}", (long id, long article, long attachment, HttpContext c, GuestSupport s) =>
            Run(c, async ct => Results.Ok(await s.DownloadAsync(await s.GuestAsync(GuestSupport.Presented(c), ct), id, article, attachment, ct))));
    }

    public static string Label(string category) => category switch
    {
        "account" => "Help with my account", "recovery" => "I can't sign in", "billing" => "Billing and subscriptions",
        "safety" => "A safety concern", "privacy" => "Privacy and my data", "media" => "Media enquiry",
        "partnerships" => "Partnerships and business", _ => "Something else",
    };

    private static int Page(HttpContext c) => string.IsNullOrEmpty(c.Request.Query["page"]) ? 1 : int.TryParse(c.Request.Query["page"], out var p) ? p : throw new ArgumentException("Invalid page.");

    private static void Headers(HttpContext c)
    {
        c.Response.Headers.CacheControl = "no-store";
        c.Response.Headers["X-Content-Type-Options"] = "nosniff";
        c.Response.Headers["Referrer-Policy"] = "no-referrer";
        c.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
    }

    private static async Task<IResult> Run(HttpContext c, Func<CancellationToken, Task<IResult>> action)
    {
        Headers(c);
        try { return await action(c.RequestAborted); }
        catch (TicketingAccessException e) { return Problem(e.Status, e.Code); }
        catch (TicketingProviderException e) { return Problem(e.Uncertain ? 409 : e.Code is "provider-forbidden" or "provider-not-found" ? 404 : 503, e.Uncertain ? "ticket-reconciliation-required" : "ticket-provider-unavailable"); }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException e) when (e.InnerException is Npgsql.PostgresException { SqlState: "23505" }) { return Problem(409, "ticket-operation-conflict"); }
        catch (ArgumentException) { return Problem(400, "invalid-request"); }
        catch (InvalidOperationException) { return Problem(409, "ticket-operation-conflict"); }
    }

    private static IResult Problem(int status, string code) => Results.Problem(statusCode: status, title: "Support request could not be completed.", extensions: new Dictionary<string, object?> { ["code"] = code });

    /// <summary>
    /// Explicit origins only (`Ticketing:Guest:AllowedOrigins`). A browser Origin not on the list is refused, preflight
    /// included; requests without an Origin (server-side callers) still need a grant for every ticket route. Never credentials.
    /// </summary>
    public static async Task<bool> OriginAsync(HttpContext c, IConfiguration configuration)
    {
        if (!c.Request.Path.StartsWithSegments(Prefix)) return true;
        var origin = c.Request.Headers.Origin.ToString();
        var allowed = configuration.GetSection("Ticketing:Guest:AllowedOrigins").Get<string[]>() ?? [];
        if (origin.Length > 0)
        {
            if (c.Request.Headers.Origin.Count != 1 || !allowed.Contains(origin, StringComparer.Ordinal)) { c.Response.StatusCode = 403; return false; }
            c.Response.Headers.AccessControlAllowOrigin = origin;
            c.Response.Headers.Vary = "Origin";
            c.Response.Headers.AccessControlExposeHeaders = ApiContract.Header;
        }
        if (HttpMethods.IsOptions(c.Request.Method))
        {
            c.Response.Headers.AccessControlAllowMethods = "GET, POST, DELETE";
            c.Response.Headers.AccessControlAllowHeaders = "Authorization, Content-Type, " + ApiContract.Header;
            c.Response.Headers.AccessControlMaxAge = "600";
            c.Response.StatusCode = origin.Length > 0 ? 204 : 403;
            return false;
        }
        // JSON bodies only: a cross-site form post can't produce application/json without a preflight.
        if (c.Request.Method is "POST" && !(c.Request.ContentType ?? "").StartsWith("application/json", StringComparison.OrdinalIgnoreCase)) { c.Response.StatusCode = 415; return false; }
        await Task.CompletedTask;
        return true;
    }
}
