using Swoovr.Hosting;
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
HostRuntime.Configure(builder,HostKind.Api);
if(builder.Configuration["Ticketing:ConfigurationFile"] is {Length:>0} ticketingConfiguration) builder.Configuration.AddJsonFile(ticketingConfiguration,optional:false,reloadOnChange:false);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
var persistence = builder.Services.AddSwoovrPersistence(builder.Configuration.GetConnectionString("Swoovr"));
builder.Services.AddScoped<AccountResolver>();
builder.Services.AddScoped<Swoovr.Api.Ticketing.TicketingActorFactory>();
// Guest support needs persistence (its endpoints are mapped only with a database).
if(persistence)builder.Services.AddScoped<Swoovr.Api.Ticketing.GuestSupport>();
// Helpdesk events become notices only when the signed webhook is configured; the worker can be turned off per host.
if(persistence)builder.Services.AddScoped<Swoovr.Api.Ticketing.TicketingEventProcessor>();
builder.Services.AddSingleton<Swoovr.Api.Ticketing.TicketingStaffSignal>();
if(persistence)builder.Services.AddScoped<Swoovr.Api.Ticketing.TicketingStaffSync>();
// Swoovr staff permissions are mirrored into Zammad (Q133). Without the staff-directory credential nothing is provisioned.
if(persistence&&builder.Configuration["Ticketing:Zammad:StaffDirectoryToken"] is {Length:>0}&&builder.Configuration.GetValue("Ticketing:Staff:Worker",true))builder.Services.AddHostedService<Swoovr.Api.Ticketing.TicketingStaffSyncWorker>();
if(persistence&&builder.Configuration["Ticketing:Zammad:WebhookSecret"] is {Length:>=32}&&builder.Configuration.GetValue("Ticketing:Events:Worker",true))builder.Services.AddHostedService<Swoovr.Api.Ticketing.TicketingEventWorker>();
builder.Services.AddScoped<Swoovr.Api.Ticketing.GuestBotCheck>();
builder.Services.AddHttpClient(Swoovr.Api.Ticketing.GuestBotCheck.Client,http=>http.Timeout=TimeSpan.FromSeconds(10));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Founders>();
// D147, D169: Didit and the members realm's deletion client, from configuration only (locally .local/docker.env).
builder.Services.AddSwoovrProviders(builder.Configuration);
// D201, D203: photo storage (R2), screening (image-moderation-api) and the quarantine pipeline.
Swoovr.Api.Media.MediaSetup.AddSwoovrMedia(builder.Services, builder.Configuration, builder.Environment, persistence);
// D336 (#221): malware scanning (ClamAV) and quarantine for support attachments.
Swoovr.Api.Ticketing.AttachmentScanningSetup.AddSwoovrAttachmentScanning(builder.Services, builder.Configuration, builder.Environment, persistence);
builder.Services.AddSwoovrApplicationEmail(builder.Configuration,builder.Environment.IsDevelopment());
BetaInvitationDelivery.PublicOrigin(builder.Configuration,builder.Environment.IsDevelopment());
if(persistence&&builder.Configuration.GetValue("Beta:Invitations:Enabled",false))builder.Services.AddHostedService<BetaInvitationWorker>();
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
if(persistence&&builder.Configuration.GetValue("TextHarm:Enabled",false)&&builder.Configuration.GetValue("TextHarm:Worker",true))builder.Services.AddHostedService<Swoovr.Api.Moderation.TextHarmWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
// #198: open messaging connections close when their account's sessions are retired (at once for staff actions, else by the sweep).
builder.Services.AddSingleton<Swoovr.Api.Connections.MessagesConnections>();
if (persistence) builder.Services.AddHostedService<Swoovr.Api.Connections.MessagesSessionSweep>();
var app = builder.Build();
HostRuntime.Use(app,HostKind.Api);
if(app.Services.GetRequiredService<Swoovr.Api.Policies.ApprovedPolicyCatalogue>().Resolve(app.Configuration["Policies:RequiredVersion"],DateTimeOffset.UtcNow).State==Swoovr.Domain.PolicyRequirementState.ConfigurationInvalid)throw new InvalidOperationException("No matching effective approved policy exists for Policies:RequiredVersion.");
// Fail at startup/command bootstrap if configured push is unsafe or incomplete.
app.Services.GetRequiredService<Swoovr.Infrastructure.Messaging.IPushSender>();
Swoovr.Api.Moderation.ModerationTransparencyRecorder.Dataset(app.Environment,app.Configuration);
if(persistence&&builder.Configuration.GetValue("TextHarm:Enabled",false)){using var textScope=app.Services.CreateScope();if(textScope.ServiceProvider.GetRequiredService<Swoovr.Api.Evidence.EvidenceKeys>().CurrentVersion is null)throw new InvalidOperationException("Text review requires configured evidence keys.");}
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
Swoovr.Api.Ticketing.TicketingHosting.Use(app);
app.UseMiddleware<ApiVersionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<IdentifierCheckMiddleware>();
// Wrap authorization and handlers so refused/failed sensitive requests are recorded too.
app.UseMiddleware<Swoovr.Api.Accounts.SensitiveAccessMiddleware>();
app.UseAuthorization();
app.UseRateLimiter();
app.UseMiddleware<MemberMutationAuthority>();
ApiReadiness.Map(app);
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
Swoovr.Api.Ticketing.AttachmentScanningSetup.Map(app);
// Q129 / D320: public, read-only compiled product version (no commit, environment or build time).
app.MapGet("/version", () => Results.Ok(Swoovr.Contracts.VersionView.From(Swoovr.Contracts.AppVersion.Current))).AllowAnonymous();
app.MapGet("/api/status", () => new FoundationStatus("Swoovr.Api", "foundation"));
AccountEndpoints.Map(app);
AdviceAcknowledgements.Map(app);
RegistrationEndpoints.Map(app);
RegisteredNamesEndpoints.Map(app);
DobChangeEndpoints.Map(app);
AccountInspectionEndpoints.Map(app);
AccountControlEndpoints.Map(app);
if(persistence)AppearanceEndpoints.Map(app);
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
if(persistence)Swoovr.Api.Analytics.BetaScorecardEndpoints.Map(app);
Swoovr.Api.Profiles.ProfileEndpoints.Map(app);
Swoovr.Api.Profiles.MarketEndpoints.Map(app);
Swoovr.Api.Connections.RequestEndpoints.Map(app);
Swoovr.Api.Travel.TravelEndpoints.Map(app);
Swoovr.Api.Travel.ArrivalEndpoints.Map(app);
Swoovr.Api.Connections.MessagingEndpoints.Map(app);
Swoovr.Api.Verification.VerificationEndpoints.Map(app);
Swoovr.Api.Moderation.ModerationEndpoints.Map(app);
if(persistence){Swoovr.Api.Policies.PolicyEndpoints.Map(app);Swoovr.Api.Moderation.ContentRemovalEndpoints.Map(app);Swoovr.Api.Moderation.TextHarmEndpoints.Map(app);Swoovr.Api.Moderation.ModerationTransparencyEndpoints.Map(app);}
Swoovr.Api.Moderation.StandingEndpoints.Map(app);
Swoovr.Api.Ticketing.TicketingWebhookEndpoints.Map(app);
Swoovr.Api.Ticketing.TicketingEndpoints.Map(app);
if(persistence)Swoovr.Api.Ticketing.GuestSupportEndpoints.Map(app);
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
