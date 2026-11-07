using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Connections.Features;
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
        // DEFAULT (#240): preserve the API's reconnectable expiry close, but keep revocation terminal.
        // A reconnect still authenticates here and at the API; a retired session cannot regain its group.
        var lifetime = Context.Features.Get<IConnectionLifetimeNotificationFeature>();
        Action reconnectBrowser = lifetime is null ? Context.Abort : lifetime.RequestClose;
        if (!await relays.OpenAsync(Context.ConnectionId, token, Context.Abort, reconnectBrowser, Context.ConnectionAborted)) { Context.Abort(); return; }
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

    public async Task<bool> OpenAsync(string browser, string token, Action closeBrowser, Action reconnectBrowser, CancellationToken cancellationToken)
    {
        // The client interprets the API's allowReconnect flag for us. Let the browser reauthenticate first;
        // its disconnect disposes this relay and cancels the delayed retry with the old token.
        var api = new HubConnectionBuilder().WithUrl(hub, options => options.AccessTokenProvider = () => Task.FromResult<string?>(token))
            .WithAutomaticReconnect([TimeSpan.FromSeconds(30)]).Build();
        foreach (var name in new[] { "message", "deleted", "closed" })
            api.On<JsonElement>(name, payload => browsers.Clients.Client(browser).SendAsync(name, payload));
        api.Reconnecting += _ =>
        {
            if (open.ContainsKey(browser)) reconnectBrowser();
            return Task.CompletedTask;
        };
        // #198: a terminal API close (including revocation) remains terminal for the browser.
        api.Closed += _ =>
        {
            if (open.ContainsKey(browser)) closeBrowser();
            return Task.CompletedTask;
        };
        // Register before starting: an API refusal can arrive immediately after the protocol handshake.
        open[browser] = api;
        try { await api.StartAsync(cancellationToken); }
        catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException)
        {
            log.LogWarning("The messaging relay couldn't reach the API hub ({Kind}).", error.GetType().Name);
            open.TryRemove(browser, out _);
            await api.DisposeAsync();
            return false;
        }
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
