using System.Net;
using Microsoft.Extensions.Configuration;
using Swoovr.Api.Ticketing;
using Swoovr.Client;
using Swoovr.Domain;

/// <summary>Guest code/grant policy and the optional bot check, with a fake HTTP verifier (no Cloudflare request).</summary>
internal static class TicketingGuestChecks
{
    private sealed class Fake(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        { Calls++; return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) }); }
    }
    private sealed class Clients(HttpMessageHandler handler) : IHttpClientFactory { public HttpClient CreateClient(string name) => new(handler, false); }

    public static async Task Run()
    {
        var n = 0;
        void Check(bool ok, string name) { if (!ok) throw new Exception(name); n++; }
        var key = new byte[32]; var now = DateTimeOffset.UnixEpoch.AddYears(56);
        TicketingGuestChallenge Make(string code) { var c = new TicketingGuestChallenge { Id = Guid.NewGuid(), CreatedAt = now, ExpiresAt = now + TicketingGuestPolicy.CodeLifetime }; c.CodeHash = TicketingGuestPolicy.CodeHash(key, c.Id, code); return c; }
        var code = TicketingGuestPolicy.Code();
        Check(TicketingGuestPolicy.CodeShape(code) && !TicketingGuestPolicy.CodeShape("1234567") && !TicketingGuestPolicy.CodeShape("1234567a"), "eight digit codes only");
        var ok = Make(code);
        Check(TicketingGuestPolicy.Check(ok, key, code, now) && !TicketingGuestPolicy.Check(ok, key, code, now), "a code works once");
        var tried = Make(code);
        for (var i = 0; i < TicketingGuestPolicy.MaximumFailures; i++) TicketingGuestPolicy.Check(tried, key, "00000000" == code ? "11111111" : "00000000", now);
        Check(!TicketingGuestPolicy.Check(tried, key, code, now), "five wrong codes end the challenge");
        var old = Make(code); old.SupersededAt = now;
        Check(!TicketingGuestPolicy.Check(old, key, code, now), "a superseded code fails");
        Check(!TicketingGuestPolicy.Check(Make(code), key, code, now + TicketingGuestPolicy.CodeLifetime), "an expired code fails");
        var other = new byte[32]; other[0] = 1;
        Check(!TicketingGuestPolicy.EmailLookup(key, "a@example.test").SequenceEqual(TicketingGuestPolicy.EmailLookup(other, "a@example.test")), "address lookups are keyed");
        Check(TicketingGuestPolicy.Login(Guid.Empty) != TicketingPolicy.MemberLogin(Guid.Empty), "guest and member helpdesk logins never coincide");
        Check(TicketingGuestPolicy.Categories.Count == 8 && TicketingGuestPolicy.Categories.Values.All(TicketingPolicy.Groups.Contains)
            && !TicketingGuestPolicy.Categories.Values.Contains("Legal & Law Enforcement") && !TicketingGuestPolicy.Categories.Values.Contains("Trust & Safety — Restricted"),
            "public categories route only to known, non-restricted queues");
        Check(TicketingGuestPolicy.TokenShape(TicketingGuestPolicy.Token()) && !TicketingGuestPolicy.TokenShape("short"), "grant shape");

        IConfiguration Config(string? secret) => new ConfigurationBuilder().AddInMemoryCollection(secret is null ? [] : [new("Ticketing:Guest:Turnstile:Secret", secret)]).Build();
        var unused = new Fake(HttpStatusCode.OK, "{}");
        Check(await new GuestBotCheck(Config(null), new Clients(unused)).PassesAsync(null, null, default) && unused.Calls == 0, "no bot secret: no check made and none claimed");
        var yes = new Fake(HttpStatusCode.OK, "{\"success\":true}");
        Check(await new GuestBotCheck(Config("fictional-secret"), new Clients(yes)).PassesAsync("token", "203.0.113.1", default) && yes.Calls == 1, "configured: verifier success passes");
        var no = new Fake(HttpStatusCode.OK, "{\"success\":false}");
        Check(!await new GuestBotCheck(Config("fictional-secret"), new Clients(no)).PassesAsync("token", null, default), "configured: verifier refusal fails");
        Check(!await new GuestBotCheck(Config("fictional-secret"), new Clients(yes)).PassesAsync(null, null, default), "configured: missing token fails");
        var down = new Fake(HttpStatusCode.BadGateway, "");
        Check(!await new GuestBotCheck(Config("fictional-secret"), new Clients(down)).PassesAsync("token", null, default), "configured: unreachable verifier fails closed");
        Check(SupportTicketText.ValidReference("SW-ABC234") && !SupportTicketText.ValidReference("SW-ABC23I") && SupportTicketText.NewLink("<x>") == "/me/support/new", "support links carry only valid report references");
        Console.WriteLine($"PASS: ticket guest code/grant policy, public categories and optional bot check ({n} checks; fake verifier, no Cloudflare request).");
    }
}
