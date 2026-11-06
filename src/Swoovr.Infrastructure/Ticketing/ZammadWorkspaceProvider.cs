using System.Net.Http.Headers;
using System.Text.Json;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Ticketing;

public sealed partial class ZammadTicketingProvider
{
    public async Task<IReadOnlyList<SupportActor>> AgentsAsync(CancellationToken ct)
    {
        var roles=await Send(HttpMethod.Get,"roles",null,ct);
        var role=roles.EnumerateArray().Single(e=>Text(e,"name")=="Agent").GetProperty("id").GetInt64();
        var users=new List<SupportActor>();
        for(var page=1;page<=100;page++)
        {
            var rows=await Send(HttpMethod.Get,$"users/search?query=role_ids:{role}&expand=true&page={page}&per_page=100",null,ct);
            users.AddRange(rows.EnumerateArray().Select(Actor).Where(a=>a.Active&&a.Roles.Count==1&&a.Roles[0]=="Agent"));
            if(rows.GetArrayLength()<100)return users;
        }
        throw new TicketingProviderException("provider-directory-too-large");
    }
    public async Task<SupportActor> ActorAsync(CancellationToken ct)=>Actor(await Send(HttpMethod.Get,"users/me?expand=true",null,ct));
    public async Task<SupportActor> UserAsync(long id,CancellationToken ct)=>Actor(await Send(HttpMethod.Get,$"users/{Id(id)}?expand=true",null,ct));
    private static SupportActor Actor(JsonElement e)=>Read(()=>new SupportActor(e.GetProperty("id").GetInt64(),Text(e,"login"),e.GetProperty("active").GetBoolean(),
        e.GetProperty("roles").EnumerateArray().Select(r=>r.GetString()!).ToArray(),e.GetProperty("group_ids").EnumerateObject().ToDictionary(p=>long.Parse(p.Name,System.Globalization.CultureInfo.InvariantCulture),p=>(IReadOnlyList<string>)p.Value.EnumerateArray().Select(v=>v.GetString()!).ToArray()),e.TryGetProperty("email",out var email)&&email.ValueKind==JsonValueKind.String?email.GetString():null));
    public async Task<SupportDirectory> DirectoryAsync(CancellationToken ct)
    {
        async Task<SupportOption[]> Options(string path)=> (await Send(HttpMethod.Get,path,null,ct)).EnumerateArray()
            .Where(e=>!e.TryGetProperty("active",out var active)||active.GetBoolean()).Select(e=>new SupportOption(e.GetProperty("id").GetInt64(),Text(e,"name"),e.TryGetProperty("state_type",out var kind)&&kind.ValueKind==JsonValueKind.String?kind.GetString():null,e.TryGetProperty("follow_up_possible",out var follow)&&follow.ValueKind==JsonValueKind.String?follow.GetString():null)).ToArray();
        return new(await Options("groups"),await Options("ticket_states?expand=true"),await Options("ticket_priorities"));
    }
    public async Task<IReadOnlyList<SupportTicket>> SearchAsync(string query,int page,int pageSize,string sort,bool descending,CancellationToken ct)
    {
        if(page is <1 or >10000||pageSize is <1 or >100||query.Length>1000||sort is not("updated_at" or "created_at" or "title" or "priority_id" or "state_id" or "number"))throw new ArgumentException("Invalid ticket search.");
        var value=await Send(HttpMethod.Get,$"tickets/search?query={Uri.EscapeDataString(query)}&page={page}&per_page={pageSize}&sort_by={sort}&order_by={(descending?"desc":"asc")}",null,ct);
        return value.EnumerateArray().Select(Ticket).ToArray();
    }
    public async Task<SupportTicket> ClassifyAsync(long id,IReadOnlyDictionary<string,string> attributes,CancellationToken ct)
    {
        if(attributes.Count is <1 or >5||attributes.Any(a=>a.Key is not("swoovr_category" or "swoovr_subcategory" or "swoovr_sensitivity" or "swoovr_resolution")||a.Value.Length>80||a.Value.Any(char.IsControl)))throw new ArgumentException("Invalid classification.");
        return ReadWrite(Ticket,await Send(HttpMethod.Put,$"tickets/{Id(id)}",attributes,ct));
    }
    public async Task<SupportArticle> ReplyWithOperationAsync(long id,string body,bool internalNote,bool customer,IReadOnlyList<SupportUpload> attachments,string operationReference,CancellationToken ct)
    {
        ValidateText(body,16000);
        if(!Guid.TryParseExact(operationReference,"D",out _)||internalNote&&customer)throw new ArgumentException("Invalid support article.");
        CheckUploads(attachments);
        return ReadWrite(Article,await Send(HttpMethod.Post,"ticket_articles",new{ticket_id=Id(id),body,type="note",content_type="text/plain",@internal=internalNote,sender=customer?"Customer":"Agent",message_id=$"swoovr-operation-{operationReference}@swoovr.invalid",preferences=new{swoovr_operation=operationReference},
            attachments=Uploads(attachments)},ct,slow:attachments.Count>0));
    }
    public async Task<SupportArticle> ReplyAsync(long id,string body,bool internalNote,bool customer,IReadOnlyList<SupportUpload> attachments,CancellationToken ct)
    {
        ValidateText(body,16000);
        if(internalNote&&customer)throw new ArgumentException("Invalid support article.");
        CheckUploads(attachments);
        return ReadWrite(Article,await Send(HttpMethod.Post,"ticket_articles",new{ticket_id=Id(id),body,type="note",content_type="text/plain",@internal=internalNote,sender=customer?"Customer":"Agent",
            attachments=Uploads(attachments)},ct,slow:attachments.Count>0));
    }
    public async Task<SupportDownload> DownloadAsync(long ticket,long article,long attachment,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,new Uri(root,$"ticket_attachment/{Id(ticket)}/{Id(article)}/{Id(attachment)}"));
        request.Headers.Authorization=new AuthenticationHeaderValue("Token","token="+token);
        if(from is long actor)request.Headers.TryAddWithoutValidation("From",actor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        // Issue 220: files up to the largest allowed (100 MB), streamed with our own cap instead of the client's 4 MB buffer.
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(UploadTimeout);
        try
        {
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,limit.Token);
            if(!response.IsSuccessStatusCode)throw new TicketingProviderException((int)response.StatusCode is 401 or 403 or 404?"provider-forbidden":"provider-unavailable");
            if(response.Content.Headers.ContentLength>MaxUploadBytes)throw new TicketingProviderException("attachment-too-large");
            await using var stream=await response.Content.ReadAsStreamAsync(limit.Token);
            using var memory=new MemoryStream();
            var buffer=new byte[81920];int read;
            while((read=await stream.ReadAsync(buffer,limit.Token))>0)
            {
                if(memory.Length+read>MaxUploadBytes)throw new TicketingProviderException("attachment-too-large");
                memory.Write(buffer,0,read);
            }
            return new(memory.ToArray(),response.Content.Headers.ContentType?.MediaType??"application/octet-stream");
        }
        catch(HttpRequestException){throw new TicketingProviderException("provider-unavailable");}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new TicketingProviderException("provider-timeout");}
    }
    public async Task<IReadOnlyList<SupportTextModule>> TextModulesAsync(CancellationToken ct)=>
        (await Send(HttpMethod.Get,"text_modules?per_page=100",null,ct)).EnumerateArray().Where(e=>e.GetProperty("active").GetBoolean())
            .Select(e=>new SupportTextModule(e.GetProperty("id").GetInt64(),Text(e,"name"),Text(e,"content"))).ToArray();
    public async Task<IReadOnlyList<SupportMacro>> MacrosAsync(CancellationToken ct)=>
        (await Send(HttpMethod.Get,"macros?per_page=100",null,ct)).EnumerateArray().Where(e=>e.GetProperty("active").GetBoolean()).Select(e=>
        {
            var p=e.GetProperty("perform");
            long? Value(string k)
            {
                if(!p.TryGetProperty(k,out var v))return null;
                if(v.ValueKind==JsonValueKind.Object)
                {
                    if(v.TryGetProperty("pre_condition",out var condition)&&!string.IsNullOrEmpty(condition.GetString()))return null;
                    if(!v.TryGetProperty("value",out v))return null;
                }
                return v.ValueKind==JsonValueKind.Number?v.GetInt64():v.ValueKind==JsonValueKind.String&&long.TryParse(v.GetString(),out var n)?n:null;
            }
            var supported=p.EnumerateObject().All(a=>a.Name is "ticket.group_id" or "ticket.owner_id" or "ticket.state_id" or "ticket.priority_id")&&p.EnumerateObject().Any()&&p.EnumerateObject().All(a=>Value(a.Name)>0);
            return new SupportMacro(e.GetProperty("id").GetInt64(),Text(e,"name"),new(Value("ticket.group_id"),Value("ticket.owner_id"),Value("ticket.state_id"),Value("ticket.priority_id")),supported);
        }).ToArray();
}
