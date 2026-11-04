using System.Text;
using Swoovr.Api.Ticketing;

internal static class TicketingWebhookChecks
{
    public static void Run()
    {
        var publicEvent = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":\"42\"},\"article\":{\"id\":\"123\"}}"));
        if (publicEvent != (42L, (long?)123L)) throw new Exception("ids-only webhook parsed");
        var railsClock = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"id\":42,\"updated_at\":\"2026-11-04 10:19:50 UTC\"},\"article\":{\"id\":123}}"));
        if (railsClock != (42L, (long?)123L)) throw new Exception("actual Zammad interpolated UTC timestamp");
        var noArticle = TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes("{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":42},\"article\":null}"));
        if (noArticle != (42L, (long?)null)) throw new Exception("article-free changes preserved");
        foreach (var body in new[] {"{}", "{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":0}}", "{\"ticket\":{\"updated_at\":\"2026-11-04T00:00:00Z\",\"id\":42},\"article\":{\"id\":-1}}", "[]"})
            if (TicketingWebhookEndpoints.Parse(Encoding.UTF8.GetBytes(body)) is not null) throw new Exception("invalid event rejected");
        Console.WriteLine("PASS: ids-only webhook schema and article-free changes.");
    }
}
