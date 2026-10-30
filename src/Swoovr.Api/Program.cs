using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api;
using Swoovr.Api.Accounts;
using Swoovr.Api.Auth;
using Swoovr.Contracts;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;
if (args.Length > 0 && !args[0].StartsWith('-')) return await Cli.RunAsync(args);
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
var persistence = builder.Services.AddSwoovrPersistence(builder.Configuration.GetConnectionString("Swoovr"));
builder.Services.AddScoped<AccountResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Founders>();
// D147, D169: Didit and the members realm's deletion client, from configuration only (locally .local/docker.env).
builder.Services.AddSwoovrProviders(builder.Configuration);
// D201, D203: photo storage (R2), screening (image-moderation-api) and the quarantine pipeline.
Swoovr.Api.Media.MediaSetup.AddSwoovrMedia(builder.Services, builder.Configuration, builder.Environment, persistence);
builder.Services.AddSwoovrEmail(builder.Configuration["Email:Smtp:Host"], builder.Configuration.GetValue("Email:Smtp:Port", 0),
    builder.Configuration["Email:From"] ?? "Swoovr <no-reply@swoovr.local>");
// #49: travel reminders, written by the API's own worker (with a database).
if (persistence) builder.Services.AddHostedService<Swoovr.Api.Notifications.NotificationWorker>();
if (persistence && builder.Configuration.GetValue("MemberAdmin:ContactRetirementEnabled",false)
    && builder.Configuration.GetValue("MemberAdmin:ContactRetirementWorker",true))
    builder.Services.AddHostedService<EmailRetirementWorker>();
// Synthetic backdated Development fixtures are opt-in; hosted runs sweep hourly with durable due queries.
if (persistence && builder.Configuration.GetValue("Retention:Worker", !builder.Environment.IsDevelopment()))
    builder.Services.AddHostedService<Swoovr.Api.Retention.RetentionWorker>();
builder.Services.AddRateLimiter(options=>{options.RejectionStatusCode=429;options.AddPolicy("meeting-safety-public",http=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(http.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=600,Window=TimeSpan.FromMinutes(1),QueueLimit=0}));});
if(persistence && builder.Configuration.GetValue("MeetingSafety:Enabled",false) && builder.Configuration.GetValue("MeetingSafety:Worker",true)) builder.Services.AddHostedService<Swoovr.Api.Safety.MeetingSafetyWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
var app = builder.Build();
// Fail at startup/command bootstrap if configured push is unsafe or incomplete.
app.Services.GetRequiredService<Swoovr.Infrastructure.Messaging.IPushSender>();
using (var pushScope = app.Services.CreateScope())
    pushScope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PushKeys>();
// Do not start hosted senders, cleanup jobs, migrations or ingress against quarantined restored data.
if(persistence)
{
    await using var restoreScope=app.Services.CreateAsyncScope();
    if(!await Swoovr.Api.Retention.RestoreReadiness.ReadyAsync(restoreScope.ServiceProvider.GetRequiredService<SwoovrDbContext>(),CancellationToken.None))
        throw new InvalidOperationException("Restored database is fenced or readiness is unavailable. Complete operator reconciliation before starting hosts.");
}
if(persistence&&app.Configuration.GetValue("MeetingSafety:Enabled",false))
{
    using var meetingScope=app.Services.CreateScope();
    _=Swoovr.Api.Safety.MeetingSafetyDelivery.PublicBase(app.Configuration,app.Environment);
    var contactKeys=meetingScope.ServiceProvider.GetRequiredService<ContactHistoryKeys>();
    if(contactKeys.CurrentVersion is null||contactKeys.Lookup is not{Length:32})throw new InvalidOperationException("Meeting safety requires configured contact encryption and lookup keys.");
    if(!app.Environment.IsDevelopment()&&(app.Services.GetRequiredService<IEmailSender>() is SmtpEmailSender or UnconfiguredEmailSender||!app.Configuration.GetValue("MeetingSafety:Worker",true)))throw new InvalidOperationException("Hosted meeting safety requires a real configured email sender and server scheduling/delivery workers.");
}
// DEFAULT: development applies migrations on start; other environments run `migrate` first.
if (persistence && app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
    await db.Database.MigrateAsync();
    await Swoovr.Api.Notifications.PushDeviceMaintenance.ProtectLegacyAsync(db, scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PushKeys>(), CancellationToken.None);
}
app.Use(async(context,next)=>{if(context.Request.Path.StartsWithSegments("/api/safety/meeting-contact")||context.Request.Path.StartsWithSegments("/api/me/meeting-safety")){context.Response.Headers.CacheControl="no-store";context.Response.Headers["Referrer-Policy"]="no-referrer";context.Response.Headers["X-Robots-Tag"]="noindex, nofollow, noarchive";}await next();});
app.UseMiddleware<ApiVersionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<IdentifierCheckMiddleware>();
// Wrap authorization and handlers so refused/failed sensitive requests are recorded too.
app.UseMiddleware<Swoovr.Api.Accounts.SensitiveAccessMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<MemberMutationAuthority>();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/status", () => new FoundationStatus("Swoovr.Api", "foundation"));
AccountEndpoints.Map(app);
DeletionEndpoints.Map(app);
PhoneEndpoints.Map(app);
ContactHistoryEndpoints.Map(app);
RegistryAdminEndpoints.Map(app);
StaffAccessEndpoints.Map(app);
Swoovr.Api.Security.SecurityEndpoints.Map(app);
Swoovr.Api.Evidence.LegalHoldEndpoints.Map(app);
Swoovr.Api.Evidence.LawEnforcementEndpoints.Map(app);
Swoovr.Api.Verification.IdentityEndpoints.Map(app);
AdminEndpoints.Map(app);
Swoovr.Api.Profiles.ProfileEndpoints.Map(app);
Swoovr.Api.Profiles.MarketEndpoints.Map(app);
Swoovr.Api.Connections.RequestEndpoints.Map(app);
Swoovr.Api.Travel.TravelEndpoints.Map(app);
Swoovr.Api.Travel.ArrivalEndpoints.Map(app);
Swoovr.Api.Connections.MessagingEndpoints.Map(app);
Swoovr.Api.Verification.VerificationEndpoints.Map(app);
Swoovr.Api.Moderation.ModerationEndpoints.Map(app);
if(persistence) Swoovr.Api.Moderation.ContentRemovalEndpoints.Map(app);
Swoovr.Api.Moderation.StandingEndpoints.Map(app);
Swoovr.Api.Profiles.MatchingEndpoints.Map(app);
Swoovr.Api.Profiles.ProfilePrivacy.Map(app);
Swoovr.Api.Media.MediaEndpoints.Map(app);
Swoovr.Api.Safety.SafetyEndpoints.Map(app);
if(persistence) Swoovr.Api.Safety.MeetingSafetyEndpoints.Map(app);
Swoovr.Api.Notifications.NotificationEndpoints.Map(app);
Swoovr.Api.Prototype.PrototypeDiscovery.Map(app);
Swoovr.Api.Prototype.PrototypeConversations.Map(app);
app.Run();
return 0;
