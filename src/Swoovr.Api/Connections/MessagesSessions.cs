using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Connections;

/// <summary>
/// #198: the messaging hub's open connections on this API instance, with the sign-in each one was opened with. The hub
/// checks access only when a connection opens, so a session cutoff (sign out everywhere, a sign-in lock, a contact
/// retirement or a restore) must also close connections that are already open; otherwise they keep receiving pushes.
/// Staff actions close them at once (<see cref="Retire"/>, <see cref="End"/>); <see cref="MessagesSessionSweep"/> re-checks
/// every open connection against the database on a short interval, which covers every other cutoff and every instance.
/// </summary>
public sealed class MessagesConnections
{
    public sealed record Entry(Guid Account, DateTimeOffset AuthenticatedAt, string? IssuedAt, Action Abort);

    private readonly ConcurrentDictionary<string, Entry> open = new();

    public void Add(string connection, Entry entry) => open[connection] = entry;
    public void Remove(string connection) => open.TryRemove(connection, out _);
    public int Count => open.Count;
    public IReadOnlyCollection<Guid> Accounts() => open.Values.Select(e => e.Account).Distinct().ToList();

    /// <summary>Closes the account's connections whose sign-in the cutoff refuses (the same rule as every API request).</summary>
    public int Retire(Guid account, DateTimeOffset? cutoff, DateTimeOffset now) =>
        Close(e => e.Account == account && SessionRevocationPolicy.Refuses(cutoff, e.AuthenticatedAt, e.IssuedAt, now));

    /// <summary>Closes every connection of the account (it is no longer active).</summary>
    public int End(Guid account) => Close(e => e.Account == account);

    private int Close(Func<Entry, bool> refused)
    {
        var closed = 0;
        foreach (var (connection, entry) in open)
        {
            if (!refused(entry) || !open.TryRemove(connection, out _)) continue;
            entry.Abort();
            closed++;
        }
        return closed;
    }
}

/// <summary>
/// #198 (DEFAULT, 7 November 2026): every 15 seconds, closes open hub connections whose account is no longer active or whose
/// sign-in predates the account's session cutoff. Staff sign-out and locks don't wait for it; they close connections at once.
/// </summary>
public sealed class MessagesSessionSweep(MessagesConnections connections, IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<MessagesSessionSweep> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, clock, stoppingToken);
            if (connections.Count == 0) continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await SweepAsync(connections, scope.ServiceProvider.GetRequiredService<SwoovrDbContext>(), clock.GetUtcNow(), stoppingToken);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogWarning("The messaging session sweep failed ({Kind}); it runs again shortly.", error.GetType().Name);
            }
        }
    }

    public static async Task<int> SweepAsync(MessagesConnections connections, SwoovrDbContext db, DateTimeOffset now, CancellationToken token)
    {
        var accounts = connections.Accounts();
        if (accounts.Count == 0) return 0;
        var states = await db.Accounts.AsNoTracking().Where(a => accounts.Contains(a.Id))
            .Select(a => new { a.Id, a.Status, a.SessionsRevokedAt }).ToDictionaryAsync(a => a.Id, token);
        var closed = 0;
        foreach (var account in accounts)
            closed += states.TryGetValue(account, out var state) && state.Status == AccountStatus.Active
                ? connections.Retire(account, state.SessionsRevokedAt, now)
                : connections.End(account);
        return closed;
    }
}
