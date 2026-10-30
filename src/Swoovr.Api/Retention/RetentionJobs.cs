using Swoovr.Api.Accounts;
using Swoovr.Api.Media;

namespace Swoovr.Api.Retention;

public sealed record RetentionJobRun(SafetyRetentionRun Safety, PurgeRun? Accounts, MessageRetentionRun? Messages, int MediaDeleted, int MediaKept, bool FailedClosed, int ConflictRecordsExpired = 0, int IdentifierRestrictionsExpired = 0, int IdentifierHistoriesDisposed = 0, int IdentifierReceiptsExpired = 0, int OperationalHoldsExpired = 0);

public sealed class RetentionJobs(SafetyRetention safety, AccountDeletion accounts, MessageRetention messages, MediaPipeline media, StaffConflictHistory conflicts, BannedIdentifierRegistry bans, RegistryExpiry expiry, RegistryDisposal disposal, OperationalRetention operational, Swoovr.Infrastructure.Persistence.SwoovrDbContext db, Swoovr.Api.Safety.MeetingSafetyRetention meetingSafety, Swoovr.Api.Moderation.TextHarmRetention textHarm,Swoovr.Api.Moderation.ModerationTransparencyRetention reporting)
{
    public async Task<RetentionJobRun> RunAsync(DateTimeOffset now, CancellationToken token,bool restoring=false)
    {
        if(!await reporting.PurgeAsync(token))return new(new SafetyRetentionRun(0,0,0,0,true),null,null,0,0,true);
        if(await textHarm.PurgeAsync(now,token) is null)return new(new SafetyRetentionRun(0,0,0,0,true),null,null,0,0,true);
        await conflicts.ReconcileAsync(now, token);
        var records = await operational.RunAsync(now, token);
        if(records.FailedClosed)return new(new SafetyRetentionRun(0,0,0,0,true),null,null,0,0,true);
        var reviews = await safety.PurgeAsync(now, token);
        if (reviews.HoldStatusUnavailable) return new(reviews, null, null, 0, 0, true);
        var deleted = await accounts.PurgeAsync(now, token,finalize:!restoring);
        if (deleted.HoldStatusUnavailable) return new(reviews, deleted, null, 0, 0, true);
        var expired = await messages.PurgeAsync(now, token);
        if (expired.HoldStatusUnavailable) return new(reviews, deleted, expired, 0, 0, true);
        var objects = await media.SweepAsync(now, token);
        if (objects is null) return new(reviews, deleted, expired, 0, 0, true);
        var associations = await conflicts.PurgeAsync(now, token);
        var attempts = await bans.SweepAttemptsAsync(now,token);
        var expiredBans = await expiry.RunAsync(now,token);
        var history=await disposal.RunAsync(now,token);
        await AuditRetention.RunAsync(db,now,token);
        var meetingsPreserved=await meetingSafety.RunAsync(now,token);
        return new(reviews, deleted, expired, objects.Value.Deleted, objects.Value.Kept, !meetingsPreserved || records.FailedClosed || associations.HoldStatusUnavailable || attempts is null || history.FailedClosed,
            associations.Expired, expiredBans, history.HistoriesDisposed, history.ReceiptsExpired,records.HoldsDisposed);
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
                if (result.FailedClosed) log.LogError("Retention run stopped because preservation status or membership was unavailable.");
                else log.LogInformation("Retention: {Cases} case records, {Content} case content sets, {Copies} copies disposed; {Review} require restricted review; {Conflicts} minimal staff associations expired; {Bans} scheduled identifier restrictions expired; {History} inactive identifier histories disposed; {Receipts} minimal dispositions expired; {Operational} released operational groups expired.",
                    result.Safety.CasesDisposed, result.Safety.ContentDisposed, result.Safety.CopiesDisposed, result.Safety.ReviewRequired, result.ConflictRecordsExpired, result.IdentifierRestrictionsExpired, result.IdentifierHistoriesDisposed, result.IdentifierReceiptsExpired,result.OperationalHoldsExpired);
            }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            { log.LogError("Retention run failed ({ErrorType}); remaining due records retry next run.", error.GetType().Name); }
            await Task.Delay(TimeSpan.FromHours(1), clock, stoppingToken);
        }
    }
}
