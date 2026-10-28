using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Media;

/// <summary>One media lifecycle writer per account, including across API replicas/operator jobs. Nested use is reentrant.</summary>
public sealed class MediaAccountLease : IAsyncDisposable
{
    public const int Namespace = 0x4D454449;
    public static int Key(Guid account) => BinaryPrimitives.ReadInt32BigEndian(SHA256.HashData(account.ToByteArray()));
    private readonly SwoovrDbContext db;
    private readonly int key;
    private MediaAccountLease(SwoovrDbContext db, int key) { this.db = db; this.key = key; }
    public static async Task<MediaAccountLease> AcquireAsync(SwoovrDbContext db, Guid account, CancellationToken token) =>
        await OpenAsync(db, account, false, token) ?? throw new InvalidOperationException("Media lease unavailable.");
    public static Task<MediaAccountLease?> TryAcquireAsync(SwoovrDbContext db, Guid account, CancellationToken token) => OpenAsync(db, account, true, token);
    private static async Task<MediaAccountLease?> OpenAsync(SwoovrDbContext db, Guid account, bool tryOnly, CancellationToken token)
    {
        var key = Key(account);
        await db.Database.OpenConnectionAsync(token);
        try
        {
            if (tryOnly)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT pg_try_advisory_lock(@namespace, @key)";
                var ns = command.CreateParameter(); ns.ParameterName = "namespace"; ns.Value = Namespace; command.Parameters.Add(ns);
                var id = command.CreateParameter(); id.ParameterName = "key"; id.Value = key; command.Parameters.Add(id);
                if (await command.ExecuteScalarAsync(token) is not true) { await db.Database.CloseConnectionAsync(); return null; }
            }
            else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({Namespace}, {key})", token);
            return new(db, key);
        }
        catch
        {
            // Acquisition may have completed before cancellation was observed; release on the same session.
            try { using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({Namespace}, {key})", release.Token); }
            finally { await db.Database.CloseConnectionAsync(); }
            throw;
        }
    }
    public async ValueTask DisposeAsync()
    {
        try { using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5)); await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({Namespace}, {key})", release.Token); }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
