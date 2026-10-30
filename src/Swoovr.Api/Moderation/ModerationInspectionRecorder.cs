using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Moderation;

/// <summary>Prospective anonymous totals; completion shares caller source transaction and pre-acquired authority.</summary>
public sealed class ModerationInspectionRecorder(SwoovrDbContext db,IHostEnvironment environment,IConfiguration configuration,TimeProvider clock)
{
    public const string TextDefinition="local-text-rules-v1",ImageDefinition="image-screening-unversioned";
    // Scope-local duplicate protection only; never persisted/exported. The source's durable ProcessedAt/attempt state
    // and existing source mutex are the cross-process receipt. A new transaction may retry an aborted source write.
    private readonly Dictionary<(Guid Transaction,Guid Transition,string Definition),(bool Incomplete,bool Candidate)> completed=[];

    /// <param name="sourceTransition">Existing intent id or actual completed media-attempt id, only transiently held in this scope.</param>
    /// <returns>false only for a repeated call for the same transition inside this same transaction.</returns>
    public async Task<bool> CompletedAsync(string definition,Guid sourceTransition,bool incompleteHistory,bool createdCandidate,CancellationToken token)
    {
        Validate(definition,incompleteHistory);
        var transaction=db.Database.CurrentTransaction??throw new InvalidOperationException("Inspection completion requires its source transaction.");
        if(sourceTransition==Guid.Empty)throw new InvalidOperationException("Inspection completion needs an actual source transition.");
        var key=(transaction.TransactionId,sourceTransition,definition);
        if(completed.TryGetValue(key,out var facts))
        {
            if(facts!=(incompleteHistory,createdCandidate))throw new InvalidOperationException("Inspection completion facts changed inside source transaction.");
            return false;
        }
        // Failure propagates. Caller rolls back source transition and total together; never swallow this failure.
        await IncrementAsync(definition,1,incompleteHistory?1:0,createdCandidate?1:0,0,token);
        completed.Add(key,(incompleteHistory,createdCandidate));return true;
    }

    public async Task FailedInTransactionAsync(string definition,Guid transition,CancellationToken token)
    {
        Validate(definition,false);
        var transaction=db.Database.CurrentTransaction??throw new InvalidOperationException("Processing transition requires its source transaction.");
        if(transition==Guid.Empty)throw new InvalidOperationException("Processing transition is required.");
        var key=(transaction.TransactionId,transition,definition);
        if(completed.ContainsKey(key))throw new InvalidOperationException("Processing transition already counted in this transaction.");
        await IncrementAsync(definition,0,0,0,1,token);completed.Add(key,(false,false));
    }
    /// <summary>
    /// Call exactly once in the actual worker's per-intent catch, after failed source scope/transaction disposed.
    /// Counts attempts, not unique content/incidents. No exception/source/id argument, no blind retry on uncertainty.
    /// </summary>
    public async Task FailedAttemptAsync(string definition,CancellationToken token)
    {
        Validate(definition,false);
        if(db.Database.CurrentTransaction is not null)throw new InvalidOperationException("Failed inspection attempt must use a fresh post-rollback scope.");
        await using var authority=await PreservationLease.SharedAsync(db,token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        await IncrementAsync(definition,0,0,0,1,token);await transaction.CommitAsync(token);
    }
    private async Task IncrementAsync(string definition,long inspections,long incomplete,long candidates,long failures,CancellationToken token)
    {
        var day=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var dataset=ModerationTransparencyRecorder.Dataset(environment,configuration);
        // One atomic PostgreSQL UPSERT serializes concurrent increments; checked bigint overflow aborts source commit.
        var changed=await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO moderation_inspection_totals
                (day,environment,definition,completed_inspections,incomplete_history,created_candidates,failed_processing_attempts)
            VALUES ({day},{dataset},{definition},{inspections},{incomplete},{candidates},{failures})
            ON CONFLICT (day,environment,definition) DO UPDATE SET
                completed_inspections=moderation_inspection_totals.completed_inspections+EXCLUDED.completed_inspections,
                incomplete_history=moderation_inspection_totals.incomplete_history+EXCLUDED.incomplete_history,
                created_candidates=moderation_inspection_totals.created_candidates+EXCLUDED.created_candidates,
                failed_processing_attempts=moderation_inspection_totals.failed_processing_attempts+EXCLUDED.failed_processing_attempts
            """,token);
        if(changed!=1)throw new InvalidOperationException("Inspection total was not recorded.");
    }
    private static void Validate(string definition,bool incomplete)
    {
        if(definition is not(TextDefinition or ImageDefinition)||incomplete&&definition!=TextDefinition)
            throw new ArgumentException("Invalid bounded inspection definition.");
    }
}
