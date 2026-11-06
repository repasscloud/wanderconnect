using System.Globalization;
using System.Text.Json;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Ticketing;

/// <summary>
/// D337 (Q136): Zammad 7.2.2 calls for the retention identity (`swoovr-retention`: `ticket.agent` with read access to every
/// group, plus `admin.data_privacy`). Deletion goes only through data privacy tasks, Zammad's supported deletion path;
/// it never edits Zammad's database. Responses are read for IDs, groups, states and dates only.
/// </summary>
public sealed partial class ZammadTicketingProvider : ITicketingRetentionProvider
{
    public async Task<IReadOnlyList<RetentionTicket>> TicketsAsync(int page, int pageSize, CancellationToken ct)
    {
        if (page is < 1 or > 100000 || pageSize is < 1 or > 100) throw new ArgumentException("Invalid page.");
        var rows = await Send(HttpMethod.Get, $"tickets?page={page}&per_page={pageSize}&sort_by=id&order_by=asc", null, ct);
        return rows.EnumerateArray().Select(RetentionRow).ToArray();
    }

    public async Task<RetentionTicket?> TicketAsync(long id, CancellationToken ct)
    {
        try { return RetentionRow(await Send(HttpMethod.Get, $"tickets/{Id(id)}", null, ct)); }
        catch (TicketingProviderException e) when (e.Code == "provider-not-found") { return null; }
    }

    public async Task<IReadOnlyDictionary<long, string>> GroupNamesAsync(CancellationToken ct) =>
        (await Send(HttpMethod.Get, "groups", null, ct)).EnumerateArray().ToDictionary(g => g.GetProperty("id").GetInt64(), g => Text(g, "name"));

    public async Task<IReadOnlySet<long>> ClosedStatesAsync(CancellationToken ct) =>
        (await Send(HttpMethod.Get, "ticket_states?expand=true", null, ct)).EnumerateArray()
            .Where(s => s.TryGetProperty("state_type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() is "closed" or "merged")
            .Select(s => s.GetProperty("id").GetInt64()).ToHashSet();

    public Task<RetentionTask> DeleteTicketAsync(long ticketId, CancellationToken ct) => DeleteAsync("Ticket", ticketId, ct);
    public Task<RetentionTask> DeleteCustomerAsync(long userId, CancellationToken ct) => DeleteAsync("User", userId, ct);

    private async Task<RetentionTask> DeleteAsync(string type, long id, CancellationToken ct) =>
        PrivacyTask(await Send(HttpMethod.Post, "data_privacy_tasks", new { deletable_type = type, deletable_id = Id(id) }, ct));

    public async Task<RetentionTask?> TaskAsync(long taskId, CancellationToken ct)
    {
        try { return PrivacyTask(await Send(HttpMethod.Get, $"data_privacy_tasks/{Id(taskId)}", null, ct)); }
        catch (TicketingProviderException e) when (e.Code == "provider-not-found") { return null; }
    }

    public async Task<bool> CustomerHasTicketsAsync(long userId, CancellationToken ct) =>
        (await Send(HttpMethod.Get, $"tickets/search?query={Uri.EscapeDataString("customer_id:" + Id(userId).ToString(CultureInfo.InvariantCulture))}&page=1&per_page=1", null, ct))
            .GetArrayLength() > 0;

    /// <summary>D333 retirement (identity token, `admin.user`): inactive, opaque `retired-…` login, personal fields cleared.</summary>
    public async Task<SupportCustomer> RetireCustomerAsync(long id, string retiredLogin, CancellationToken ct)
    {
        if (!retiredLogin.StartsWith("retired-", StringComparison.Ordinal) || retiredLogin.Length != 40 || !retiredLogin[8..].All(char.IsAsciiHexDigitLower))
            throw new ArgumentException("Invalid retired login.");
        return ReadWrite(Customer, await Send(HttpMethod.Put, $"users/{Id(id)}", new
        {
            // Zammad splits a lone first name into first/last; set both so the display name is exactly "Retired Swoovr Member".
            login = retiredLogin, firstname = HelpdeskRetentionPolicy.RetiredFirstName, lastname = HelpdeskRetentionPolicy.RetiredLastName, email = "", phone = "", mobile = "", fax = "",
            web = "", street = "", zip = "", city = "", country = "", address = "", department = "", note = "", active = false,
        }, ct));
    }

    private static RetentionTicket RetentionRow(JsonElement e) => Read(() => new RetentionTicket(e.GetProperty("id").GetInt64(), e.GetProperty("customer_id").GetInt64(),
        e.GetProperty("group_id").GetInt64(), e.GetProperty("state_id").GetInt64(),
        e.TryGetProperty("last_close_at", out var closed) && closed.ValueKind == JsonValueKind.String ? closed.GetDateTimeOffset() : null,
        e.GetProperty("updated_at").GetDateTimeOffset()));

    private static RetentionTask PrivacyTask(JsonElement e) => Read(() => new RetentionTask(e.GetProperty("id").GetInt64(), Text(e, "state")));
}
