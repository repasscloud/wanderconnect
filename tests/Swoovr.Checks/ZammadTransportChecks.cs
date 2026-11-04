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
        Console.WriteLine("PASS: Zammad transport paging, denial, uncertain writes without retry and required article visibility.");
    }

    private sealed class Handler : HttpMessageHandler
    {
        public string Body = "";
        public string? Path;
        public HttpStatusCode Status = HttpStatusCode.OK;
        public int Calls;
        public CancellationTokenSource? CancelAfterApply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (CancelAfterApply is { } cancellation) { cancellation.Cancel(); throw new OperationCanceledException(cancellationToken); }
            Path = request.RequestUri!.PathAndQuery;
            if (request.Headers.Authorization?.Scheme != "Token") throw new Exception("provider auth missing");
            return Task.FromResult(new HttpResponseMessage(Status) {Content = new StringContent(Body, Encoding.UTF8, "application/json")});
        }
    }
}
