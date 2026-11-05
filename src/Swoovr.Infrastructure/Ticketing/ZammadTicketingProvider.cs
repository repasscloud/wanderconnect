using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Ticketing;

/// <summary>Bounded Zammad 7.2.2 transport. No error bodies or customer text are included in exceptions/logs.
/// Each instance is bound to a resolved actor credential; redirects must be disabled by the composition root.</summary>
public sealed partial class ZammadTicketingProvider : ITicketingWorkspaceProvider
{
    private readonly HttpClient http;
    private readonly Uri root;
    private readonly string token;
    private readonly long? from;

    public ZammadTicketingProvider(HttpClient http, Uri baseUrl, string token, long? from = null)
    {
        if (baseUrl.Scheme != "https" && !(baseUrl.Scheme == "http" && (baseUrl.IsLoopback || baseUrl.Host == "swoovr-zammad")))
            throw new ArgumentException("Zammad requires HTTPS outside the explicit local network.");
        if (!string.IsNullOrEmpty(baseUrl.UserInfo) || !string.IsNullOrEmpty(baseUrl.Query) || !string.IsNullOrEmpty(baseUrl.Fragment))
            throw new ArgumentException("Invalid Zammad base URL.");
        if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsWhiteSpace)) throw new ArgumentException("Invalid provider credential.");
        this.http = http;
        this.root = new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/api/v1/");
        this.token = token;
        this.from = from is null ? null : Id(from.Value);
        http.Timeout = TimeSpan.FromSeconds(20);
        http.MaxResponseContentBufferSize = 4 * 1024 * 1024;
    }

    public async Task<SupportCustomer?> FindCustomerAsync(string canonicalLogin, CancellationToken ct)
    {
        if (canonicalLogin.Length is < 1 or > 180 || canonicalLogin.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '@' and not '.' and not '_'))
            throw new ArgumentException("Invalid canonical login.");
        var result = await Send(HttpMethod.Get, "users/search?query=" + Uri.EscapeDataString("login:\"" + canonicalLogin + "\"") + "&page=1&per_page=10", null, ct);
        var matches = result.EnumerateArray().Where(u => Text(u, "login") == canonicalLogin).Select(Customer).ToArray();
        return matches.Length switch { 0 => null, 1 => matches[0], _ => throw new TicketingProviderException("provider-identity-ambiguous") };
    }

    public async Task<SupportCustomer> CreateCustomerAsync(string canonicalLogin, string? verifiedEmail, CancellationToken ct)
        => ReadWrite(Customer, await Send(HttpMethod.Post, "users", new { login = canonicalLogin, firstname = "Swoovr", lastname = "Customer", email = verifiedEmail,
            roles = new[] { "Customer" }, active = true }, ct));

    public async Task<SupportCustomer> UpdateCustomerAsync(long id, string? verifiedEmail, bool active, CancellationToken ct)
        => ReadWrite(Customer, await Send(HttpMethod.Put, $"users/{Id(id)}", new { email = verifiedEmail, active }, ct));

    public async Task<IReadOnlyList<SupportTicket>> SearchAsync(string query, int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 10000 || pageSize is < 1 or > 100 || query.Length > 1000) throw new ArgumentException("Invalid page or query.");
        var value = await Send(HttpMethod.Get, $"tickets/search?query={Uri.EscapeDataString(query)}&page={page}&per_page={pageSize}&sort_by=updated_at&order_by=desc", null, ct);
        return value.EnumerateArray().Select(Ticket).ToArray();
    }

    public async Task<SupportTicket?> GetTicketAsync(long id, CancellationToken ct)
    {
        try { return Ticket(await Send(HttpMethod.Get, $"tickets/{Id(id)}?expand=true", null, ct)); }
        catch (TicketingProviderException e) when (e.Code == "provider-not-found") { return null; }
    }

    public async Task<SupportTicket> CreateTicketAsync(SupportTicketDraft draft, CancellationToken ct)
    {
        ValidateText(draft.Title, 200); ValidateText(draft.Body, 16000);
        if(draft.Source is not("member" or "staff" or "guest"))throw new ArgumentException("Invalid source.");
        if (!Guid.TryParse(draft.OperationReference, out _)) throw new ArgumentException("Invalid operation reference.");
        return ReadWrite(Ticket, await Send(HttpMethod.Post, "tickets", new { title = draft.Title, customer_id = Id(draft.CustomerId), group_id = Id(draft.GroupId),
            swoovr_operation = draft.OperationReference, swoovr_source=draft.Source, article = new { body = draft.Body, type = "note", content_type = "text/plain", @internal = false, sender = "Customer" } }, ct));
    }

    public async Task<SupportTicket> ChangeTicketAsync(long id, SupportTicketChange change, CancellationToken ct)
    {
        var values = new Dictionary<string, long>();
        if (change.GroupId is long g) values["group_id"] = Id(g);
        if (change.OwnerId is long o) values["owner_id"] = Id(o);
        if (change.StateId is long s) values["state_id"] = Id(s);
        if (change.PriorityId is long p) values["priority_id"] = Id(p);
        if (values.Count == 0) throw new ArgumentException("No changes.");
        return ReadWrite(Ticket, await Send(HttpMethod.Put, $"tickets/{Id(id)}", values, ct));
    }

    public async Task<IReadOnlyList<SupportArticle>> GetArticlesAsync(long ticketId, CancellationToken ct)
        => (await Send(HttpMethod.Get, $"ticket_articles/by_ticket/{Id(ticketId)}", null, ct)).EnumerateArray().Select(Article).ToArray();

    public async Task<SupportArticle> ReplyAsync(long ticketId, string body, bool internalNote, bool customer, CancellationToken ct)
    {
        ValidateText(body, 16000);
        if (internalNote && customer) throw new ArgumentException("Customers cannot create private notes.");
        return ReadWrite(Article, await Send(HttpMethod.Post, "ticket_articles", new { ticket_id = Id(ticketId), body, type = "note", content_type = "text/plain",
            @internal = internalNote, sender = customer ? "Customer" : "Agent" }, ct));
    }

    private async Task<JsonElement> Send(HttpMethod method, string path, object? payload, CancellationToken ct, long? onBehalf = null)
    {
        bool write = method != HttpMethod.Get;
        using var request = new HttpRequestMessage(method, new Uri(root, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Token", "token=" + token);
        if((onBehalf ?? from) is long actor) request.Headers.TryAddWithoutValidation("From",actor.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (payload is not null) request.Content = JsonContent.Create(payload);
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode switch {
                    HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized => "provider-forbidden",
                    HttpStatusCode.NotFound => "provider-not-found",
                    HttpStatusCode.TooManyRequests => "provider-throttled",
                    _ => "provider-unavailable" };
                throw new TicketingProviderException(code, write && (int)response.StatusCode >= 500);
            }
            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            if (bytes.Length == 0) return default;
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.Clone();
        }
        catch (TicketingProviderException) { throw; }
        catch (OperationCanceledException) when (write || !ct.IsCancellationRequested) { throw new TicketingProviderException("provider-timeout", write); }
        catch (HttpRequestException) { throw new TicketingProviderException("provider-unavailable", write); }
        catch (JsonException) { throw new TicketingProviderException("provider-schema", write); }
    }

    private static void ValidateText(string value, int max) { if (string.IsNullOrWhiteSpace(value) || value.Length > max || value.Contains('\0')) throw new ArgumentException("Invalid support text."); }
    private static long Id(long id) => id > 0 ? id : throw new ArgumentException("Invalid provider ID.");
    private static string Text(JsonElement e, string name) => e.GetProperty(name).GetString() ?? throw new TicketingProviderException("provider-schema");
    private static SupportCustomer Customer(JsonElement e) => Read(() => new SupportCustomer(e.GetProperty("id").GetInt64(), Text(e, "login"), e.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String ? email.GetString() : null, e.GetProperty("active").GetBoolean()));
    private static SupportTicket Ticket(JsonElement e) => Read(() => new SupportTicket(e.GetProperty("id").GetInt64(), Text(e, "number"), Text(e, "title"), e.GetProperty("customer_id").GetInt64(),
        e.GetProperty("group_id").GetInt64(), e.GetProperty("owner_id").GetInt64(), e.GetProperty("state_id").GetInt64(), e.GetProperty("priority_id").GetInt64(), e.GetProperty("updated_at").GetDateTimeOffset(), e.TryGetProperty("swoovr_operation", out var operation) && operation.ValueKind == JsonValueKind.String ? operation.GetString() : null) { Classification = new[]{"swoovr_category","swoovr_subcategory","swoovr_sensitivity","swoovr_resolution","swoovr_source"}.Where(k=>e.TryGetProperty(k,out var v)&&v.ValueKind==JsonValueKind.String).ToDictionary(k=>k,k=>e.GetProperty(k).GetString()!) });
    private static SupportArticle Article(JsonElement e) => Read(() => new SupportArticle(e.GetProperty("id").GetInt64(), e.GetProperty("ticket_id").GetInt64(), Text(e, "body"), Text(e, "content_type"),
        e.GetProperty("internal").GetBoolean(), Text(e, "sender"), e.GetProperty("created_at").GetDateTimeOffset(), e.GetProperty("attachments").EnumerateArray().Select(a => new SupportAttachment(a.GetProperty("id").GetInt64(), Text(a,"filename"),
            AttachmentType(a), AttachmentSize(a))).ToArray()) {AuthorId=e.TryGetProperty("created_by_id",out var author)?author.GetInt64():null,OperationReference=ArticleOperation(e),Type=e.TryGetProperty("type",out var type)&&type.ValueKind==JsonValueKind.String?type.GetString():null});
    private static string? ArticleOperation(JsonElement e)
    {
        // Customer articles deliberately discard arbitrary preferences in 7.2.2. Message-ID is a verified persisted field.
        if(e.TryGetProperty("message_id",out var message)&&message.ValueKind==JsonValueKind.String&&message.GetString() is {} id&&
            id.StartsWith("swoovr-operation-",StringComparison.Ordinal)&&id.EndsWith("@swoovr.invalid",StringComparison.Ordinal)&&id.Length==68&&Guid.TryParseExact(id[17..53],"D",out var operation))return operation.ToString("D");
        return e.TryGetProperty("preferences",out var preferences)&&preferences.ValueKind==JsonValueKind.Object&&preferences.TryGetProperty("swoovr_operation",out var marker)&&marker.ValueKind==JsonValueKind.String?marker.GetString():null;
    }
    private static string AttachmentType(JsonElement a)
    {
        var preferences=a.GetProperty("preferences");
        return preferences.TryGetProperty("Mime-Type",out var type)||preferences.TryGetProperty("Content-Type",out type)?type.GetString()??"application/octet-stream":"application/octet-stream";
    }
    private static long AttachmentSize(JsonElement a)
    {
        var value=a.GetProperty("size");
        return value.ValueKind==JsonValueKind.Number?value.GetInt64():long.Parse(value.GetString()!,System.Globalization.CultureInfo.InvariantCulture);
    }
    private static T ReadWrite<T>(Func<JsonElement, T> parse, JsonElement value)
    {
        try { return parse(value); }
        catch (TicketingProviderException e) when (e.Code == "provider-schema") { throw new TicketingProviderException("provider-schema", true); }
    }
    private static T Read<T>(Func<T> read)
    {
        try { return read(); }
        catch (Exception e) when (e is KeyNotFoundException or InvalidOperationException or FormatException or OverflowException) { throw new TicketingProviderException("provider-schema"); }
    }
}
