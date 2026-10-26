using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;

namespace Swoovr.Web.Auth;

/// <summary>
/// The browser's side of realtime messaging (D143) under the backend-for-frontend rule (D59): the browser connects here
/// with its session cookie, and the web host opens the API's messaging hub with the member's token, which never leaves
/// the server, and forwards the pushes. The browser can't invoke anything on either hub; messages go through /bff/.
/// Only same-origin connections are accepted, so another site can't listen with the member's cookie.
/// </summary>
public sealed class MessagesRelayHub(MessagesRelays relays) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var origin = http?.Request.Headers.Origin.ToString();
        var self = http is null ? null : $"{http.Request.Scheme}://{http.Request.Host}";
        if (http is null || http.User.Identity?.IsAuthenticated != true || !string.Equals(origin, self, StringComparison.OrdinalIgnoreCase)
            || await http.GetTokenAsync("access_token") is not { } token)
        {
            Context.Abort();
            return;
        }
        if (!await relays.OpenAsync(Context.ConnectionId, token, Context.ConnectionAborted)) { Context.Abort(); return; }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await relays.CloseAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>One API hub connection per browser connection, closed with it.</summary>
public sealed class MessagesRelays(IHubContext<MessagesRelayHub> browsers, IConfiguration configuration, ILogger<MessagesRelays> log) : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, HubConnection> open = new();
    private readonly Uri hub = new(new Uri(configuration["Api:BaseUrl"] ?? "http://127.0.0.1:5080/"), "hubs/messages");

    public async Task<bool> OpenAsync(string browser, string token, CancellationToken cancellationToken)
    {
        var api = new HubConnectionBuilder().WithUrl(hub, options => options.AccessTokenProvider = () => Task.FromResult<string?>(token)).Build();
        foreach (var name in new[] { "message", "deleted", "closed" })
            api.On<JsonElement>(name, payload => browsers.Clients.Client(browser).SendAsync(name, payload));
        try { await api.StartAsync(cancellationToken); }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            log.LogWarning("The messaging relay couldn't reach the API hub ({Kind}).", error.GetType().Name);
            await api.DisposeAsync();
            return false;
        }
        open[browser] = api;
        return true;
    }

    public async Task CloseAsync(string browser)
    {
        if (open.TryRemove(browser, out var api)) await api.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var api in open.Values) await api.DisposeAsync();
        open.Clear();
    }
}
