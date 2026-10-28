using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>One safety/account lifecycle writer per account, including across API replicas/operator jobs. Nested use is reentrant.</summary>
public sealed class AccountLifecycleLease : IAsyncDisposable
{
    public const int Namespace = 0x41434354;
    public const int MediaNamespace = 0x4D454449;
    public static int Key(Guid account) => BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(account.ToByteArray()));
    private readonly SwoovrDbContext db;
    private readonly int key, space;
    private int disposed;
    private AccountLifecycleLease(SwoovrDbContext db, int key, int space) { this.db = db; this.key = key; this.space = space; }
    public static async Task<AccountLifecycleLease> AcquireAsync(SwoovrDbContext db, Guid account, CancellationToken token) =>
        await OpenAsync(db, account, Namespace, false, token) ?? throw new InvalidOperationException("Media lease unavailable.");
    public static Task<AccountLifecycleLease?> TryAcquireAsync(SwoovrDbContext db, Guid account, CancellationToken token) => OpenAsync(db, account, Namespace, true, token);
    public static async Task<AccountLifecycleLease> AcquireMediaAsync(SwoovrDbContext db, Guid account, CancellationToken token) =>
        await OpenAsync(db, account, MediaNamespace, false, token) ?? throw new InvalidOperationException("Media lease unavailable.");
    public static Task<AccountLifecycleLease?> TryAcquireMediaAsync(SwoovrDbContext db, Guid account, CancellationToken token) => OpenAsync(db, account, MediaNamespace, true, token);
    public static async Task<IAsyncDisposable> AcquireManyAsync(SwoovrDbContext db, IEnumerable<Guid> accounts, CancellationToken token)
    {
        var acquired = new List<AccountLifecycleLease>();
        try
        {
            foreach (var account in accounts.GroupBy(Key).OrderBy(group => group.Key).Select(group => group.First())) acquired.Add(await AcquireAsync(db, account, token));
            return new LeaseSet(acquired);
        }
        catch { await new LeaseSet(acquired).DisposeAsync(); throw; }
    }
    private sealed class LeaseSet(List<AccountLifecycleLease> acquired) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Always attempt all releases even when one connection command fails.
            Exception? failure = null;
            foreach (var lease in acquired.AsEnumerable().Reverse())
                try { await lease.DisposeAsync(); } catch (Exception error) { failure ??= error; }
            if (failure is not null) throw failure;
        }
    }
    private static async Task<AccountLifecycleLease?> OpenAsync(SwoovrDbContext db, Guid account, int space, bool tryOnly, CancellationToken token)
    {
        var key = Key(account);
        await db.Database.OpenConnectionAsync(token);
        try
        {
            if (tryOnly)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT pg_try_advisory_lock(@namespace, @key)";
                var ns = command.CreateParameter(); ns.ParameterName = "namespace"; ns.Value = space; command.Parameters.Add(ns);
                var id = command.CreateParameter(); id.ParameterName = "key"; id.Value = key; command.Parameters.Add(id);
                if (await command.ExecuteScalarAsync(token) is not true) { await db.Database.CloseConnectionAsync(); return null; }
            }
            else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({space}, {key})", token);
            return new(db, key, space);
        }
        catch
        {
            // Acquisition may have completed before cancellation was observed; release on the same session.
            try { using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({space}, {key})", release.Token); }
            finally { await db.Database.CloseConnectionAsync(); }
            throw;
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try { using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({space}, {key})", release.Token); }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
