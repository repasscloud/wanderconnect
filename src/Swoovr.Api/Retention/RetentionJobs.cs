using Swoovr.Api.Accounts;
using Swoovr.Api.Media;

namespace Swoovr.Api.Retention;

public sealed record RetentionJobRun(SafetyRetentionRun Safety, PurgeRun? Accounts, MessageRetentionRun? Messages, int MediaDeleted, int MediaKept, bool FailedClosed, int ConflictRecordsExpired = 0);

public sealed class RetentionJobs(SafetyRetention safety, AccountDeletion accounts, MessageRetention messages, MediaPipeline media, StaffConflictHistory conflicts)
{
    public async Task<RetentionJobRun> RunAsync(DateTimeOffset now, CancellationToken token)
    {
        await conflicts.ReconcileAsync(now, token);
        var reviews = await safety.PurgeAsync(now, token);
        if (reviews.HoldStatusUnavailable) return new(reviews, null, null, 0, 0, true);
        var deleted = await accounts.PurgeAsync(now, token);
        if (deleted.HoldStatusUnavailable) return new(reviews, deleted, null, 0, 0, true);
        var expired = await messages.PurgeAsync(now, token);
        if (expired.HoldStatusUnavailable) return new(reviews, deleted, expired, 0, 0, true);
        var objects = await media.SweepAsync(now, token);
        if (objects is null) return new(reviews, deleted, expired, 0, 0, true);
        var associations = await conflicts.PurgeAsync(now, token);
        return new(reviews, deleted, expired, objects.Value.Deleted, objects.Value.Kept, associations.HoldStatusUnavailable, associations.Expired);
    }
}

/// <summary>Hourly due queries survive restarts; per-record database leases coordinate replicas and operator runs.</summary>
public sealed class RetentionWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RetentionWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(30), clock, stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<RetentionJobs>().RunAsync(clock.GetUtcNow(), stoppingToken);
                if (result.FailedClosed) log.LogError("Retention run stopped because preservation status was unavailable.");
                else log.LogInformation("Retention: {Cases} case records, {Content} case content sets, {Copies} copies disposed; {Review} require restricted review; {Conflicts} minimal staff associations expired.",
                    result.Safety.CasesDisposed, result.Safety.ContentDisposed, result.Safety.CopiesDisposed, result.Safety.ReviewRequired, result.ConflictRecordsExpired);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { log.LogError("Retention run failed ({ErrorType}); remaining due records retry next run.", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}
