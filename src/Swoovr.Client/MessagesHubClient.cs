using Microsoft.AspNetCore.SignalR.Client;
using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>
/// Receives the messaging hub's pushes (D143): new messages, deletions for both and conversations a block closed. The
/// app connects to the API's hub with its bearer token; the browser connects to the web host's relay, which holds the
/// token server-side. Nothing is sent through the hub: messages go through MemberClient, so every send is checked.
/// </summary>
public sealed class MessagesHubClient : IAsyncDisposable
{
    private readonly HubConnection connection;

    public MessagesHubClient(Uri hub, Func<Task<string?>>? accessToken = null)
    {
        connection = new HubConnectionBuilder()
            .WithUrl(hub, options => { if (accessToken is not null) options.AccessTokenProvider = accessToken; })
            .WithAutomaticReconnect()
            .Build();
        connection.On<MessageView>("message", m => Message?.Invoke(m));
        connection.On<MessageDeleted>("deleted", d => Deleted?.Invoke(d));
        connection.On<ConversationClosed>("closed", c => Closed?.Invoke(c));
    }

    public event Action<MessageView>? Message;
    public event Action<MessageDeleted>? Deleted;
    public event Action<ConversationClosed>? Closed;

    public bool Connected => connection.State == HubConnectionState.Connected;

    public Task StartAsync(CancellationToken cancellationToken = default) => connection.StartAsync(cancellationToken);

    public ValueTask DisposeAsync() => connection.DisposeAsync();
}
