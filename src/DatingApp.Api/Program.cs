using DatingApp.Contracts;
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/api/v1/status", () => new FoundationStatus("DatingApp.Api", "foundation"));
DatingApp.Api.Prototype.PrototypeDiscovery.Map(app);
app.Run();
