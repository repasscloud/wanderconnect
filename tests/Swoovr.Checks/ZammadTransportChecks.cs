using System.Net;
using System.Text;
using Swoovr.Domain;
using Swoovr.Infrastructure.Ticketing;

internal static class ZammadTransportChecks
{
    public static async Task Run()
    {
        var handler = new Handler();
        using var http = new HttpClient(handler);
        var provider = new ZammadTicketingProvider(http, new Uri("http://localhost:8095/"), "private-token");
        handler.Body = "[]";
        await provider.SearchAsync("customer_id:42", 2, 25, default);
        if (handler.Path != "/api/v1/tickets/search?query=customer_id%3A42&page=2&per_page=25&sort_by=updated_at&order_by=desc") throw new Exception("bounded search and paging");
        handler.Status = HttpStatusCode.Forbidden;
        try { await provider.GetTicketAsync(42, default); throw new Exception("expected denial"); }
        catch (TicketingProviderException error) when (error.Code == "provider-forbidden" && !error.Uncertain) { }
        handler.Status = HttpStatusCode.ServiceUnavailable;
        try { await provider.ReplyAsync(42, "hello", false, false, default); throw new Exception("expected uncertainty"); }
        catch (TicketingProviderException error) when (error.Uncertain) { }
        if (handler.Calls != 3) throw new Exception("writes never automatically retried");
        handler.Status = HttpStatusCode.OK;
        handler.Body = "[{\"id\":1,\"ticket_id\":42,\"body\":\"secret\",\"content_type\":\"text/plain\",\"sender\":\"Agent\",\"created_at\":\"2026-11-04T00:00:00Z\",\"attachments\":[]}]";
        try { await provider.GetArticlesAsync(42, default); throw new Exception("missing visibility must fail closed"); }
        catch (TicketingProviderException error) when (error.Code == "provider-schema") { }
        try { await provider.ReplyAsync(42, "hello", false, false, default); throw new Exception("successful malformed write must be uncertain"); }
        catch (TicketingProviderException error) when (error.Uncertain) { }
        using var cancel = new CancellationTokenSource();
        handler.CancelAfterApply = cancel;
        try { await provider.ReplyAsync(42, "cancel after send", false, false, cancel.Token); throw new Exception("expected cancelled uncertainty"); }
        catch (TicketingProviderException error) when (error.Uncertain) { }
        handler.CancelAfterApply=null;
        handler.Body="{\"id\":42,\"login\":\"swoovr-customer\",\"active\":true,\"roles\":[\"Customer\"],\"group_ids\":{}}";
        using var memberHttp=new HttpClient(handler);
        var member=new ZammadTicketingProvider(memberHttp,new Uri("http://localhost:8095/"),"private-token",42);
        var actor=await member.ActorAsync(default);
        if(actor.Id!=42||handler.From!="42"||actor.Roles.Single()!="Customer")throw new Exception("server-bound actor and role facts");
        handler.Body="[]";
        await member.SearchAsync("customer_id:42",1,20,"title",false,default);
        if(!handler.Path!.EndsWith("sort_by=title&order_by=asc"))throw new Exception("explicit safe sorting");
        handler.Body="{\"id\":9,\"ticket_id\":42,\"body\":\"reply\",\"content_type\":\"text/plain\",\"internal\":false,\"sender\":\"Customer\",\"created_at\":\"2026-11-04T00:00:00Z\",\"created_by_id\":42,\"preferences\":{\"swoovr_operation\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"},\"attachments\":[]}";
        var marked=await member.ReplyWithOperationAsync(42,"reply",false,true,[],"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",default);
        if(marked.OperationReference!="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"||marked.AuthorId!=42)throw new Exception("article recovery marker and actor roundtrip");
        handler.Body=handler.Body.Replace("\"attachments\":[]", "\"attachments\":[{\"id\":7,\"filename\":\"ordinary.txt\",\"size\":\"8\",\"preferences\":{\"Mime-Type\":\"text/plain\"}}]");
        var attached=await member.ReplyWithOperationAsync(42,"reply",false,true,[],"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",default);
        if(attached.Attachments.Single().Size!=8||attached.Attachments.Single().ContentType!="text/plain")throw new Exception("actual Zammad attachment schema");
        handler.Body=handler.Body.Replace("\"preferences\":{\"swoovr_operation\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"}","\"preferences\":{},\"message_id\":\"swoovr-operation-aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa@swoovr.invalid\"");
        var customerMarker=await member.ReplyWithOperationAsync(42,"reply",false,true,[],"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",default);
        if(customerMarker.OperationReference!="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")throw new Exception("customer reply operation marker survives provider preference sanitization");
        handler.Body="[{\"id\":1,\"name\":\"close\",\"active\":true,\"perform\":{\"ticket.state_id\":{\"value\":4}}},{\"id\":2,\"name\":\"advanced\",\"active\":true,\"perform\":{\"ticket.tags\":{\"operator\":\"add\",\"value\":\"spam\"}}}]";
        var macros=await provider.MacrosAsync(default);
        if(!macros[0].Supported||macros[0].Change.StateId!=4||macros[1].Supported)throw new Exception("actual macro descriptors and unsupported actions fail closed");
        Console.WriteLine("PASS: Zammad transport paging, denial, uncertain writes without retry and required article visibility.");
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "";
        public string? Path;
        public string? From;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public int Calls;
        public CancellationTokenSource? CancelAfterApply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            From=request.Headers.TryGetValues("From",out var values)?values.Single():null;
            if (CancelAfterApply is { } cancellation) { cancellation.Cancel(); throw new OperationCanceledException(cancellationToken); }
            Path = request.RequestUri!.PathAndQuery;
            if (request.Headers.Authorization?.Scheme != "Token") throw new Exception("provider auth missing");
            return Task.FromResult(new HttpResponseMessage(Status) {Content = new StringContent(Body, Encoding.UTF8, "application/json")});
        }
    }
}
