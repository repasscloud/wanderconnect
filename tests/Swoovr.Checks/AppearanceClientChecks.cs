using System.Net;
using System.Net.Http.Json;
using Swoovr.Client;
using Swoovr.Contracts;
static class AppearanceClientChecks
{
    public static async Task Run()
    {
        var owner=new string('a',64);
        HttpResponseMessage Owned(HttpResponseMessage response){response.Headers.Add(AppearanceOwner.Header,owner);return response;}
        var count=0;void Check(bool ok,string name){count++;if(!ok)throw new Exception("Appearance client: "+name);}
        using var http=new HttpClient(new Fake(async request=>
        {
            Check(request.RequestUri!.AbsolutePath=="/api/me/appearance","stable own route");
            Check(request.Headers.TryGetValues(ApiContract.Header,out var versions)&&versions.Single()==ApiContract.Initial,"common date version");
            if(request.Method==HttpMethod.Get)return Owned(new(HttpStatusCode.NoContent));
            Check(request.Headers.GetValues(AppearanceOwner.Header).Single()==owner,"pending write binds original account owner");
            var update=await request.Content!.ReadFromJsonAsync<AppearanceUpdate>();Check(update?.ChangeId!=Guid.Empty,"portable exact retry identity");
            return Owned(new(HttpStatusCode.Conflict){Content=JsonContent.Create(new AppearanceConflict("appearance-conflict",new(new("pride","dark"),long.MaxValue)))});
        })){BaseAddress=new("https://appearance.example.test/")};
        var client=new MemberClient(http);
        var missing=await client.AppearanceAsync();Check(missing.Status==204&&missing.Value==null,"absent preference is not an empty success body error");
        var conflict=await client.SaveAppearanceAsync(new(new("coral","system"),4,Guid.NewGuid()),owner);
        Check(!conflict.Ok&&conflict.Status==409&&conflict.Code=="appearance-conflict"&&conflict.Value?.Revision==long.MaxValue,"409 carries exact64-bit current snapshot without success");
        using var malformedHttp=new HttpClient(new Fake(_=>Task.FromResult(Owned(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("not-json")})))){BaseAddress=new("https://appearance.example.test/")};
        Check((await new MemberClient(malformedHttp).AppearanceAsync()).Code=="invalid-appearance-response","malformed provider projection fails closed");
        using var invalidHttp=new HttpClient(new Fake(_=>Task.FromResult(Owned(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new AppearanceSnapshot(new("standard","system"),0))})))){BaseAddress=new("https://appearance.example.test/")};
        Check((await new MemberClient(invalidHttp).AppearanceAsync()).Status==502,"revision zero cannot become a confirmed baseline");
        Console.WriteLine($"PASS: appearance typed client ({count} fake HTTP checks; no live authorization claim).");
    }
    private sealed class Fake(Func<HttpRequestMessage,Task<HttpResponseMessage>> respond):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>respond(request);}
}
