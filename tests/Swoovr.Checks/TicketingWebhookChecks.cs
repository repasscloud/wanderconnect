using System.Text;
using Swoovr.Api.Ticketing;
using Swoovr.Domain;

internal static class TicketingWebhookChecks
{
    public static void Run()
    {
        var publicEvent = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":\"42\"},\"article\":{\"id\":\"123\"}}"));
        if (publicEvent != (42L, (long?)123L, (long?)null)) throw new Exception("ids-only webhook parsed");
        var railsClock = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"id\":42,\"updated_at\":\"2026-11-04 10:19:50 UTC\"},\"article\":{\"id\":123}}"));
        if (railsClock != (42L, (long?)123L, (long?)null)) throw new Exception("actual Zammad interpolated UTC timestamp");
        var noArticle = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":42},\"article\":null}"));
        if (noArticle != (42L, (long?)null, (long?)null)) throw new Exception("article-free changes preserved");
        var withCustomer = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"id\":\"42\",\"updated_at\":\"2026-11-04T00:00:00Z\",\"customer_id\":\"7\"},\"article\":{\"id\":\"123\"}}"));
        if (withCustomer != (42L, (long?)123L, (long?)7L)) throw new Exception("signed customer id selects the re-fetch actor");
        if (SupportNotice.Ticket(SupportNotice.Subject(9_000_000_001)) != 9_000_000_001 || SupportNotice.Ticket(Guid.NewGuid()) is not null) throw new Exception("support notice subject round trip");
        if (new SupportArticle(1, 1, "", "text/plain", false, "Customer", default, []) { Type = "email" }.CustomerVisible
            || !new SupportArticle(1, 1, "", "text/plain", false, "Agent", default, []) { Type = "email" }.CustomerVisible
            || new SupportArticle(1, 1, "", "text/plain", true, "Agent", default, []) { Type = "note" }.CustomerVisible) throw new Exception("customer visibility: no internal notes, no unverified email senders");
        foreach (var body in new[] {"{}", "{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":42,\"customer_id\":0}}", "{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":0}}", "{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":42},\"article\":{\"id\":-1}}", "[]"})
            if (TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes(body)) is not null) throw new Exception("invalid event rejected");
        Console.WriteLine("PASS: ids-only webhook schema, signed customer id, article-free changes, notice subjects and customer visibility.");
    }
}
