using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;

namespace Swoovr.Infrastructure.Identity;

/// <summary>#206: pinned Keycloak Admin REST user updates preserve unrelated attributes/actions and never accept passwords.</summary>
public partial class KeycloakIdentityAdmin
{
    public async Task<IdentityAccountInfo?> AccountAsync(string subject, CancellationToken ct)
    {
        if (!Configured || !Guid.TryParse(subject, out var id)) return null;
        using var user = await GetAsync($"users/{id}", "account metadata", ct, array: false);
        using var credentials = user is null ? null : await GetAsync($"users/{id}/credentials", "credential types", ct);
        if (user is null || credentials is null) return null;
        try
        {
            var row = user.RootElement;
            if (row.GetProperty("id").GetString() != id.ToString()) return null;
            var types = credentials.RootElement.EnumerateArray().Select(c => Text(c, "type")).OfType<string>().Distinct().ToArray();
            var actions = row.TryGetProperty("requiredActions", out var required) ? required.EnumerateArray().Select(a => a.GetString()!).ToArray() : [];
            string? phone = row.TryGetProperty("attributes", out var attrs) && attrs.TryGetProperty("phoneNumber", out var phones) && phones.ValueKind == JsonValueKind.Array
                ? phones.EnumerateArray().Select(v => v.GetString()).FirstOrDefault() : null;
            return new(subject, Text(row, "firstName"), Text(row, "lastName"), Text(row, "email"), row.TryGetProperty("emailVerified", out var verified) && verified.GetBoolean(),
                row.TryGetProperty("enabled", out var enabled) && enabled.GetBoolean(), phone, types, actions);
        }
        catch (Exception e) when (e is InvalidOperationException or KeyNotFoundException or FormatException) { return null; }
    }

    public async Task<string?> CreateAccountAsync(IdentityAccountUpdate update, CancellationToken ct)
    {
        if (!Configured) return null;
        // Disabled until the application account/roles commit; no password and no asserted email ownership.
        using var result = await DirectoryWriteAsync(HttpMethod.Post, "users", new { username = update.Email, firstName = update.FirstName,
            lastName = update.LastName, email = update.Email, emailVerified = false, enabled = false,
            requiredActions = new[] { "VERIFY_EMAIL", "CONFIGURE_TOTP" },
            attributes = new Dictionary<string, string[]> { ["phoneNumber"] = update.Phone is { Length: > 0 } phone ? [phone] : [] } }, ct);
        if (result?.StatusCode != HttpStatusCode.Created || result.Headers.Location is not { } location) return null;
        var path = location.IsAbsoluteUri ? location.AbsolutePath : location.OriginalString;
        return Guid.TryParse(path.TrimEnd('/').Split('/').LastOrDefault(), out var id) ? id.ToString() : null;
    }

    public async Task<bool> UpdateAccountAsync(string subject, IdentityAccountUpdate update, CancellationToken ct)
    {
        if (!Guid.TryParse(subject, out var id)) return false;
        using var current = await GetAsync($"users/{id}", "account update", ct, array: false);
        if (current is null || Text(current.RootElement, "id") != id.ToString()) return false;
        var values = ProfileValues(current.RootElement);
        if (current.RootElement.TryGetProperty("attributes", out var originalAttributes)) values["attributes"] = originalAttributes.Clone();
        if (current.RootElement.TryGetProperty("requiredActions", out var originalActions)) values["requiredActions"] = originalActions.Clone();
        values["firstName"] = JsonSerializer.SerializeToElement(update.FirstName);
        values["lastName"] = JsonSerializer.SerializeToElement(update.LastName);
        if (!string.Equals(Text(current.RootElement, "email"), update.Email, StringComparison.OrdinalIgnoreCase))
        {
            values["email"] = JsonSerializer.SerializeToElement(update.Email);
            values["emailVerified"] = JsonSerializer.SerializeToElement(false);
            var actions = ReadActions(current.RootElement).Append("VERIFY_EMAIL").Distinct().ToArray();
            values["requiredActions"] = JsonSerializer.SerializeToElement(actions);
        }
        values["enabled"] = JsonSerializer.SerializeToElement(update.Enabled);
        var attributes = values.TryGetValue("attributes", out var attrs) ? JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(attrs.GetRawText())! : [];
        attributes["phoneNumber"] = JsonSerializer.SerializeToElement(update.Phone is { Length: > 0 } phone ? new[] { phone } : []);
        values["attributes"] = JsonSerializer.SerializeToElement(attributes);
        using var result = await DirectoryWriteAsync(HttpMethod.Put, $"users/{id}", values, ct);
        return result?.IsSuccessStatusCode == true;
    }

    public async Task<bool> RequireActionsAsync(string subject, IReadOnlyList<string> actions, CancellationToken ct)
    {
        if (!Guid.TryParse(subject, out var id) || actions.Any(a => a is not ("CONFIGURE_TOTP" or "UPDATE_PASSWORD" or "VERIFY_EMAIL"))) return false;
        using var current = await GetAsync($"users/{id}", "required actions", ct, array: false);
        if (current is null || Text(current.RootElement, "id") != id.ToString()) return false;
        var values = ProfileValues(current.RootElement);
        values["requiredActions"] = JsonSerializer.SerializeToElement(ReadActions(current.RootElement).Concat(actions).Distinct().ToArray());
        using var result = await DirectoryWriteAsync(HttpMethod.Put, $"users/{id}", values, ct);
        return result?.IsSuccessStatusCode == true;
    }

    public async Task<bool> PasswordEmailAsync(string subject, CancellationToken ct)
    {
        if (!Guid.TryParse(subject, out var id) || await AccountAsync(subject, ct) is not { EmailVerified: true } account || !account.CredentialTypes.Contains("password")) return false;
        using var response = await DirectoryWriteAsync(HttpMethod.Put, $"users/{id}/execute-actions-email", new[] { "UPDATE_PASSWORD" }, ct);
        return response?.IsSuccessStatusCode == true;
    }

    // Native declarative user profiles require these fields on PUT. Preserve only public profile
    // fields and administrative flags; never echo credentials, secrets, federation links or arbitrary response fields.
    private static Dictionary<string, JsonElement> ProfileValues(JsonElement current)
    {
        var values = new Dictionary<string, JsonElement>();
        foreach (var key in new[] { "username", "email", "firstName", "lastName", "emailVerified", "enabled", "attributes", "requiredActions" })
            if (current.TryGetProperty(key, out var value)) values[key] = value.Clone();
        return values;
    }
    private static string? Text(JsonElement e, string key) => e.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static IEnumerable<string> ReadActions(JsonElement e) => e.TryGetProperty("requiredActions", out var values) ? values.EnumerateArray().Select(v => v.GetString()!).ToArray() : [];
    private async Task<HttpResponseMessage?> DirectoryWriteAsync(HttpMethod method, string path, object value, CancellationToken ct)
    {
        if (!Configured) return null;
        try
        {
            var bearer = await TokenAsync(ct);
            if (bearer is null) return null;
            using var request = new HttpRequestMessage(method, $"{options.BaseUrl.TrimEnd('/')}/admin/realms/{Uri.EscapeDataString(options.Realm)}/{path}");
            request.Headers.Authorization = new("Bearer", bearer);
            request.Content = JsonContent.Create(value);
            // No retries: a lost write response is uncertain. Never log bodies or automatic redirect locations.
            var result = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!result.IsSuccessStatusCode) log.LogWarning("Keycloak directory write returned {Status}.", (int)result.StatusCode);
            return result;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException) { log.LogWarning("Keycloak directory write failed: {Kind}.", e.GetType().Name); return null; }
    }
}
