using System.Text.Json;
using Swoovr.Contracts;
using Swoovr.Domain;

namespace Swoovr.Api.Prototype;

// No provider or application storage. All IDs/status/location/actors below are FICTIONAL.
internal static class PrototypeConversations
{
    private static readonly Guid Alex=Guid.Parse("41000000-0000-0000-0000-000000000001");
    private static readonly Guid Jamie=Guid.Parse("41000000-0000-0000-0000-000000000002");
    private static readonly Guid Area=Guid.Parse("41000000-0000-0000-0000-000000000003");
    private sealed class Scenario(string travel)
    {
        public string Travel {get;}=travel;
        public DateTimeOffset Expires {get;}=DateTimeOffset.UtcNow.AddMinutes(20);
        public ConnectionThread Thread {get;set;}=ConnectionThread.Empty(Alex,Jamie)!;
        public long Revision {get;set;}
        public bool Blocked {get;set;}
    }
    private static readonly AccessSession Session=new(AccountRole.Member,SignInMethod.EmailPassword,false,true,new HashSet<Guid>());
    private static DiscoveryMember Member(Guid id)=>new(id,true,true,true,true,
        new HashSet<DiscoveryContext>(Enum.GetValues<DiscoveryContext>()),new HashSet<ConnectionIntention>{ConnectionIntention.Friendship});
    private static DiscoveryRules Rules()=>new(true,true,true,false,
        new(new HashSet<ConnectionIntention>(),RequiredIntentionMatch.Any,new HashSet<ConnectionIntention>()));
    private static ConnectionAccess Access(Scenario scenario,Guid actor)=>new(Session,actor,Member(Alex),Member(Jamie),scenario.Blocked,false);
    private static DiscoveryAccessRequest Contact(Scenario scenario,Guid actor,DateTimeOffset now)=>new(Session,actor,
        Member(actor),Member(actor==Alex ? Jamie : Alex),Rules(),Rules(),
        scenario.Travel=="local" ? DiscoveryContext.Friends : DiscoveryContext.Travel,DiscoveryResource.Contact,
        false,scenario.Blocked,true,true,false,scenario.Travel!="local",Area,
        scenario.Travel=="arrived" ? new(actor,Area,now.AddSeconds(-1),now.AddMinutes(1),true,true) : null);
    private static Guid Actor(string? value)=>value switch {"alex"=>Alex,"jamie"=>Jamie,_=>Guid.Empty};
    private static PrototypeConversation Project(Guid id,Scenario scenario,string actor)
    {
        var access=Access(scenario,Actor(actor));
        var read=scenario.Thread.CanRead(access);
        return new(true,id,scenario.Revision,scenario.Blocked ? "Unavailable" : scenario.Thread.State.ToString(),actor,
            read,scenario.Thread.CanReport(access),read ? scenario.Thread.Messages.Select(m=>new PrototypeConversationMessage(m.SenderId==Alex ? "Alex" : "Jamie",m.Text)).ToArray() : []);
    }
    private sealed class InvalidBody(int status):Exception {public int Status {get;}=status;}
    private static async Task<JsonDocument> Body(HttpRequest request,string[] keys)
    {
        if(request.Query.Count!=0 || !request.HasJsonContentType())throw new InvalidBody(400);
        if(request.ContentLength>16384)throw new InvalidBody(413);
        // Explicit streaming bound also covers a chunked request with no Content-Length.
        var bytes=new byte[16385];var total=0;
        while(total<bytes.Length)
        {
            var read=await request.Body.ReadAsync(bytes.AsMemory(total),request.HttpContext.RequestAborted);
            if(read==0)break;total+=read;
        }
        if(total>16384)throw new InvalidBody(413);
        JsonDocument document;
        try {document=JsonDocument.Parse(bytes.AsMemory(0,total));}
        catch(JsonException) {throw new InvalidBody(400);}
        if(document.RootElement.ValueKind!=JsonValueKind.Object){document.Dispose();throw new InvalidBody(400);}
        var names=document.RootElement.EnumerateObject().Select(p=>p.Name).ToArray();
        if(names.Length!=keys.Length || names.Distinct(StringComparer.Ordinal).Count()!=names.Length || names.Except(keys,StringComparer.Ordinal).Any())
        {document.Dispose();throw new InvalidBody(400);}
        return document;
    }
    private static string? String(JsonElement value,string name)=>value.GetProperty(name) is {ValueKind:JsonValueKind.String} property ? property.GetString() : null;
    public static void Map(WebApplication app)
    {
        if(!app.Environment.IsDevelopment() || !app.Configuration.GetValue<bool>("Prototype:Enabled"))return;
        var scenarios=new Dictionary<Guid,Scenario>();var gate=new object();
        void Expire(){foreach(var id in scenarios.Where(p=>p.Value.Expires<=DateTimeOffset.UtcNow).Select(p=>p.Key).ToArray())scenarios.Remove(id);}
        var group=app.MapGroup("/api/v1/prototype/conversations");
        group.AddEndpointFilter(async(context,next)=>{
            context.HttpContext.Response.Headers.CacheControl="no-store";
            context.HttpContext.Response.Headers.XContentTypeOptions="nosniff";
            try {return await next(context);}catch(InvalidBody error){return Results.StatusCode(error.Status);}
        });
        group.MapPost("",async(HttpRequest request)=>{
            using var body=await Body(request,["travel"]);
            var travel=String(body.RootElement,"travel");
            if(travel is not ("local" or "upcoming" or "arrived"))return Results.BadRequest();
            lock(gate)
            {
                Expire();if(scenarios.Count>=32)return Results.StatusCode(429);
                var id=Guid.NewGuid();var scenario=new Scenario(travel);scenarios.Add(id,scenario);
                return Results.Created($"/api/v1/prototype/conversations/{id}?actor=alex",Project(id,scenario,"alex"));
            }
        });
        group.MapGet("/{id:guid}",(Guid id,HttpRequest request)=>{
            if(request.Query.Count!=1 || request.Query["actor"].Count!=1)return Results.BadRequest();
            var actor=request.Query["actor"].ToString();if(Actor(actor)==Guid.Empty)return Results.BadRequest();
            lock(gate){Expire();return scenarios.TryGetValue(id,out var scenario) ? Results.Ok(Project(id,scenario,actor)) : Results.NotFound();}
        });
        group.MapPost("/{id:guid}/actions",async(Guid id,HttpRequest request)=>{
            using var body=await Body(request,["actor","action","revision"]);
            var actor=String(body.RootElement,"actor");var action=String(body.RootElement,"action");
            var revision=body.RootElement.GetProperty("revision");
            if(Actor(actor)==Guid.Empty || action is not ("request" or "accept" or "decline" or "send" or "close" or "block")
                || revision.ValueKind!=JsonValueKind.Number || !revision.TryGetInt64(out var expected) || expected<0)return Results.BadRequest();
            lock(gate)
            {
                Expire();if(!scenarios.TryGetValue(id,out var scenario))return Results.NotFound();
                if(expected!=scenario.Revision || scenario.Revision==long.MaxValue)return Results.Conflict();
                if(action=="block")
                {
                    if(scenario.Blocked)return Results.Conflict();scenario.Blocked=true;
                }
                else
                {
                    var now=DateTimeOffset.UtcNow;var access=Access(scenario,Actor(actor));
                    var thread=scenario.Thread;
                    if(action=="send" && thread.Messages.Count>=64)return Results.Conflict();
                    var next=action switch {
                        "request"=>thread.TryRequest(Contact(scenario,Actor(actor),now),thread.Revision,"Fictional message: would you like to meet for coffee?",now),
                        "accept"=>thread.TryAccept(Contact(scenario,Actor(actor),now),thread.Revision,now),
                        "decline"=>thread.TryDecline(access,thread.Revision),
                        "send"=>thread.TrySend(access,thread.Revision,"Fictional reply: coffee sounds good. Let's choose a public place.",now),
                        "close"=>thread.TryClose(access,thread.Revision),_=>null};
                    if(next is null)return Results.Conflict();scenario.Thread=next;
                }
                scenario.Revision++;return Results.Ok(Project(id,scenario,actor!));
            }
        });
    }
}
