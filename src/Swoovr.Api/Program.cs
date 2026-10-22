using Swoovr.Contracts;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/v1/status", () => new FoundationStatus("Swoovr.Api", "foundation"));
Swoovr.Api.Prototype.PrototypeDiscovery.Map(app);
Swoovr.Api.Prototype.PrototypeConversations.Map(app);
app.Run();
