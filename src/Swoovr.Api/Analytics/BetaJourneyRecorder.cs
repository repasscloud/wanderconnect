using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Moderation;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Analytics;

/// <summary>Best-effort operational observations use their own context, never poisoning a product transaction.</summary>
public sealed class BetaJourneyRecorder(DbContextOptions<SwoovrDbContext> options,IConfiguration configuration,
    IHostEnvironment environment,TimeProvider clock,ILogger<BetaJourneyRecorder> log)
{
    public async Task RecordAsync(Guid account,bool explored,bool hasPeer,CancellationToken token)
    {
        if(!configuration.GetValue("BetaAnalytics:Enabled",false))return;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(1));
        try
        {
            await using var db=new SwoovrDbContext(options);
            await using var lease=await PreservationLease.SharedAsync(db,deadline.Token);
            var day=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
            var dataset=ModerationTransparencyRecorder.Dataset(environment,configuration);
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO beta_journey_days(account_id,day,dataset,explore_succeeded,explore_has_peer)
                SELECT a.id,{day},{dataset},{explored},{explored&&hasPeer} FROM accounts a
                JOIN member_profiles p ON p.account_id=a.id
                WHERE a.id={account} AND a.status='Active' AND a.realm='Members' AND NOT p.bot
                FOR SHARE OF a
                ON CONFLICT(account_id,day,dataset) DO UPDATE SET
                explore_succeeded=beta_journey_days.explore_succeeded OR EXCLUDED.explore_succeeded,
                explore_has_peer=beta_journey_days.explore_has_peer OR EXCLUDED.explore_has_peer
                """,deadline.Token);
        }
        catch(Exception error)when(!token.IsCancellationRequested)
        {log.LogDebug("Beta observation unavailable ({ErrorType}); no product action changed.",error.GetType().Name);}
    }
}
