using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Moderation;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

// PostgreSQL checks run only against the harness-owned migrated disposable database.
// Exercises the real source-checkpoint+counter transaction pattern, not measured classifier accuracy/integration.
static class ModerationInspectionTotalChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using(var gate=new SwoovrDbContext(options))
        {
            var name=gate.Database.GetDbConnection().Database;
            if(!name.StartsWith("swoovr_check_",StringComparison.Ordinal)&&!name.StartsWith("swoovr_checks_",StringComparison.Ordinal))
                throw new InvalidOperationException("Inspection checks require the owned disposable database.");
        }
        var clock=new Clock();var host=new Host();var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()).Build();
        var day=DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);var definition=ModerationInspectionRecorder.TextDefinition;
        async Task<long[]> Counts(string selectedDefinition)
        {
            await using var view=new SwoovrDbContext(options);
            var row=await view.Set<ModerationInspectionTotal>().AsNoTracking().SingleOrDefaultAsync(r=>r.Day==day&&r.Environment=="local-synthetic"&&r.Definition==selectedDefinition);
            return row is null?[0,0,0,0]:[row.CompletedInspections,row.IncompleteHistory,row.CreatedCandidates,row.FailedProcessingAttempts];
        }
        var baseline=await Counts(definition);var sourceAccount=Guid.NewGuid();var first=Guid.NewGuid();var second=Guid.NewGuid();
        var key=RandomNumberGenerator.GetBytes(32);
        await using(var db=new SwoovrDbContext(options))
        {
            db.Accounts.Add(new(){Id=sourceAccount,Issuer="https://synthetic.example.test/inspection-counts",Subject=sourceAccount.ToString(),Realm=AccountRealm.Members,
                Status=AccountStatus.Active,CreatedAt=clock.GetUtcNow(),LastSeenAt=clock.GetUtcNow()});
            foreach(var id in new[]{first,second})db.Set<TextHarmIntent>().Add(new(){Id=id,AccountId=sourceAccount,Source=TextHarmSource.ProfileAbout,SourceId=sourceAccount,
                Revision=Guid.NewGuid(),CreatedAt=clock.GetUtcNow(),SealedText=EvidenceSeal.Seal(1,key,JsonSerializer.Serialize("Fictional anonymous-total source fixture"))});
            await db.SaveChangesAsync();
            var refused=false;try{await new ModerationInspectionRecorder(db,host,config,clock).CompletedAsync(definition,first,false,false,default);}catch(InvalidOperationException){refused=true;}
            check("anonymous completion cannot run outside source transaction",refused);
            var properties=db.Model.FindEntityType(typeof(ModerationInspectionTotal))!.GetProperties().Select(p=>p.Name).Order().ToArray();
            check("anonymous daily storage has only bounded dimensions and four counts",properties.SequenceEqual(new[]{"Day","Environment","Definition","CompletedInspections","IncompleteHistory","CreatedCandidates","FailedProcessingAttempts"}.Order()));
        }
        async Task<(bool SourceChanged,bool DuplicateIgnored)> Complete(Guid id,bool commit,bool incomplete)
        {
            await using var db=new SwoovrDbContext(options);
            await using var owner=await AccountLifecycleLease.AcquireAsync(db,sourceAccount,default);
            await using var preservation=await PreservationLease.ExclusiveAsync(db,default);
            await using var transaction=await db.Database.BeginTransactionAsync();
            var source=await db.Set<TextHarmIntent>().SingleAsync(i=>i.Id==id);
            if(source.ProcessedAt is not null){await transaction.CommitAsync();return(false,false);}
            source.ProcessedAt=clock.GetUtcNow();source.SealedText=null;source.Result="no-signal";
            var recorder=new ModerationInspectionRecorder(db,host,config,clock);
            var firstCall=await recorder.CompletedAsync(definition,id,incomplete,false,default);
            var duplicate=await recorder.CompletedAsync(definition,id,incomplete,false,default);
            await db.SaveChangesAsync();
            if(commit)await transaction.CommitAsync();else await transaction.RollbackAsync();
            return(firstCall,!duplicate);
        }
        var aborted=await Complete(first,false,false);
        await using(var observe=new SwoovrDbContext(options))
            check("rollback preserves pending source and rolls back anonymous total",aborted.SourceChanged&&aborted.DuplicateIgnored
                &&await observe.Set<TextHarmIntent>().AnyAsync(i=>i.Id==first&&i.ProcessedAt==null&&i.SealedText!=null)
                &&(await Counts(definition)).SequenceEqual(baseline));
        var committed=await Complete(first,true,false);var replay=await Complete(first,true,false);
        var one=await Counts(definition);
        check("durable same-source replay counts one completed inspection only",committed.SourceChanged&&committed.DuplicateIgnored&&!replay.SourceChanged
            &&one[0]==baseline[0]+1&&one[1]==baseline[1]&&one[2]==baseline[2]&&one[3]==baseline[3]);
        // Two concurrent source producers share the existing source lock; only one sees a pending transition.
        var start=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<(bool SourceChanged,bool DuplicateIgnored)> Producer(){await start.Task;return await Complete(second,true,true);}
        var producerA=Producer();var producerB=Producer();start.SetResult(true);var concurrent=await Task.WhenAll(producerA,producerB);
        var two=await Counts(definition);
        check("concurrent same-source retries cannot double count inspection or incomplete history",concurrent.Count(r=>r.SourceChanged)==1
            &&two[0]==baseline[0]+2&&two[1]==baseline[1]+1);
        // Two calls model distinct worker catches: attempts, not deduplicated incidents. No exception/id payload exists.
        async Task Failure()
        {await using var db=new SwoovrDbContext(options);await new ModerationInspectionRecorder(db,host,config,clock).FailedAttemptAsync(definition,default);}
        await Task.WhenAll(Failure(),Failure());var failures=await Counts(definition);
        check("concurrent failed attempts increment atomically without inventing completion or candidate",failures[0]==two[0]&&failures[1]==two[1]&&failures[2]==two[2]&&failures[3]==two[3]+2);
        await using(var db=new SwoovrDbContext(options))
        await using(var preservation=await PreservationLease.SharedAsync(db,default))
        await using(var transaction=await db.Database.BeginTransactionAsync())
        {
            var before=await Counts(ModerationInspectionRecorder.ImageDefinition);
            var recorder=new ModerationInspectionRecorder(db,host,config,clock);
            await recorder.CompletedAsync(ModerationInspectionRecorder.ImageDefinition,Guid.NewGuid(),false,true,default);
            await transaction.CommitAsync();var after=await Counts(ModerationInspectionRecorder.ImageDefinition);
            check("bounded definitions keep actual image indicator counters separate",after[0]==before[0]+1&&after[1]==before[1]&&after[2]==before[2]+1);
        }
        await using(var db=new SwoovrDbContext(options))
        await using(var preservation=await PreservationLease.SharedAsync(db,default))
        await using(var transaction=await db.Database.BeginTransactionAsync())
        {
            var rejected=false;try{await new ModerationInspectionRecorder(db,host,config,clock).CompletedAsync("unbounded-private-definition",Guid.NewGuid(),false,false,default);}catch(ArgumentException){rejected=true;}
            check("recorder refuses unknown definition dimensions",rejected);
            rejected=false;try{await new ModerationInspectionRecorder(db,host,config,clock).CompletedAsync(ModerationInspectionRecorder.ImageDefinition,Guid.NewGuid(),true,false,default);}catch(ArgumentException){rejected=true;}
            check("image counters cannot claim text-history completeness",rejected);
            var configured=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"Reporting:Dataset","production"}}).Build();
            rejected=false;try{await new ModerationInspectionRecorder(db,host,configured,clock).CompletedAsync(definition,Guid.NewGuid(),false,false,default);}catch(InvalidOperationException){rejected=true;}
            check("Development host cannot label inspection totals production",rejected);
        }
        await using(var db=new SwoovrDbContext(options))
        {
            var rows=await db.Set<ModerationInspectionTotal>().AsNoTracking().Where(r=>r.Day==day&&r.Environment=="local-synthetic").ToListAsync();
            check("daily counters use UTC day and keep synthetic environment explicit",day==new DateOnly(2090,1,1)&&rows.Count>=2&&rows.All(r=>r.Day==day&&r.Environment=="local-synthetic"));
        }
        // Bigint overflow must abort the source transaction, not commit a completion without its denominator.
        var overflowDay=day.AddDays(-1);var overflowSource=Guid.NewGuid();
        await using(var db=new SwoovrDbContext(options))
        {
            db.Set<TextHarmIntent>().Add(new(){Id=overflowSource,AccountId=sourceAccount,Source=TextHarmSource.ProfileAbout,SourceId=sourceAccount,Revision=Guid.NewGuid(),CreatedAt=clock.GetUtcNow(),
                SealedText=EvidenceSeal.Seal(1,key,JsonSerializer.Serialize("Fictional overflow source fixture"))});
            db.Set<ModerationInspectionTotal>().Add(new(){Day=overflowDay,Environment="local-synthetic",Definition=definition,CompletedInspections=long.MaxValue});await db.SaveChangesAsync();
        }
        var failed=false;
        await using(var db=new SwoovrDbContext(options))
        {
            await using var authority=await PreservationLease.ExclusiveAsync(db,default);
            await using var transaction=await db.Database.BeginTransactionAsync();
            var source=await db.Set<TextHarmIntent>().SingleAsync(i=>i.Id==overflowSource);source.ProcessedAt=clock.GetUtcNow();source.SealedText=null;source.Result="no-signal";
            var overflowClock=new Clock{Now=clock.GetUtcNow().AddDays(-1)};
            try{await new ModerationInspectionRecorder(db,host,config,overflowClock).CompletedAsync(definition,overflowSource,false,false,default);await db.SaveChangesAsync();await transaction.CommitAsync();}
            catch(PostgresException error)when(error.SqlState==PostgresErrorCodes.NumericValueOutOfRange){failed=true;await transaction.RollbackAsync();}
        }
        await using(var db=new SwoovrDbContext(options))
            check("counter write failure cannot lose pending source work",failed&&await db.Set<TextHarmIntent>().AnyAsync(i=>i.Id==overflowSource&&i.ProcessedAt==null&&i.SealedText!=null)
                &&await db.Set<ModerationInspectionTotal>().AnyAsync(r=>r.Day==overflowDay&&r.Environment=="local-synthetic"&&r.Definition==definition&&r.CompletedInspections==long.MaxValue));
    }
    private sealed class Clock:TimeProvider
    {public DateTimeOffset Now=new(2090,1,2,13,59,0,TimeSpan.FromHours(14));public override DateTimeOffset GetUtcNow()=>Now.ToUniversalTime();}
    private sealed class Host:IHostEnvironment
    {public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
