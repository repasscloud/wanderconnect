using Swoovr.Client;
using Swoovr.Contracts;
using System.Net;
using System.Net.Http.Json;

static class PrototypeConversationClientChecks
{
    public static async Task Run()
    {
        var id=Guid.Parse("51000000-0000-0000-0000-000000000001");
        var snapshot=new PrototypeConversation(true,id,0,"None","alex",false,false,[]);
        var calls=0;
        using var http=new HttpClient(new Handler(async request=>{
            calls++;
            if(calls==1)
            {
                if(request.Method!=HttpMethod.Post || request.RequestUri!.AbsolutePath!="/api/v1/prototype/conversations" || (await request.Content!.ReadFromJsonAsync<PrototypeConversationCreate>())?.Travel!="local")throw new Exception("Create transport mismatch");
                return snapshot;
            }
            if(calls==2)
            {
                if(request.Method!=HttpMethod.Get || request.RequestUri!.PathAndQuery!=$"/api/v1/prototype/conversations/{id}?actor=jamie")throw new Exception("Get transport mismatch");
                return snapshot with {Actor="jamie"};
            }
            if(request.Method!=HttpMethod.Post || request.RequestUri!.AbsolutePath!=$"/api/v1/prototype/conversations/{id}/actions" || (await request.Content!.ReadFromJsonAsync<PrototypeConversationAction>()) is not {Actor:"alex",Action:"request",Revision:0})throw new Exception("Action transport mismatch");
            return snapshot with {Revision=1,State="Pending",CanRead=true,CanReport=true,Messages=[new("Alex","Fictional message: would you like to meet for coffee?")]};
        })){BaseAddress=new Uri("https://example.invalid/")};
        var client=new PrototypeConversationClient(http);
        _=await client.CreateAsync("local");_=await client.GetAsync(id,"jamie");_=await client.ActAsync(id,new("alex","request",0));
        if(calls!=3)throw new Exception("Scenario calls missing");
        foreach(var malformed in new[]{snapshot with {IsSimulation=false},snapshot with {Id=Guid.NewGuid()},snapshot with {Actor="admin"},snapshot with {State="Live"},snapshot with {Revision=-1},snapshot with {Messages=null!},snapshot with {Messages=[new("Alex","Private text")]},snapshot with {CanRead=true}})
        {
            using var badHttp=new HttpClient(new Handler(_=>Task.FromResult(malformed))){BaseAddress=http.BaseAddress};
            try{_=await new PrototypeConversationClient(badHttp).GetAsync(id,"alex");throw new Exception("Malformed conversation accepted");}
            catch(InvalidOperationException){}
        }
        Console.WriteLine("PASS: fictional conversation typed methods and bound simulation-only response validation.");
    }
    private sealed class Handler(Func<HttpRequestMessage,Task<PrototypeConversation>> action):HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>
            new(HttpStatusCode.OK){Content=JsonContent.Create(await action(request))};
    }
}
