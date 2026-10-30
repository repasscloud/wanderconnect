using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swoovr.Api;
using System.Net;
using System.Text;
using System.Text.Json;
using Swoovr.Infrastructure.Email;

static class MailerSendChecks
{
    public static async Task Run(Action<string, bool> check)
    {
        var count=0;var report=check;check=(name,ok)=>{count++;report(name,ok);};
        Registration(check);
        var message = new EmailMessage("fictional@example.test", "Fictional subject", "Private fictional body");
        var options = new MailerSendOptions("fictional-token-never-log", "Swoovr <sender@example.test>");
        var calls = 0;
        using var client = new HttpClient(new Fake(async (request, token) =>
        {
            calls++;
            check("MailerSend canonical HTTPS endpoint", request.Method == HttpMethod.Post && request.RequestUri!.AbsoluteUri == "https://api.mailersend.com/v1/email");
            check("MailerSend bearer token", request.Headers.Authorization?.Scheme == "Bearer" && request.Headers.Authorization.Parameter == "fictional-token-never-log");
            check("MailerSend JSON media type", request.Content!.Headers.ContentType?.MediaType == "application/json");
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            var root = payload.RootElement;
            check("MailerSend actual payload sender", root.GetProperty("from").GetProperty("email").GetString() == "sender@example.test" && root.GetProperty("from").GetProperty("name").GetString() == "Swoovr");
            check("MailerSend actual payload recipient/body", root.GetProperty("to")[0].GetProperty("email").GetString() == message.To && root.GetProperty("subject").GetString() == message.Subject && root.GetProperty("text").GetString() == message.Text);
            return Response(202, "malformed body deliberately unread", "fictional-message-123");
        }));
        var sender = new MailerSendEmailSender(client, options);
        check("MailerSend202 acknowledgement acceptance only", (await sender.SendOutcomeAsync(message, default)).Status == "accepted");
        check("MailerSend sends exactly once", calls == 1);
        check("MailerSend options redact all runtime material", options.ToString() == "MailerSendOptions(redacted)");
        foreach (var (status, expected) in new[] { (200, "unknown"), (201, "unknown"), (204, "unknown"), (302, "unknown"), (400, "rejected"), (401, "rejected"), (403, "rejected"), (404, "rejected"), (405, "rejected"), (408, "unknown"), (421, "unknown"), (422, "rejected"), (429, "unknown"), (500, "unknown"), (502, "unknown"), (503, "unknown"), (504, "unknown") })
        {
            var attempts = 0;
            using var http = new HttpClient(new Fake((_, _) => { attempts++; return Task.FromResult(Response(status, "provider-private-error-" + message.To)); }));
            var result = await new MailerSendEmailSender(http, options).SendOutcomeAsync(message, default);
            check("MailerSend explicit outcome for HTTP" + status, result.Status == expected && attempts == 1);
            check("MailerSend fixed error codes do not reflect provider body/destination", !result.ToString().Contains(message.To, StringComparison.Ordinal));
        }
        foreach (var (body, expected) in new[] { ("", "unknown"), ("not-json", "unknown"), ("[]", "unknown"), ("{\"warnings\":[{\"type\":\"ALL_SUPPRESSED\"}]}", "rejected"), ("{\"warnings\":[{\"type\":\"SOME_SUPPRESSED\"}]}", "unknown") })
        {
            using var http = new HttpClient(new Fake((_, _) => Task.FromResult(Response(202, body))));
            check("MailerSend202 missing acknowledgement/suppression classification", (await new MailerSendEmailSender(http, options).SendOutcomeAsync(message, default)).Status == expected);
        }
        using (var http = new HttpClient(new Fake((_, _) =>
        {
            var response = Response(202, "", "fictional-message");
            response.Headers.Add("x-send-paused", "true");
            return Task.FromResult(response);
        })))
            check("MailerSend paused acceptance is explicitly distinct from delivery", (await new MailerSendEmailSender(http, options).SendOutcomeAsync(message, default)).Code == "provider-paused");
        using (var http = new HttpClient(new Fake((_, _) => Task.FromResult(Response(202, "", "invalid message id")))))
            check("MailerSend malformed acknowledgement stays unknown", (await new MailerSendEmailSender(http, options).SendOutcomeAsync(message, default)).Status == "unknown");
        var stream = new CountingStream(new byte[100000]);
        using (var http = new HttpClient(new Fake((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted) { Content = new UnboundedContent(stream) }))))
        {
            check("MailerSend oversized warning response stays unknown", (await new MailerSendEmailSender(http, options).SendOutcomeAsync(message, default)).Status == "unknown");
            check("MailerSend body consumption bounded at8193 bytes", stream.ReadBytes <= 8193);
        }
        using (var http = new HttpClient(new Fake((_, _) => throw new HttpRequestException("fictional-token-never-log " + message.To))))
        {
            var outcome = await new MailerSendEmailSender(http, options).SendOutcomeAsync(message, default);
            check("MailerSend network-after-possible-effect unknown with no exception leakage", outcome == new EmailOutcome("unknown", "unconfirmed"));
        }
        using (var http = new HttpClient(new Fake(async (_, token) => { await Task.Delay(Timeout.InfiniteTimeSpan, token); return Response(202); })))
            check("MailerSend finite deadline unknown rather than retry", (await new MailerSendEmailSender(http, new MailerSendOptions("fictional", "sender@example.test", TimeSpan.FromMilliseconds(10))).SendOutcomeAsync(message, default)).Status == "unknown");
        var neverCalls = 0;
        using (var http = new HttpClient(new Fake((_, _) => { neverCalls++; return Task.FromResult(Response(202)); })))
        {
            var local = new MailerSendEmailSender(http, options);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            check("MailerSend pre-send cancellation reports known no effect", (await local.SendOutcomeAsync(message, cancel.Token)).Code == "cancelled-before-send" && neverCalls == 0);
            check("MailerSend malformed recipient rejected locally", (await local.SendOutcomeAsync(message with { To = "invalid\r\naddress" }, default)).Status == "rejected" && neverCalls == 0);
            check("MailerSend large message rejected before request allocation", (await local.SendOutcomeAsync(message with { Text = new string('x', 65537) }, default)).Status == "rejected" && neverCalls == 0);
        }
        var config = new Dictionary<string, string?>();
        string? Read(string key) => config.GetValueOrDefault(key);
        check("MailerSend selector defaults off without credentials", MailerSendOptions.ReadConfiguration(Read) is null);
        config["Email:Provider"] = "mailersend";
        try { MailerSendOptions.ReadConfiguration(Read); check("MailerSend missing config refused", false); }
        catch (ArgumentException error) { check("MailerSend missing config fixed safe message", !error.Message.Contains("fictional-token", StringComparison.Ordinal)); }
        config["Email:MailerSend:ApiToken"] = "fictional-token"; config["Email:From"] = "Swoovr <sender@example.test>";
        check("MailerSend explicit selector and resolved config", MailerSendOptions.ReadConfiguration(Read) is not null);
        config["Email:MailerSend:TimeoutSeconds"] = "infinite-secret-value";
        try { MailerSendOptions.ReadConfiguration(Read); check("MailerSend invalid deadline refused", false); }
        catch (InvalidOperationException error) { check("MailerSend invalid config does not reflect its value", !error.Message.Contains("infinite-secret-value", StringComparison.Ordinal)); }
        config["Email:Provider"] = "disabled";
        check("MailerSend disabled ignores unused credentials/timeout", MailerSendOptions.ReadConfiguration(Read) is null);
        Console.WriteLine($"PASS: MailerSend application adapter ({count} fake HTTP/configuration checks; no external request or delivery evidence).");
    }

    private static void Registration(Action<string,bool> check)
    {
        IConfiguration Config(Dictionary<string,string?> values)=>new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        foreach(var development in new[]{false,true})
        {
            var services=new ServiceCollection();services.AddLogging();services.AddSwoovrApplicationEmail(Config(new()),development);using var provider=services.BuildServiceProvider();
            check("Email no selector/credentials is disabled",provider.GetRequiredService<IEmailSender>() is UnconfiguredEmailSender);
        }
        var local=new Dictionary<string,string?>{{"Email:Smtp:Host","127.0.0.1"},{"Email:Smtp:Port","1025"}};
        {var services=new ServiceCollection();services.AddLogging();services.AddSwoovrApplicationEmail(Config(local),true);using var provider=services.BuildServiceProvider();check("Development legacy Mailpit preserved",provider.GetRequiredService<IEmailSender>() is SmtpEmailSender);}
        local["Email:Provider"]="disabled";
        {var services=new ServiceCollection();services.AddLogging();services.AddSwoovrApplicationEmail(Config(local),true);using var provider=services.BuildServiceProvider();check("Explicit disabled never falls back to Mailpit",provider.GetRequiredService<IEmailSender>() is UnconfiguredEmailSender);}
        foreach(var selected in new[]{"smtp","fictional-unknown","mailersend"})
        {
            local["Email:Provider"]=selected;
            try{new ServiceCollection().AddSwoovrApplicationEmail(Config(local),false);check("Unsafe/missing selected provider refuses bootstrap",false);}
            catch(Exception error)when(error is ArgumentException or InvalidOperationException){check("Unsafe/missing provider never falls back",true);}
        }
        local["Email:Provider"]="mailersend";local["Email:MailerSend:ApiToken"]="fictional-never-live";local["Email:From"]="sender@example.test";
        {var services=new ServiceCollection();services.AddLogging();services.AddSwoovrApplicationEmail(Config(local),false);using var provider=services.BuildServiceProvider();check("Explicit hosted MailerSend resolves actual adapter",provider.GetRequiredService<IEmailSender>() is MailerSendEmailSender);}
    }

    private static HttpResponseMessage Response(int status, string body = "", string? id = null)
    {
        var response = new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        if (id is not null) response.Headers.TryAddWithoutValidation("x-message-id", id);
        return response;
    }
    private sealed class Fake(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request, cancellationToken);
    }
    private sealed class CountingStream(byte[] bytes) : MemoryStream(bytes)
    {
        public int ReadBytes { get; private set; }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = await base.ReadAsync(buffer, cancellationToken); ReadBytes += count; return count;
        }
    }
    private sealed class UnboundedContent(Stream stream) : HttpContent
    {
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
        protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) => throw new InvalidOperationException("Response must be streamed, never buffered.");
        protected override Task<Stream> CreateContentReadStreamAsync() => Task.FromResult(stream);
    }
}
