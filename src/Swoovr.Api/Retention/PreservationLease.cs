using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>
/// Cross-process preservation barrier: destructive source work takes shared, coverage changes take exclusive.
/// Order: short account state, media mutex (if needed), barrier, hold mutex. Never upgrade shared to exclusive.
/// </summary>
public sealed class PreservationLease : IAsyncDisposable
{
    public const int BarrierNamespace = 0x50524553;
    public const int HoldNamespace = 0x484F4C44;
    public const int CaseNamespace = 0x43415345;
    private readonly SwoovrDbContext db;
    private readonly int space, key;
    private readonly bool shared;
    private int disposed;
    private PreservationLease(SwoovrDbContext db, int space, int key, bool shared) { this.db = db; this.space = space; this.key = key; this.shared = shared; }
    public static Task<PreservationLease> SharedAsync(SwoovrDbContext db, CancellationToken token) => OpenAsync(db, BarrierNamespace, 0, true, token);
    public static Task<PreservationLease> ExclusiveAsync(SwoovrDbContext db, CancellationToken token) => OpenAsync(db, BarrierNamespace, 0, false, token);
    public static Task<PreservationLease> HoldAsync(SwoovrDbContext db, Guid hold, CancellationToken token) => OpenAsync(db, HoldNamespace, AccountLifecycleLease.Key(hold), false, token);
    public static Task<PreservationLease> CaseAsync(SwoovrDbContext db, Guid review, CancellationToken token) => OpenAsync(db, CaseNamespace, AccountLifecycleLease.Key(review), false, token);
    private static async Task<PreservationLease> OpenAsync(SwoovrDbContext db, int space, int key, bool shared, CancellationToken token)
    {
        await db.Database.OpenConnectionAsync(token);
        var lease = new PreservationLease(db, space, key, shared);
        try
        {
            if (shared) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock_shared({space}, {key})", token);
            else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_lock({space}, {key})", token);
            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        try
        {
            using var release = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            if (shared) await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock_shared({space}, {key})", release.Token);
            else await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_unlock({space}, {key})", release.Token);
        }
        finally { await db.Database.CloseConnectionAsync(); }
    }
}
