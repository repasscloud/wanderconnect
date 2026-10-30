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
builder.Services.AddRazorComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<TicketStore>();
builder.Services.AddTransient<StaffTokenHandler>();
builder.Services.AddHttpClient<Swoovr.Client.AdminClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromSeconds(10);
}).AddHttpMessageHandler<StaffTokenHandler>();

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
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/error", createScopeForErrors: true); app.UseHsts(); }
app.Use(async(context,next)=>
{
    if(context.Request.Path.StartsWithSegments("/code-reports"))context.Response.Headers.CacheControl="no-store";
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
app.MapPost("/sign-out", async (HttpContext context) =>
{
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/signed-out");
}).AllowAnonymous();
app.MapGet("/signed-out", () => Results.Content("<!doctype html><title>Signed out — Swoovr admin</title><p>You're signed out. <a href=\"/\">Sign in again</a></p>", "text/html")).AllowAnonymous();
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
    if (!System.Text.RegularExpressions.Regex.IsMatch(token, @"^[0-9a-f]{32}\.(review|case)\.[0-9]{1,12}\.[A-Za-z0-9_-]{43}$")) return Results.NotFound();
    return await admin.MediaAsync("api/media/" + token, cancellationToken) is { } bytes ? Results.File(bytes, "image/jpeg") : Results.NotFound();
}).RequireAuthorization();
app.MapGet("/code-reports/{year:int}/export",async(int year,Swoovr.Client.AdminClient admin,CancellationToken token)=>
{
    var(status,body)=await admin.ModerationReportExportAsync(year,token);
    return body is null?Results.StatusCode(status):Results.File(body,"application/json",$"swoovr-code-report-{year}.json");
}).RequireAuthorization();
app.MapRazorComponents<App>().RequireAuthorization();
app.Run();
