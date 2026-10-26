using Swoovr.Web.Auth;
using Swoovr.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Static SSR for public and sign-in pages; signed-in features as Interactive WebAssembly only (D59).
builder.Services.AddRazorComponents().AddInteractiveWebAssemblyComponents();
builder.Services.AddHttpClient<Swoovr.Client.PrototypeClient>(http => {
    http.BaseAddress = new Uri(builder.Configuration["Prototype:ApiBaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<Swoovr.Client.SessionClient>(http => {
    http.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromSeconds(5);
});
builder.Services.AddMemberSignIn(builder.Configuration, builder.Environment);
builder.Services.AddBff(builder.Configuration);
// D143: realtime messages reach the browser through the web host's relay; the member's token stays here.
builder.Services.AddSignalR();
builder.Services.AddSingleton<Swoovr.Web.Auth.MessagesRelays>();

builder.Services.AddHttpClient<Swoovr.Client.PrototypeConversationClient>(http => {
    http.BaseAddress = new Uri(builder.Configuration["Prototype:ApiBaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromSeconds(5);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAntiforgery();
// D160: restricted, suspended and terminated members see the restricted screen instead of member pages.
app.UseRestrictionGate();

app.MapStaticAssets();
MemberSignIn.MapMemberSignIn(app);
Bff.MapBff(app);
// The hub checks the signed-in member itself, as the BFF does (this host runs UseAuthentication without UseAuthorization).
app.MapHub<Swoovr.Web.Auth.MessagesRelayHub>("/bff/hubs/messages");
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Swoovr.Web.Client._Imports).Assembly);

app.Run();
