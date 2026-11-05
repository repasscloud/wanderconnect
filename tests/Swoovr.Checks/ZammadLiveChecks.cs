using System.Text.Json;
using Swoovr.Domain;
using Swoovr.Infrastructure.Ticketing;

internal static class ZammadLiveChecks
{
    public static async Task Run(string directory)
    {
        using var config = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "provisioned.json")));
        using var fixtures = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(directory, "fixtures.json")));
        var root = config.RootElement;
        var rows = fixtures.RootElement;
        using var handler = new SocketsHttpHandler {AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(5)};
        using var identityHttp = new HttpClient(handler, false);
        using var supportHttp = new HttpClient(handler, false);
        using var customerHttp = new HttpClient(handler, false);
        var identity = new ZammadTicketingProvider(identityHttp, new Uri("http://127.0.0.1:8095/"), root.GetProperty("identity_token").GetString()!);
        var support = new ZammadTicketingProvider(supportHttp, new Uri("http://127.0.0.1:8095/"), rows.GetProperty("support").GetProperty("token").GetString()!);
        var customer = new ZammadTicketingProvider(customerHttp, new Uri("http://127.0.0.1:8095/"), rows.GetProperty("customer_a").GetProperty("token").GetString()!);
        var who = await identity.FindCustomerAsync("swoovr-aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa", default) ?? throw new Exception("customer lookup");
        var operation = Guid.NewGuid().ToString();
        var created = await support.CreateTicketAsync(new("Fictional typed transport enquiry", "Ordinary fictional support message.", who.Id,
            root.GetProperty("groups").GetProperty("General Support").GetInt64(), operation), default);
        var read = await customer.GetTicketAsync(created.Id, default) ?? throw new Exception("customer detail");
        if (read.CustomerId != who.Id) throw new Exception("customer identity mismatch");
        await support.ReplyAsync(created.Id, "Private note through typed transport.", true, false, default);
        await support.ReplyAsync(created.Id, "Public reply through typed transport.", false, false, default);
        var articles = await customer.GetArticlesAsync(created.Id, default);
        if (articles.Any(a => a.Internal) || !articles.Any(a => a.Body == "Public reply through typed transport.")) throw new Exception("visibility");
        var oldEmail = who.Email;
        await identity.UpdateCustomerAsync(who.Id, "changed-customer-a@example.test", true, default);
        var after = await identity.FindCustomerAsync(who.Login, default) ?? throw new Exception("identity after email change");
        if (after.Id != who.Id || (await customer.GetTicketAsync(created.Id, default))?.Id != created.Id) throw new Exception("email changed identity");
        await identity.UpdateCustomerAsync(who.Id, oldEmail, true, default);
        var found = await support.SearchAsync("swoovr_operation:" + operation, 1, 10, default);
        // Elasticsearch may index asynchronously; record API/search acceptance separately from immediate consistency.
        Console.WriteLine($"PASS: real Zammad typed customer lookup/create/detail/public/private replies and email continuity; immediate indexed matches: {found.Count}.");
    }
}
