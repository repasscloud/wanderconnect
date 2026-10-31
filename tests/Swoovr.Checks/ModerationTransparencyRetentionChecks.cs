using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Moderation;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class ModerationTransparencyRetentionChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(options);
        var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();
        var name="swoovr_annual_retention_"+Guid.NewGuid().ToString("N");
        await using(var command=new NpgsqlCommand("CREATE DATABASE "+name,admin))await command.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options);await db.Database.MigrateAsync();
            var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Reporting:Dataset"]="production"}).Build();
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(new Host()).AddSingleton(TimeProvider.System).AddLogging().BuildServiceProvider();
            var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);
            var evidence=new EvidencePreservation(services,new EvidenceKeys(config,founders),new ContactHistoryKeys(config,founders));
            var reports=new ModerationTransparencyReports(db,new(db,TimeProvider.System),TimeProvider.System);
            var retention=new ModerationTransparencyRetention(db,evidence,reports,TimeProvider.System);
            var now=DateTimeOffset.UtcNow;var old=now.AddYears(-5);var year=old.Month>=7?old.Year+1:old.Year;
            var member=Guid.NewGuid();var staff=Guid.NewGuid();
            foreach(var id in new[]{member,staff})db.Accounts.Add(new(){Id=id,Issuer="https://annual-retention.example.test",Subject=id.ToString(),Realm=id==member?AccountRealm.Members:AccountRealm.Staff,Status=AccountStatus.Active,CreatedAt=old,LastSeenAt=now});
            await db.SaveChangesAsync();
            Guid receipt;
            await using(var tx=await db.Database.BeginTransactionAsync())
            {
                var sanction=new Sanction{Id=Guid.NewGuid(),AccountId=member,Kind=SanctionKind.Termination,StartsAt=old,CreatedBy=staff,CreatedByRole=AccountRole.Admin,Reason="Fictional old annual fixture"};db.Sanctions.Add(sanction);
                receipt=(await new ModerationTransparencyRecorder(db,new Host(),config,TimeProvider.System).StageSanctionAsync(sanction,old,null,default)).Id;
                db.Set<ModerationInspectionTotal>().Add(new(){Day=DateOnly.FromDateTime(old.UtcDateTime),Environment="production",Definition=ModerationInspectionRecorder.TextDefinition,CompletedInspections=5});
                await db.SaveChangesAsync();await tx.CommitAsync();
            }
            bool refused=false;try{await db.Set<ModerationTransparencyFact>().Where(f=>f.Id==receipt).ExecuteDeleteAsync();}catch(PostgresException e)when(e.SqlState=="23514"){refused=true;}
            check("annual old receipt cannot disappear before aggregate archive",refused);
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional preservation fixture",PlacedBy=staff,PlacedByRole=AccountRole.Legal,PlacedAt=old,ReviewDue=now.AddDays(1),RelatedAccountIds=[member],RetentionCaseIds=[]};
            db.LegalHolds.Add(hold);db.LegalHoldScopes.Add(new(){HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=member});await db.SaveChangesAsync();
            check("annual purge succeeds while respecting held subject",await retention.PurgeAsync(default));
            check("annual purge archives before expiry and preserves held receipt",await db.Set<ModerationTransparencyFact>().AnyAsync(f=>f.Id==receipt)&&await db.Set<ModerationTransparencySnapshot>().AnyAsync(s=>s.EndingYear==year));
            check("anonymous expired daily rows dispose independently",!await db.Set<ModerationInspectionTotal>().AnyAsync());
            hold.Status=LegalHoldStatus.Released;hold.ReleasedAt=now;hold.ReleasedBy=staff;hold.ReleaseReason="Fictional scope release";await db.SaveChangesAsync();
            hold.RelatedAccountIds=null;await db.SaveChangesAsync();
            check("annual released hold with unresolved provenance fails closed",!await retention.PurgeAsync(default)&&await db.Set<ModerationTransparencyFact>().AnyAsync(f=>f.Id==receipt));
            refused=false;try{await db.Set<ModerationTransparencyFact>().Where(f=>f.Id==receipt).ExecuteDeleteAsync();}catch(PostgresException e)when(e.SqlState=="23514"){refused=true;}
            check("annual database also refuses unresolved released-hold disposal",refused);
            hold.RelatedAccountIds=[member];await db.SaveChangesAsync();
            check("annual purge resumes after hold release",await retention.PurgeAsync(default)&&!await db.Set<ModerationTransparencyFact>().AnyAsync(f=>f.Id==receipt));
            var archived=await reports.ReadAsync(year,default);
            check("annual archived report survives receipt disposal without re-exposing small cells",archived.Counts.Where(c=>c.Status!="not-recorded").All(c=>c.Count is null));
            refused=false;try{await reports.SaveAsync(year,default);}catch(ArgumentException){refused=true;}
            check("annual expired year refuses recomputation from partial remaining sources",refused);
            check("annual repeat purge retains immutable archive",await retention.PurgeAsync(default)&&await db.Set<ModerationTransparencySnapshot>().CountAsync()==1);
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Host:IHostEnvironment
    {public string EnvironmentName{get;set;}="Production";public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
    private sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>throw new InvalidOperationException("No delivery in annual retention checks.");}
}
