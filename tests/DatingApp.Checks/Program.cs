using System.Net;
using System.Text;
using DatingApp.Client;
using DatingApp.Contracts;
using System.Text.Json;

AccessPolicyChecks.Run();
DiscoveryPolicyChecks.Run();
AppearanceSyncChecks.Run();

var expected = new FoundationStatus("DatingApp.Api", "foundation");
using var http = new HttpClient(new StatusHandler()) { BaseAddress = new Uri("https://example.invalid/") };
var actual = await new FoundationClient(http).GetStatusAsync();
if (actual != expected) throw new Exception("Typed client contract mismatch.");
using var empty = new HttpClient(new StatusHandler(empty: true)) { BaseAddress = http.BaseAddress };
try { await new FoundationClient(empty).GetStatusAsync(); throw new Exception("Expected empty response rejection."); }
catch (InvalidOperationException) { }
Console.WriteLine("PASS: typed client route, JSON contract and empty response handling.");

sealed class StatusHandler(bool empty = false) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri?.AbsolutePath != "/api/v1/status")
            throw new Exception("Unexpected API request.");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(empty ? "null" : JsonSerializer.Serialize(new FoundationStatus("DatingApp.Api", "foundation")), Encoding.UTF8, "application/json")
        });
    }
}
