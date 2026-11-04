using System.Text.RegularExpressions;
using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>
/// Native support requests (Zammad behind the Swoovr API): wording shared by the member web app and the native app.
/// The helpdesk owns the conversation; Swoovr owns the reports, decisions and appeals a request may mention.
/// </summary>
public static partial class SupportTicketText
{
    public const string Title = "Support";
    public const string Intro = "Ask Swoovr support about your account, a report or a decision. Replies from the support team appear here.";
    public const string MemberPath = "/me/support";
    public const string NewTitle = "New support request";
    public const string NoTickets = "You haven't contacted support yet.";
    public const string SensitiveHelp = "Don't send identity documents, card numbers or passwords. Swoovr support never asks for them here.";
    public const string OutcomeHelp = "Closing a support request doesn't change a decision or an appeal. Those are shown in Account standing.";
    public const string AttachmentHelp = "You can add up to three plain text (.txt) files of 64 KB each.";
    public const string Unavailable = "Support isn't available right now. Try again soon, or email support@swoovr.com.";
    public const string TitleLabel = "Subject";
    public const string BodyLabel = "How can we help?";
    public const string ReplyLabel = "Your reply";

    /// <summary>The member's new-request link, carrying only a valid report reference (never report content).</summary>
    public static string NewLink(string? reference = null) => ValidReference(reference) ? MemberPath + "/new?reference=" + reference : MemberPath + "/new";
    public static string Link(long id) => MemberPath + "/" + id;

    public static bool ValidReference(string? reference) => reference is not null && Reference().IsMatch(reference);
    /// <summary>A safe prefill: the reference alone, which staff look up in Swoovr. Nothing from the report is copied.</summary>
    public static string Subject(string? reference) => ValidReference(reference) ? $"About report {reference}" : "";

    public static string State(string? name) => name switch
    {
        "new" => "Received",
        "open" => "Open",
        "pending reminder" or "pending close" => "Waiting",
        "closed" => "Closed",
        "merged" => "Merged",
        null or "" => "Unknown",
        _ => char.ToUpperInvariant(name[0]) + name[1..],
    };

    public static string StateName(IReadOnlyList<TicketOption> states, long id) => State(states.FirstOrDefault(s => s.Id == id)?.Name);

    public static string Sender(string sender, bool member) => sender switch
    {
        "Customer" => member ? "You" : "Member",
        "Agent" => "Swoovr support",
        _ => "Swoovr",
    };

    /// <summary>One sentence per refusal. Never echoes provider detail.</summary>
    public static string Problem(int status, string? code) => code switch
    {
        "ticket-changed" => "This request changed while you were working on it. The latest version is shown.",
        "ticket-reconciliation-required" => "We couldn't confirm your last message was sent. Send it again: it won't be sent twice.",
        "ticket-follow-up-unavailable" => "This request is closed. Start a new request if you still need help.",
        "ticket-operation-conflict" => "That message was already sent with different content. Refresh and try again.",
        "ticket-provider-unavailable" or "ticketing-unavailable" => Unavailable,
        "invalid-request" => "Check your message and any attachments, then try again.",
        "conflict-of-interest" => "You can't work on this request because of a recorded conflict of interest.",
        "ticket-customer-ambiguous" => "More than one account uses that email address. Look the member up by account ID instead.",
        "ticket-customer-not-found" => "No member account matches that.",
        "ticket-owner-unavailable" => "That person can't own requests in this queue.",
        "transfer-to-restricted-group-first" => "Move the request to a restricted queue before marking it restricted.",
        "ticket-macro-unsupported" => "That macro includes actions Swoovr can't run. Use the individual controls instead.",
        "ticket-case-unavailable" => "That case or appeal doesn't exist, or isn't available to your role.",
        "ticket-search-scope-too-large" => "That search covers too much. Narrow it with a queue or status.",
        _ => status switch
        {
            401 => "Your session has ended. Sign in again.",
            403 or 404 => "This request isn't available.",
            413 => "That's too large to send.",
            0 or >= 500 => Unavailable,
            _ => "Something went wrong. Try again.",
        },
    };

    [GeneratedRegex("^SW-[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$")]
    private static partial Regex Reference();
}
