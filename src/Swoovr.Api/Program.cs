using Microsoft.EntityFrameworkCore;
using Swoovr.Api;
using Swoovr.Api.Accounts;
using Swoovr.Api.Auth;
using Swoovr.Contracts;
using Swoovr.Infrastructure.Persistence;
if (args.Length > 0 && !args[0].StartsWith('-')) return await Cli.RunAsync(args);
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddKeycloakAuthentication(builder.Configuration, builder.Environment);
var persistence = builder.Services.AddSwoovrPersistence(builder.Configuration.GetConnectionString("Swoovr"));
builder.Services.AddScoped<AccountResolver>();
builder.Services.AddProblemDetails();
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
Swoovr.Api.Prototype.PrototypeDiscovery.Map(app);
Swoovr.Api.Prototype.PrototypeConversations.Map(app);
app.Run();
return 0;
