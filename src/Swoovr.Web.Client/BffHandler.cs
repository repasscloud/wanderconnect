namespace Swoovr.Web.Client;

/// <summary>
/// Marks requests as coming from this app. The host refuses /bff/ calls without the header,
/// and a cross-site page cannot add a custom header without a CORS preflight the host never
/// grants, so another site cannot drive the API with the member's cookie.
/// </summary>
public sealed class BffHandler : DelegatingHandler
{
    public const string Header = "X-Swoovr-Bff";

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add(Header, "1");
        return base.SendAsync(request, cancellationToken);
    }
}
