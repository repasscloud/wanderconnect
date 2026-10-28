using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>
/// Ordinary EF writes participate in preservation snapshots without holding the global barrier through provider calls.
/// Destructive bulk/object work has an explicit outer barrier through its transaction/external deletion.
/// </summary>
public sealed class PreservationSaveInterceptor : SaveChangesInterceptor
{
    private readonly ConcurrentDictionary<DbContext, PreservationLease> pending = new();
    private async Task BeginAsync(DbContext? context, CancellationToken token)
    {
        if (context is not SwoovrDbContext db) return;
        var lease = await PreservationLease.SharedAsync(db, token);
        if (pending.TryAdd(db, lease)) return;
        await lease.DisposeAsync();
        throw new InvalidOperationException("Concurrent saves are not supported on one persistence context.");
    }
    private async Task EndAsync(DbContext? context)
    {
        if (context is not null && pending.TryRemove(context, out var lease)) await lease.DisposeAsync();
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData data, InterceptionResult<int> result)
    { BeginAsync(data.Context, CancellationToken.None).GetAwaiter().GetResult(); return result; }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData data, InterceptionResult<int> result, CancellationToken token = default)
    { await BeginAsync(data.Context, token); return result; }
    public override int SavedChanges(SaveChangesCompletedEventData data, int result)
    { EndAsync(data.Context).GetAwaiter().GetResult(); return result; }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data, int result, CancellationToken token = default)
    { await EndAsync(data.Context); return result; }
    public override void SaveChangesFailed(DbContextErrorEventData data) => EndAsync(data.Context).GetAwaiter().GetResult();
    public override Task SaveChangesFailedAsync(DbContextErrorEventData data, CancellationToken token = default) => EndAsync(data.Context);
    public override void SaveChangesCanceled(DbContextEventData data) => EndAsync(data.Context).GetAwaiter().GetResult();
    public override Task SaveChangesCanceledAsync(DbContextEventData data, CancellationToken token = default) => EndAsync(data.Context);
    public override InterceptionResult ThrowingConcurrencyException(ConcurrencyExceptionEventData data, InterceptionResult result)
    { EndAsync(data.Context).GetAwaiter().GetResult(); return result; }
    public override async ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData data, InterceptionResult result, CancellationToken token = default)
    { await EndAsync(data.Context); return result; }
}
