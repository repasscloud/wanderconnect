using System.Globalization;
using System.Text.Json;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Ticketing;

/// <summary>
/// Staff account management through Zammad 7.2.2's REST API, bound to the dedicated staff-directory credential
/// (permissions admin.user and admin.session only). Every agent it writes has exactly the Agent role, so a role added in
/// Zammad's own admin screens is removed again.
/// </summary>
public sealed partial class ZammadTicketingProvider : ITicketingStaffDirectory
{
    public async Task<IReadOnlyList<SupportOption>> GroupsAsync(CancellationToken ct) =>
        (await Send(HttpMethod.Get, "groups", null, ct)).EnumerateArray()
            .Select(e => Read(() => new SupportOption(e.GetProperty("id").GetInt64(), Text(e, "name"), null, null))).ToArray();

    public async Task<StaffDirectoryUser?> FindAsync(string login, CancellationToken ct)
    {
        if (login.Length is < 1 or > 80 || login.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '-')) throw new ArgumentException("Invalid staff login.");
        var rows = await Send(HttpMethod.Get, "users/search?query=" + Uri.EscapeDataString("login:\"" + login + "\"") + "&expand=true&page=1&per_page=10", null, ct);
        var matches = rows.EnumerateArray().Where(u => Text(u, "login") == login).Select(StaffUser).ToArray();
        return matches.Length switch { 0 => null, 1 => matches[0], _ => throw new TicketingProviderException("provider-identity-ambiguous") };
    }

    public async Task<StaffDirectoryUser> GetAsync(long id, CancellationToken ct) => StaffUser(await Send(HttpMethod.Get, $"users/{Id(id)}?expand=true", null, ct));

    // Writes are read back expanded (role and group names), so what is checked is what Zammad stored.
    public async Task<StaffDirectoryUser> CreateAgentAsync(string login, string firstname, string lastname, string? email, CancellationToken ct)
    {
        var created = await Send(HttpMethod.Post, "users", new { login, firstname, lastname, email = email ?? "", roles = new[] { "Agent" }, group_ids = new Dictionary<string, string[]>(), active = false }, ct);
        return await GetAsync(ReadWrite(e => e.GetProperty("id").GetInt64(), created), ct);
    }

    public async Task<StaffDirectoryUser> UpdateAgentAsync(long id, bool active, IReadOnlyDictionary<long, string> groups, string firstname, string lastname, string? email, CancellationToken ct)
    {
        await Send(HttpMethod.Put, $"users/{Id(id)}", new
        {
            active, firstname, lastname, email = email ?? "", roles = new[] { "Agent" },
            group_ids = groups.ToDictionary(g => Id(g.Key).ToString(CultureInfo.InvariantCulture), g => new[] { g.Value }),
        }, ct);
        return await GetAsync(id, ct);
    }

    public async Task<string> IssueTokenAsync(long id, CancellationToken ct)
    {
        var issued = await Send(HttpMethod.Post, "user_access_token", new { name = "swoovr-admin", permission = new[] { "ticket.agent" }, expires_at = (string?)null }, ct, Id(id));
        var token = ReadWrite(e => Text(e, "token"), issued);
        return token.Length is >= 32 and <= 256 && token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_') ? token : throw new TicketingProviderException("provider-schema", true);
    }

    public async Task<int> RevokeTokensAsync(long id, bool keepIssued, CancellationToken ct)
    {
        var listed = await Send(HttpMethod.Get, "user_access_token", null, ct, Id(id));
        var tokens = Read(() => listed.GetProperty("tokens").EnumerateArray().Select(t => (Id: t.GetProperty("id").GetInt64(), Name: t.GetProperty("name").GetString())).ToList());
        long? keep = keepIssued ? tokens.Where(t => t.Name == "swoovr-admin").Select(t => (long?)t.Id).Max() : null;
        var revoked = 0;
        foreach (var token in tokens.Where(t => t.Id != keep))
        {
            await Send(HttpMethod.Delete, "user_access_token/" + Id(token.Id).ToString(CultureInfo.InvariantCulture), null, ct, Id(id));
            revoked++;
        }
        return revoked;
    }

    public async Task<int> EndSessionsAsync(long id, CancellationToken ct)
    {
        var listed = await Send(HttpMethod.Get, "sessions", null, ct);
        var sessions = listed.ValueKind == JsonValueKind.Object && listed.TryGetProperty("sessions", out var rows) ? rows : listed;
        var ended = 0;
        foreach (var session in sessions.EnumerateArray())
        {
            if (!session.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object || !data.TryGetProperty("user_id", out var user)) continue;
            var owner = user.ValueKind == JsonValueKind.Number ? user.GetInt64() : long.TryParse(user.GetString(), CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
            if (owner != id || !session.TryGetProperty("id", out var sessionId)) continue;
            await Send(HttpMethod.Delete, "sessions/" + Uri.EscapeDataString(sessionId.ValueKind == JsonValueKind.Number ? sessionId.GetInt64().ToString(CultureInfo.InvariantCulture) : sessionId.GetString() ?? ""), null, ct);
            ended++;
        }
        return ended;
    }

    async Task<IReadOnlyList<StaffDirectoryUser>> ITicketingStaffDirectory.AgentsAsync(CancellationToken ct)
    {
        var role = (await Send(HttpMethod.Get, "roles", null, ct)).EnumerateArray().Single(e => Text(e, "name") == "Agent").GetProperty("id").GetInt64();
        var users = new List<StaffDirectoryUser>();
        for (var page = 1; page <= 100; page++)
        {
            var rows = await Send(HttpMethod.Get, $"users/search?query=role_ids:{role}&expand=true&page={page}&per_page=100", null, ct);
            users.AddRange(rows.EnumerateArray().Select(StaffUser).Where(u => u.Active));
            if (rows.GetArrayLength() < 100) return users;
        }
        throw new TicketingProviderException("provider-directory-too-large");
    }

    private static StaffDirectoryUser StaffUser(JsonElement e) => Read(() => new StaffDirectoryUser(e.GetProperty("id").GetInt64(), Text(e, "login"), e.GetProperty("active").GetBoolean(),
        e.GetProperty("roles").EnumerateArray().Select(r => r.GetString()!).ToArray(),
        e.GetProperty("group_ids").EnumerateObject().ToDictionary(p => long.Parse(p.Name, CultureInfo.InvariantCulture), p => (IReadOnlyList<string>)p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray()),
        e.TryGetProperty("email", out var email) && email.ValueKind == JsonValueKind.String && email.GetString() is { Length: > 0 } address ? address : null));
}
