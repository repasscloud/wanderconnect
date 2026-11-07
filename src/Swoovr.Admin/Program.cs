using Swoovr.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Swoovr.Admin;
using Swoovr.Admin.Components;

// The administration app (D136): a separate Blazor host with server-side rendering only, signed in through the Keycloak
// staff realm. It calls the protected admin API server-side with the staff member's token, which never reaches the
// browser (the cookie holds only a session key). Every rule is enforced by the API; this app only presents it.
var builder = WebApplication.CreateBuilder(args);
HostRuntime.Configure(builder,HostKind.Admin);
builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TicketStore>();
builder.Services.AddSingleton<StaffSessionRenewal>();
builder.Services.AddTransient<StaffTokenHandler>();
builder.Services.AddHttpClient<Swoovr.Client.AdminClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<StaffTokenHandler>();
// Issue 222: support files to and from the helpdesk may take minutes; only ticket uploads and attachment downloads use this.
builder.Services.AddHttpClient(Swoovr.Admin.AdminTransfers.Client, http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromMinutes(11);
}).AddHttpMessageHandler<StaffTokenHandler>();
// A staff reply may carry the owner's 150 MB of files (D330); the request limit is raised for that page's posts only (below).
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = Swoovr.Contracts.SupportAttachmentPolicy.MaxUploadRequestBytes);

var cookieSecurity = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
var auth = builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
});
auth.AddCookie(options =>
{
    options.Cookie.Name = "swoovr.admin";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurity;
    // DEFAULT: staff sessions last at most 8 hours; sensitive actions still need a sign-in within 15 minutes (API).
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events.OnValidatePrincipal = context => context.HttpContext.RequestServices.GetRequiredService<StaffSessionRenewal>().ValidateAsync(context);
});
builder.Services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
    .Configure<TicketStore>((options, store) => options.SessionStore = store);
auth.AddOpenIdConnect(options =>
{
    options.Authority = builder.Configuration["Keycloak:Authority"] ?? "http://localhost:8180/realms/swoovr-staff-dev";
    options.ClientId = builder.Configuration["Keycloak:ClientId"] ?? "swoovr-admin-web";
    options.ClientSecret = builder.Configuration["Keycloak:ClientSecret"];
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment() || builder.Configuration.GetValue("Keycloak:RequireHttpsMetadata", true);
    options.ResponseType = OpenIdConnectResponseType.Code;
    options.ResponseMode = OpenIdConnectResponseMode.Query;
    options.UsePkce = true;
    options.SaveTokens = true;
    options.MapInboundClaims = false;
    options.GetClaimsFromUserInfoEndpoint = false;
    options.TokenValidationParameters.NameClaimType = "given_name";
    options.Scope.Clear();
    foreach (var scope in new[] { "openid", "profile", "email" }) options.Scope.Add(scope);
    foreach (var cookie in new[] { options.CorrelationCookie, options.NonceCookie }) { cookie.SameSite = SameSiteMode.Lax; cookie.SecurePolicy = cookieSecurity; }
    // "Sign in again" (a stale-session answer from the API) forces a fresh password + code at Keycloak.
    options.Events.OnRedirectToIdentityProvider = context =>
    {
        if (context.Properties.Items.ContainsKey("reauth")) { context.ProtocolMessage.MaxAge = "0"; context.ProtocolMessage.Prompt = "login"; }
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();

var app = builder.Build();
HostRuntime.Use(app,HostKind.Admin);
HostRuntime.MapPortalHealth(app);
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error", createScopeForErrors: true); app.UseHsts(); }
app.Use(async(context,next)=>
{
    if(context.Request.Path.StartsWithSegments("/code-reports")||context.Request.Path.StartsWithSegments("/beta-scorecard")||context.Request.Path.StartsWithSegments("/beta"))context.Response.Headers.CacheControl="no-store";
    // Issue 222: a support request's reply form may carry the owner's file list (D330); every other request keeps the default limit.
    if(Swoovr.Admin.AdminTransfers.IsTicketPost(context.Request)&&context.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>() is {IsReadOnly:false} size)
        size.MaxRequestBodySize=Swoovr.Contracts.SupportAttachmentPolicy.MaxUploadRequestBytes;
    await next(context);
});
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapGet("/sign-in", (bool? reauth, string? returnUrl) =>
{
    var properties = new AuthenticationProperties { RedirectUri = returnUrl is { } r && r.StartsWith('/') && !r.StartsWith("//") ? r : "/" };
    if (reauth == true) properties.Items["reauth"] = "1";
    return Results.Challenge(properties, [OpenIdConnectDefaults.AuthenticationScheme]);
}).AllowAnonymous();
// Sign out ends the Keycloak session as well as this app's cookie and server-side ticket, so the next sign-in always starts
// from the password (and code) again. Keycloak logout runs first so it can still read the id_token hint from the ticket.
app.MapPost("/sign-out", async (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (context.User.Identity?.IsAuthenticated == true)
        return Results.SignOut(new AuthenticationProperties { RedirectUri = "/signed-out" },
            [OpenIdConnectDefaults.AuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme]);
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/signed-out");
}).AllowAnonymous();
app.MapGet("/signed-out", (HttpContext context) =>
{
    context.Response.Headers.CacheControl = "no-store";
    if (context.User.Identity?.IsAuthenticated == true) return Results.Redirect("/");
    // Q129 / D320: the product version on every admin page, including this one.
    return Results.Content($$"""
        <!doctype html>
        <html lang="en" data-scheme="standard" data-theme="light">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow"><title>Signed out — Swoovr admin</title>
        <link rel="stylesheet" href="/themes.css"><link rel="stylesheet" href="/admin.css"></head>
        <body><main class="signed-out-page"><p class="admin-brand"><span class="wordmark">swoovr</span><small>Staff workspace</small></p>
        <h1>You're signed out.</h1><p>Your staff session has ended on this device.</p>
        <p><a class="ui-button-primary" href="/sign-in?reauth=true" data-enhance-nav="false" data-sign-in-again>Sign in again</a></p>
        <footer class="admin-version"><small data-app-version="{{Swoovr.Contracts.AppVersion.Current.ProductVersion}}">{{Swoovr.Contracts.AppVersion.Current.DisplayVersion}}</small></footer></main></body></html>
        """, "text/html");
}).AllowAnonymous();
// D189: the one-time evidence package, fetched server-side with the officer's token and handed over as a file.
app.MapGet("/holds/exports/{token}", async (string token, Swoovr.Client.AdminClient admin, CancellationToken cancellationToken) =>
{
    var (status, body) = await admin.DownloadExportAsync(token, cancellationToken);
    return body is null ? Results.Content($"<!doctype html><title>Export unavailable — Swoovr admin</title><p data-export-status=\"{status}\">This export link has expired, was already used, or isn't yours.</p><p><a href=\"/holds\">Legal holds</a></p>", "text/html", statusCode: status == 401 ? 401 : 404)
        : Results.File(body, "application/json", "swoovr-evidence.json");
}).RequireAuthorization();
// D195 (§13): the quarterly audit export, with no case content.
app.MapGet("/law-enforcement/audit-export", async (string? quarter, Swoovr.Client.AdminClient admin, CancellationToken cancellationToken) =>
{
    var (status, body) = await admin.LeAuditExportAsync(quarter ?? "", cancellationToken);
    return body is null ? Results.Content($"<!doctype html><title>Audit export unavailable — Swoovr admin</title><p data-audit-status=\"{status}\">That quarter isn't available to you.</p><p><a href=\"/law-enforcement/transparency\">Back</a></p>", "text/html", statusCode: status is 400 or 401 or 403 ? status : 404)
        : Results.File(body, "application/json", $"swoovr-le-audit-{quarter}.json");
}).RequireAuthorization();
// D203: a review image (and #41: a reported chat photo), only for signed-in staff and only through a link the API issued for a recorded purpose.
app.MapGet("/media/{token}", async (string token, Swoovr.Client.AdminClient admin, CancellationToken cancellationToken) =>
{
    if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^[0-9a-f]{32}\.(review|case|member|inspection|appeal)\.[0-9]{1,12}\.[A-Za-z0-9_-]{43}$")) return Results.NotFound();
    return await admin.MediaAsync("api/media/" + token, cancellationToken) is { } bytes ? Results.File(bytes, "image/jpeg") : Results.NotFound();
}).RequireAuthorization();
// A support request's plain-text attachment, fetched server-side with the staff member's token; the API rechecks access.
app.MapGet("/tickets/{id:long}/articles/{article:long}/attachments/{attachment:long}", async (long id, long article, long attachment, IHttpClientFactory clients, CancellationToken cancellationToken) =>
{
    var result = await Swoovr.Admin.AdminTransfers.Create(clients).TicketAttachmentAsync(id, article, attachment, cancellationToken);
    // Issue 220: the type the API derived from the checked file (members may now send photos, documents, video and audio).
    if (result.Ok) return Results.File(result.Value!.Data, result.Value.ContentType == "text/plain" ? "text/plain; charset=utf-8" : result.Value.ContentType, result.Value.Filename);
    // D336: a file the malware check didn't clear (or can't check now) says so, instead of a bare "not found".
    return result.Status is 409 or 503 ? Results.Text(Swoovr.Client.SupportTicketText.Problem(result.Status, result.Code), "text/plain; charset=utf-8", statusCode: result.Status) : Results.NotFound();
}).RequireAuthorization();
app.MapGet("/code-reports/{year:int}/export",async(int year,Swoovr.Client.AdminClient admin,CancellationToken token)=>
{
    var(status,body)=await admin.ModerationReportExportAsync(year,token);
    return body is null?Results.StatusCode(status):Results.File(body,"application/json",$"swoovr-code-report-{year}.json");
}).RequireAuthorization();
app.MapRazorComponents<App>().RequireAuthorization();
app.Run();
