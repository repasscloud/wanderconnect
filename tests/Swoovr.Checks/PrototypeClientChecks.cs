using System.Net;
using System.Text;
using Swoovr.Client;
using Swoovr.Contracts;

static class PrototypeClientChecks
{
    public static async Task Run()
    {
        using var http = new HttpClient(new PreviewHandler()) { BaseAddress = new("https://example.invalid/") };
        var client = new PrototypeClient(http);
        var result = await client.GetPeopleAsync(new("upcoming",true,true,true,"all"));
        if (!result.IsSimulation || result.People.Count != 0) throw new Exception("Prototype client result mismatch");
        using var unsafeHttp = new HttpClient(new PreviewHandler(unsafePayload: true)) { BaseAddress = http.BaseAddress };
        try { await new PrototypeClient(unsafeHttp).GetPeopleAsync(new("upcoming",true,true,true,"all")); throw new Exception("Unlabelled preview accepted"); }
        catch (InvalidOperationException) { }
        Console.WriteLine("PASS: prototype typed route/filter transport and non-simulation payload rejection.");
    }
    private sealed class PreviewHandler(bool unsafePayload = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if (request.RequestUri?.PathAndQuery != "/api/v1/prototype/people?travel=upcoming&incognito=true&hideSparse=true&requireFriendship=true&match=all&requireActivity=false" || request.Method != HttpMethod.Get)
                throw new Exception("Prototype typed route mismatch");
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(unsafePayload
                ? "{\"isSimulation\":false,\"viewerDescription\":\"not a demo\",\"people\":[]}"
                : "{\"isSimulation\":true,\"viewerDescription\":\"Fictional adult\",\"people\":[]}",Encoding.UTF8,"application/json") });
        }
    }
}
