using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace Swoovr.Admin;

/// <summary>
/// Adds the signed-in staff member's access token to admin API calls. The token stays in the server-side ticket
/// (TicketStore), never in the browser. An expired token gives the API's 401, which the pages turn into "sign in again".
/// </summary>
public sealed class StaffTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (accessor.HttpContext is { } context && await context.GetTokenAsync("access_token") is { } token)
            request.Headers.Authorization = new("Bearer", token);
        return await base.SendAsync(request, cancellationToken);
    }
}

/// <summary>Keeps authentication tickets (and the tokens in them) in server memory; the cookie carries only a key.</summary>
public sealed class TicketStore(IMemoryCache cache) : ITicketStore
{
    public Task<string> StoreAsync(AuthenticationTicket ticket)
    {
        var key = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        return RenewAsync(key, ticket).ContinueWith(_ => key);
    }

    public Task RenewAsync(string key, AuthenticationTicket ticket)
    {
        cache.Set(key, ticket, ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.AddHours(8));
        return Task.CompletedTask;
    }

    public Task<AuthenticationTicket?> RetrieveAsync(string key) => Task.FromResult(cache.Get<AuthenticationTicket>(key));

    public Task RemoveAsync(string key)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}

/// <summary>Short wording shared by the pages for API refusals.</summary>
public static class AdminText
{
    public static string Refusal(int status, string? code) => (status, code) switch
    {
        (401, _) => "Your session has ended. Sign in again.",
        (403, "stale-session") => "This needs a fresh sign-in. Sign in again, then repeat it.",
        (403, "moderation-not-permitted") => "Your role can't do this here (staff-affiliated case, your own account, or an admin's action).",
        (403, _) => "Your role can't do this.",
        (404, _) => "Not found, or not available to your role.",
        (409, _) => "That doesn't apply right now.",
        (400, _) => "Check the form: a reason is needed, restrictions last up to 30 days and suspensions up to 90.",
        _ => $"The API answered {status}.",
    };
}

/// <summary>Wording for legal-hold refusals (D189).</summary>
public static class HoldText
{
    public static string Refusal(int status, string? code) => (status, code) switch
    {
        (403, null) or (403, "") => "Only the security and legal officer can do this (admins may open a provisional hold from a case).",
        (_, "hold-reason-required") => "Give a reason (up to 1,000 characters).",
        (_, "hold-scope-invalid") => "Name 1–20 existing member accounts, and only their conversations. An admin's hold covers only the case's people.",
        (_, "hold-dates-invalid") => "Check the period and the review date (in the future, within a year).",
        (_, "hold-case-required") => "Admins open a provisional hold from a case.",
        (_, "hold-released") => "This hold has been released.",
        (_, "hold-not-provisional") => "Only a provisional hold can be confirmed.",
        (_, "disclosure-invalid") => "Record the purpose; a disclosure needs the agency, officer, reference, legal basis and how the request was verified.",
        (_, "evidence-not-configured") => "No evidence key is configured, so nothing can be preserved.",
        _ => AdminText.Refusal(status, code),
    };
}
