using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Swoovr.Client;
using Swoovr.Web.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
// Same-origin calls to the web host's backend-for-frontend; the browser sends the session
// cookie and the host adds the API token server-side. No token ever reaches this code.
builder.Services.AddScoped(_ => new MemberClient(new HttpClient(new BffHandler { InnerHandler = new HttpClientHandler() })
{
    BaseAddress = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "bff/"),
}));
await builder.Build().RunAsync();
