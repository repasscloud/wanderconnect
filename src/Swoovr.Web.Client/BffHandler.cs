using Microsoft.AspNetCore.Components;
using Swoovr.Contracts;

namespace Swoovr.Web.Client;

/// <summary>
/// Marks requests as coming from this app. The host refuses /bff/ calls without the header,
/// and a cross-site page cannot add a custom header without a CORS preflight the host never
/// grants, so another site cannot drive the API with the member's cookie.
/// #201: when the host reports that it ended a retired session, the app reloads at the page the host named (sign in again,
/// or home) instead of showing "We couldn't load your account" on a session that no longer works.
/// </summary>
public sealed class BffHandler(NavigationManager? navigation = null) : DelegatingHandler
{
    public const string Header = "X-Swoovr-Bff";

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(Header, "1");
        var response = await base.SendAsync(request, cancellationToken);
        if (navigation is not null && response.StatusCode == System.Net.HttpStatusCode.Unauthorized
            && response.Headers.TryGetValues(RetiredSession.Header, out var values)
            && RetiredSession.Destination(values.FirstOrDefault()) is { } next)
            navigation.NavigateTo(next, forceLoad: true);
        return response;
    }
}
