extern alias AdminHost;
using System.Globalization;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using StaffSessionRenewal = AdminHost::Swoovr.Admin.StaffSessionRenewal;

static class StaffSessionRenewalChecks
{
    public static async Task Run()
    {
        var count = 0;
        void Check(bool condition, string name) { if (!condition) throw new Exception("staff renewal: " + name); count++; }
        using var services = new ServiceCollection().AddLogging().AddAuthentication("Cookies").AddCookie().Services.BuildServiceProvider();
        var scopes = new List<IServiceScope>();
        CookieValidatePrincipalContext Context(AuthenticationProperties properties)
        {
            var scope = services.CreateScope(); scopes.Add(scope);
            var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
            var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("auth_time", "123"), new Claim("amr", "pwd")], "oidc"));
            return new(http, new AuthenticationScheme("Cookies", null, typeof(CookieAuthenticationHandler)), new CookieAuthenticationOptions(), new(principal, properties, "Cookies"));
        }
        AuthenticationProperties Properties(string? expiry)
        {
            var result = new AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow.AddHours(-1), ExpiresUtc = DateTimeOffset.UtcNow.AddHours(7) };
            result.StoreTokens([new() { Name = "access_token", Value = "old-access" }, new() { Name = "refresh_token", Value = "synthetic-refresh" }, new() { Name = "expires_at", Value = expiry ?? "" }]);
            return result;
        }
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var store = new AdminHost::Swoovr.Admin.TicketStore(cache);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity("oidc")), Properties(DateTimeOffset.UtcNow.AddMinutes(5).ToString("o")), "Cookies");
        var key = await store.StoreAsync(ticket);
        Check(await store.RetrieveAsync(key) is not null, "new ticket can be retrieved");
        await store.RemoveAsync(key);
        await store.RenewAsync(key, ticket);
        Check(await store.RetrieveAsync(key) is null, "late renewal cannot resurrect a signed-out ticket");
        var handler = new Provider();
        var oidc = new OpenIdConnectOptions { ClientId = "staff", ClientSecret = "synthetic", Backchannel = new HttpClient(handler), ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(new() { TokenEndpoint = "https://provider.example.invalid/token" }) };
        var renewal = new StaffSessionRenewal(cache, new Monitor(oidc));
        var valid = Context(Properties(DateTimeOffset.UtcNow.AddMinutes(5).ToString("o")));
        await renewal.ValidateAsync(valid);
        Check(handler.Calls == 0 && !valid.ShouldRenew && valid.Principal is not null, "unexpired token requires no provider request");
        var props = Properties(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o"));
        var deadline = props.ExpiresUtc;
        var contexts = Enumerable.Range(0, 8).Select(_ => Context(props)).ToArray();
        await Task.WhenAll(contexts.Select(renewal.ValidateAsync));
        Check(handler.Calls == 1, "concurrent requests share one refresh rotation");
        Check(props.GetTokenValue("access_token") == "synthetic-access" && props.GetTokenValue("refresh_token") == "synthetic-next", "both server-held tokens updated");
        Check(props.ExpiresUtc == deadline && props.IssuedUtc > DateTimeOffset.UtcNow.AddSeconds(-5), "renewal preserves absolute deadline");
        Check(contexts.All(c => c.Principal?.FindFirst("auth_time")?.Value == "123" && c.Principal.FindFirst("amr")?.Value == "pwd"), "renewal does not invent fresh authentication or change method");
        foreach (var invalid in new string?[] { null, "not-a-date" })
        {
            var context = Context(Properties(invalid)); await renewal.ValidateAsync(context);
            Check(context.Principal?.Identity?.IsAuthenticated != true && context.HttpContext.Response.Headers.SetCookie.Count > 0, "missing or malformed expiry clears local session");
        }
        using var failureCache = new MemoryCache(new MemoryCacheOptions());
        handler.Reject = true;
        var rejected = Context(Properties(DateTimeOffset.UtcNow.AddMinutes(-1).ToString("o", CultureInfo.InvariantCulture)));
        await new StaffSessionRenewal(failureCache, new Monitor(oidc)).ValidateAsync(rejected);
        Check(rejected.Principal?.Identity?.IsAuthenticated != true && !rejected.ShouldRenew, "provider refusal cannot renew a staff session");
        foreach (var scope in scopes) scope.Dispose();
        Console.WriteLine($"PASS: staff session renewal ({count} checks; fake provider concurrency, absolute deadline, freshness and fail-closed cases).");
    }
    sealed class Provider : HttpMessageHandler
    {
        public int Calls; public bool Reject;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Interlocked.Increment(ref Calls); await Task.Delay(20, token);
            return Reject ? new(HttpStatusCode.BadRequest) : new(HttpStatusCode.OK) { Content = new StringContent("{\"access_token\":\"synthetic-access\",\"refresh_token\":\"synthetic-next\",\"expires_in\":300}") };
        }
    }
    sealed class Monitor(OpenIdConnectOptions value) : IOptionsMonitor<OpenIdConnectOptions>
    {
        public OpenIdConnectOptions CurrentValue => value;
        public OpenIdConnectOptions Get(string? name) => value;
        public IDisposable? OnChange(Action<OpenIdConnectOptions, string?> listener) => null;
    }
}
