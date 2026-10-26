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
builder.Services.AddScoped<Founders>();
// D147: Didit settings from configuration only (locally .local/docker.env through the host environment).
var didit = new Swoovr.Infrastructure.Verification.DiditOptions(builder.Configuration["Didit:ApiKey"], builder.Configuration["Didit:WorkflowId"],
    builder.Configuration["Didit:WebhookSecret"], new Uri(builder.Configuration["Didit:BaseUrl"] ?? "https://verification.didit.me/"));
builder.Services.AddSingleton(didit);
builder.Services.AddHttpClient<Swoovr.Domain.IIdVerificationProvider, Swoovr.Infrastructure.Verification.DiditClient>(http => http.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSwoovrEmail(builder.Configuration["Email:Smtp:Host"], builder.Configuration.GetValue("Email:Smtp:Port", 0),
    builder.Configuration["Email:From"] ?? "Swoovr <no-reply@swoovr.local>");
builder.Services.AddProblemDetails();
builder.Services.AddSignalR();
var app = builder.Build();
// DEFAULT: development applies migrations on start; other environments run `migrate` first.
if (persistence && app.Environment.IsDevelopment())
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<SwoovrDbContext>().Database.MigrateAsync();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/v1/status", () => new FoundationStatus("Swoovr.Api", "foundation"));
AccountEndpoints.Map(app);
AdminEndpoints.Map(app);
Swoovr.Api.Profiles.ProfileEndpoints.Map(app);
Swoovr.Api.Profiles.MarketEndpoints.Map(app);
Swoovr.Api.Connections.RequestEndpoints.Map(app);
Swoovr.Api.Travel.TravelEndpoints.Map(app);
Swoovr.Api.Travel.ArrivalEndpoints.Map(app);
Swoovr.Api.Connections.MessagingEndpoints.Map(app);
Swoovr.Api.Verification.VerificationEndpoints.Map(app);
Swoovr.Api.Moderation.ModerationEndpoints.Map(app);
Swoovr.Api.Profiles.MatchingEndpoints.Map(app);
Swoovr.Api.Prototype.PrototypeDiscovery.Map(app);
Swoovr.Api.Prototype.PrototypeConversations.Map(app);
app.Run();
return 0;
