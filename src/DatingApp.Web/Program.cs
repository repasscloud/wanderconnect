using DatingApp.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();
builder.Services.AddHttpClient<DatingApp.Client.PrototypeClient>(http => {
    http.BaseAddress = new Uri(builder.Configuration["Prototype:ApiBaseUrl"] ?? "http://127.0.0.1:5080/");
    http.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient<DatingApp.Client.PrototypeConversationClient>(http => {
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

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
