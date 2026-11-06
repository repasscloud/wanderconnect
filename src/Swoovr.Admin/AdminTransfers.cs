using System.Text.RegularExpressions;

namespace Swoovr.Admin;

/// <summary>
/// Issue 222 (D331): support files in Admin. Staff replies and private notes may carry the owner's file list (D330), so
/// the ticket page's posts accept up to the policy's request size, and uploads and attachment downloads use an API client
/// with a long timeout. Every other Admin call keeps the 10-second client.
/// </summary>
public static partial class AdminTransfers
{
    public const string Client = "admin-transfer";

    public static Swoovr.Client.AdminClient Create(IHttpClientFactory clients) => new(clients.CreateClient(Client));

    /// <summary>A post to one support request's page (its reply form carries files).</summary>
    public static bool IsTicketPost(HttpRequest request) => HttpMethods.IsPost(request.Method) && TicketPage().IsMatch(request.Path.Value ?? "");

    [GeneratedRegex("^/tickets/[0-9]{1,18}$")]
    private static partial Regex TicketPage();
}
