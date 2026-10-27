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
        (403, "elevated-access-required") => "This needs time-limited access with a recorded purpose. Ask for it under Elevated access, then repeat it.",
        (403, "conflict-of-interest") => "A conflict of interest keeps you away from this member's records.",
        (409, "bulk-export-approval-required") => "A bulk export needs another officer's approval (Elevated access). Then export again.",
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
        (403, "elevated-access-required") or (403, "conflict-of-interest") or (409, "bulk-export-approval-required") => AdminText.Refusal(status, code),
        (_, "hold-reason-required") => "Give a reason (up to 1,000 characters).",
        (_, "hold-scope-invalid") => "Name 1–20 existing member accounts, and only their conversations. An admin's hold covers only the case's people.",
        (_, "hold-dates-invalid") => "Check the period and the review date (in the future, within a year).",
        (_, "hold-case-required") => "Admins open a provisional hold from a case.",
        (_, "hold-released") => "This hold has been released.",
        (_, "hold-not-provisional") => "Only a provisional hold can be confirmed.",
        (_, "disclosure-invalid") => "Record the purpose; a disclosure needs the agency, officer, reference, legal basis and how the request was verified.",
        (_, "evidence-not-configured") => "No evidence key is configured, so nothing can be preserved.",
        (_, "hold-release-not-independent") => "Release needs an officer who neither placed nor confirmed this hold (procedure §7).",
        (_, "hold-overlaps-unchecked") => "Other live holds cover the same accounts. Check them, tick the box and release again.",
        (_, "le-case-invalid") => "Check the form: the case, its fields or the redaction/correction details.",
        (_, "le-not-verified") => "Verify the requester independently first (procedure §3.2).",
        (_, "le-date-invalid") => "A temporary police preservation hold is reviewed within 30 days.",
        (_, "le-case-closed") => "The case is closed.",
        _ => AdminText.Refusal(status, code),
    };
}

/// <summary>Wording for law-enforcement case refusals (D195).</summary>
public static class LeText
{
    public static string Refusal(int status, string? code) => (status, code) switch
    {
        (403, null) or (403, "") => "Only an uninvolved security and legal officer can do this.",
        (_, "le-not-separated") => "Another officer approves this (or reviews the emergency approval) — procedure §5.",
        (_, "le-not-verified") => "Verify the requester independently first: an official directory, the agency switchboard, its portal or in person.",
        (_, "le-not-assessed") => "Record the legal assessment first.",
        (_, "le-app65-required") => "Relying on APP 6.2(e) needs the written APP 6.5 note first.",
        (_, "le-case-closed") => "The case is closed.",
        (_, "le-already-decided") => "That request was already decided.",
        (_, "le-closure-incomplete") => "The closure checklist isn't complete.",
        (_, "le-date-invalid") => "Check the date (reconsideration within six months; temporary holds within 30 days).",
        (_, "le-case-invalid") => "Check the form: every required field, valid choices and lengths.",
        _ => HoldText.Refusal(status, code),
    };

    public static string Item(string item) => item switch
    {
        "requests-decided" => "Every request decided",
        "emergency-reviewed" => "Every emergency (break-glass) approval reviewed by another officer",
        "requester-verified" => "Requester independently verified (needed for any disclosure)",
        "legal-assessment" => "Legal assessment recorded",
        "app65-note" => "APP 6.5 note recorded where APP 6.2(e) was relied on",
        "notification-decisions" => "A notification decision for every subject",
        "transfer-receipts" => "A receipt for every transfer",
        "holds-assigned" => "Every live hold from this case confirmed with a future review (holds outlive closure)",
        _ => item,
    };

    public static string Timer(string state) => state switch
    {
        "response-due" => "Response due",
        "response-overdue" => "Response OVERDUE",
        "update-due" => "Next update due",
        "update-overdue" => "Update OVERDUE",
        _ => "Closed",
    };
}
