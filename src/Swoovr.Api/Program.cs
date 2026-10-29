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
// Synthetic backdated Development fixtures are opt-in; hosted runs sweep hourly with durable due queries.
if (persistence && builder.Configuration.GetValue("Retention:Worker", !builder.Environment.IsDevelopment()))
    builder.Services.AddHostedService<Swoovr.Api.Retention.RetentionWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
var app = builder.Build();
// Fail at startup/command bootstrap if configured push is unsafe or incomplete.
app.Services.GetRequiredService<Swoovr.Infrastructure.Messaging.IPushSender>();
using (var pushScope = app.Services.CreateScope())
    pushScope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PushKeys>();
// DEFAULT: development applies migrations on start; other environments run `migrate` first.
if (persistence && app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
    await db.Database.MigrateAsync();
    await Swoovr.Api.Notifications.PushDeviceMaintenance.ProtectLegacyAsync(db, scope.ServiceProvider.GetRequiredService<Swoovr.Api.Notifications.PushKeys>(), CancellationToken.None);
}
app.UseMiddleware<ApiVersionMiddleware>();
app.UseAuthentication();
app.UseMiddleware<IdentifierCheckMiddleware>();
// Wrap authorization and handlers so refused/failed sensitive requests are recorded too.
app.UseMiddleware<Swoovr.Api.Accounts.SensitiveAccessMiddleware>();
app.UseAuthorization();
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
Swoovr.Api.Moderation.StandingEndpoints.Map(app);
Swoovr.Api.Profiles.MatchingEndpoints.Map(app);
Swoovr.Api.Profiles.ProfilePrivacy.Map(app);
Swoovr.Api.Media.MediaEndpoints.Map(app);
Swoovr.Api.Safety.SafetyEndpoints.Map(app);
Swoovr.Api.Notifications.NotificationEndpoints.Map(app);
Swoovr.Api.Prototype.PrototypeDiscovery.Map(app);
Swoovr.Api.Prototype.PrototypeConversations.Map(app);
app.Run();
return 0;
