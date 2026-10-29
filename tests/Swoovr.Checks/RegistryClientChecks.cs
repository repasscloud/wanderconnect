using System.Net;
using System.Text.Json;
using System.Net.Http.Json;
using Swoovr.Client;
using Swoovr.Contracts;

/// <summary>Restricted metadata transport: no contact in URLs, real continuation, separate release/reinstatement bodies.</summary>
static class RegistryClientChecks
{
    public static async Task Run()
    {
        var count=0;void Check(bool condition,string message){if(!condition)throw new Exception("Registry client: "+message);count++;}
        var now=DateTimeOffset.UtcNow;var id=Guid.NewGuid();var review=Guid.NewGuid();var account=Guid.NewGuid();var next=Guid.NewGuid();
        var entry=new BannedIdentifierView(id,"email",review,"permanent-account-ban","active",now,null,null,now.AddMonths(6),null,0,null,null);
        using var handler=new Handler(entry,account,next);using var http=new HttpClient(handler){BaseAddress=new("https://api.example.test/")};var client=new AdminClient(http);
        var page=await client.RegistryAsync(due:true,after:review);
        Check(page.Ok&&page.Value!.Items.Single().Id==id&&page.Value.NextCursor==next,"array plus continuation retained");
        Check(handler.Method==HttpMethod.Get&&handler.Path.Contains("due=true")&&handler.Path.Contains(review.ToString()),"opaque paging filters only");
        var found=await client.RegistryLookupAsync(new IdentifierLookup("email","Fictional.Lookup@example.test","Recorded access purpose"));
        Check(found.Ok&&found.Value!.Count==1,"lookup returns metadata");
        Check(handler.Method==HttpMethod.Post&&handler.Path=="/api/admin/banned-identifiers/lookup"&&handler.Body.Contains("Fictional.Lookup@example.test"),"lookup contact is in POST body only");
        Check(!handler.Path.Contains("@")&&!JsonSerializer.Serialize(found.Value).Contains("IdentifierHash"),"no raw value/hash in URL/result");
        var detail=await client.RegistryEntryAsync(id);Check(detail.Ok&&detail.Value!.Entry.CaseId==review,"detail preserves associated case");
        await client.RegistryReviewAsync(id,"Retained following fictional necessity review");Check(handler.Path.EndsWith(id+"/review")&&handler.Body.Contains("necessity"),"review reason preserved");
        await client.RegistryReleaseAsync(new IdentifierReleaseRequest([id],"Fictional ownership review completed"));
        Check(handler.Path=="/api/admin/banned-identifiers/release"&&handler.Body.Contains(id.ToString()),"identifier release uses explicit selected IDs");
        var preview=await client.ReinstatementAsync(account);Check(preview.Ok&&preview.Value!.Identifiers.Single().Id==id,"server reinstatement preview preserves complete source");
        await client.ReinstateAsync(account,new AccountReinstatementRequest([id],"Fictional independent account review"));
        Check(handler.Path==$"/api/admin/accounts/{account}/reinstate"&&handler.Body.Contains("revokeIdentifierIds"),"account reinstatement separate from release");
        handler.Forbidden=true;var denied=await client.RegistryEntryAsync(id);Check(denied.Status==403&&denied.Code=="elevated-access-required"&&denied.Value is null,"refusal cannot look like empty success");
        handler.Forbidden=false;handler.BadCursor=true;var rejected=false;
        try{await client.RegistryAsync();}catch(InvalidOperationException){rejected=true;}
        Check(rejected,"malformed continuation cannot silently truncate registry");
        Check(handler.Version==ApiContract.Initial,"dated stable API contract used");
        Console.WriteLine($"PASS: registry staff client ({count} checks; fake HTTP metadata, no live authorization claim).");
    }
    private sealed class Handler(BannedIdentifierView entry,Guid account,Guid next):HttpMessageHandler
    {
        public string Path="",Body="",Version="";public HttpMethod? Method;public bool Forbidden,BadCursor;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Path=request.RequestUri!.PathAndQuery;Method=request.Method;Body=request.Content is null?"":await request.Content.ReadAsStringAsync(token);
            Version=string.Join(",",request.Headers.GetValues(ApiContract.Header));
            if(Forbidden)return new(HttpStatusCode.Forbidden){Content=JsonContent.Create(new{code="elevated-access-required"})};
            object body=Path.Contains("/lookup")?new[]{entry}:Path.Contains("/reinstatement")?new AccountReinstatementPreview(account,"active",true,null,[entry.CaseId],[entry],true,[entry.Id])
                :Path.Contains("/reinstate")||Path.EndsWith("/release")?new IdentifierWorkflowResult(1)
                :Path.EndsWith("/review")?entry:Path.Contains(entry.Id.ToString())?new BannedIdentifierDetail(entry,[],[]):new[]{entry};
            var response=new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(body,body.GetType())};
            if(Path.StartsWith("/api/admin/banned-identifiers?"))response.Headers.Add("Swoovr-Next-Cursor",BadCursor?"invalid":next.ToString());
            return response;
        }
    }
}
