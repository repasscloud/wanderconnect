using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

static class OperationalRetentionChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);
        var connection=source.Database.GetDbConnection().ConnectionString;var name="swoovr_operational_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var owned=new NpgsqlConnectionStringBuilder(connection){Database=name,Pooling=false}.ConnectionString;
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(owned).Options);await db.Database.MigrateAsync();
            var config=new ConfigurationBuilder().AddInMemoryCollection().Build();
            using var services=new ServiceCollection().AddSingleton(db).AddSingleton<IConfiguration>(config).AddSingleton<IHostEnvironment>(new Development()).AddSingleton(TimeProvider.System).AddLogging().BuildServiceProvider();
            var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);
            var evidence=new EvidencePreservation(services,new EvidenceKeys(config,founders),new ContactHistoryKeys(config,founders));
            var purge=new OperationalRetention(db,evidence,new SafetyRetention(db,evidence));
            var now=DateTimeOffset.UtcNow;var member=Guid.NewGuid();var actor=Guid.NewGuid();
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Released,Reason="Fictional obsolete operational reason",PlacedByRole=AccountRole.Legal,
                PlacedAt=now.AddYears(-4),ReviewDue=now.AddYears(-4).AddDays(1),ReleasedAt=now.AddYears(-3),ReleasedBy=actor,
                RelatedAccountIds=[member],RetentionCaseIds=[],NoReviewRetentionConfirmedAt=now.AddYears(-3),NoReviewRetentionConfirmedBy=actor};
            db.LegalHolds.Add(hold);db.LegalHoldScopes.Add(new(){HoldId=hold.Id,Kind=HoldScopeKind.Account,TargetId=member});
            db.CustodyEvents.Add(new(){HoldId=hold.Id,At=now.AddYears(-3),Action="released",PreviousHash=[],Hash=EvidenceSeal.Chain([],hold.Id,now.AddYears(-3),null,"released",null)});
            var export=new EvidenceExport{Id=Guid.NewGuid(),HoldId=hold.Id,TokenHash=new byte[32],CreatedBy=actor,CreatedAt=now.AddYears(-3),ExpiresAt=now.AddYears(-3).AddMinutes(15),Purpose="Fictional expired purpose"};
            db.EvidenceExports.Add(export);db.ExportApprovals.Add(new(){Id=Guid.NewGuid(),HoldId=hold.Id,RequestedBy=actor,RequestedAt=now.AddYears(-3),Purpose="Fictional old approval"});await db.SaveChangesAsync();
            config["Evidence:HoldCheckUnavailable"]="true";
            check("unavailable preservation refuses operational expiry",(await purge.RunAsync(now,CancellationToken.None)).FailedClosed&&await db.LegalHolds.AnyAsync());
            config["Evidence:HoldCheckUnavailable"]="false";
            var overlap=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional overlapping preservation",PlacedByRole=AccountRole.Legal,PlacedAt=now,ReviewDue=now.AddMonths(1),RelatedAccountIds=[member],RetentionCaseIds=[]};
            db.LegalHolds.Add(overlap);db.LegalHoldScopes.Add(new(){HoldId=overlap.Id,Kind=HoldScopeKind.Account,TargetId=member});await db.SaveChangesAsync();
            check("overlapping live hold protects old operational group",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0&&await db.EvidenceExports.AnyAsync());
            overlap.RelatedAccountIds=null;await db.SaveChangesAsync();
            check("unknown overlapping hold membership refuses group erasure",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0);
            overlap.Status=LegalHoldStatus.Released;overlap.ReleasedAt=now;overlap.RelatedAccountIds=[member];await db.SaveChangesAsync();
            hold.RelatedAccountIds=null;await db.SaveChangesAsync();
            check("unknown legacy membership refuses operational expiry",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0);
            hold.RelatedAccountIds=[member];hold.ReleasedBy=null;await db.SaveChangesAsync();
            check("missing release actor is unresolved provenance, not disposal authority",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0);
            hold.ReleasedBy=actor;export.DownloadedAt=now;await db.SaveChangesAsync();
            check("late export starts independent operational window",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0);
            export.DownloadedAt=now.AddYears(-3);await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("CREATE FUNCTION fixture_hold_delete_fail() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'fictional disposal failure'; END $$; CREATE TRIGGER fixture_hold_delete_fail BEFORE DELETE ON legal_holds FOR EACH ROW EXECUTE FUNCTION fixture_hold_delete_fail();");
            var failed=false;try{await purge.RunAsync(now,CancellationToken.None);}catch(PostgresException){failed=true;}
            check("group disposal failure rolls back chain/export/approval erasure",failed&&await db.CustodyEvents.AnyAsync(c=>c.HoldId==hold.Id)&&await db.EvidenceExports.AnyAsync(e=>e.HoldId==hold.Id)&&await db.ExportApprovals.AnyAsync(a=>a.HoldId==hold.Id));
            await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fixture_hold_delete_fail ON legal_holds; DROP FUNCTION fixture_hold_delete_fail();");
            check("eligible released group expires after overlapping release",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==1);
            check("whole chain/export/approval/scope removed without partial chain",!await db.LegalHolds.AnyAsync(h=>h.Id==hold.Id)&&!await db.CustodyEvents.AnyAsync(c=>c.HoldId==hold.Id)&&!await db.EvidenceExports.AnyAsync(e=>e.HoldId==hold.Id)&&!await db.LegalHoldScopes.AnyAsync(s=>s.HoldId==hold.Id));
            check("repeat operational expiry is idempotent",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0);
            var police=new LeCase{Id=Guid.NewGuid(),Reference="LE-FICTIONAL-EXPIRY",Status=LeCaseStatus.Closed,Channel="email",Agency="Fictional Agency",OfficerName="Fictional Adult",OwnerId=actor,CreatedBy=actor,
                ReceivedAt=now.AddYears(-9),CreatedAt=now.AddYears(-9),ClosedAt=now.AddYears(-8),ClosedBy=actor,RetainUntil=now.AddYears(-1)};
            var policeHold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Released,Reason="Fictional old police metadata",PlacedByRole=AccountRole.Legal,PlacedAt=now.AddYears(-9),ReviewDue=now.AddYears(-9).AddMonths(1),ReleasedAt=now.AddYears(-8),ReleasedBy=actor,RelatedAccountIds=[member],RetentionCaseIds=[],LeCaseId=police.Id};
            db.LeCases.Add(police);db.LegalHolds.Add(policeHold);
            var policeExport=new EvidenceExport{Id=Guid.NewGuid(),HoldId=policeHold.Id,LeCaseId=police.Id,TokenHash=Enumerable.Repeat((byte)18,32).ToArray(),CreatedBy=actor,CreatedAt=now.AddYears(-8),ExpiresAt=now.AddYears(-8).AddMinutes(15),DownloadedAt=now.AddYears(-6),Purpose="Fictional police disclosure"};
            db.EvidenceExports.Add(policeExport);db.Disclosures.Add(new(){Id=Guid.NewGuid(),HoldId=policeHold.Id,Agency="Fictional Agency",Officer="Fictional Adult",Reference="Fictional reference",LegalBasis="warrant",VerifiedHow="Fictional isolated fixture",DisclosedBy=actor,DisclosedAt=now.AddYears(-8)});
            var transfer=new LeTransfer{Id=Guid.NewGuid(),CaseId=police.Id,ExportId=policeExport.Id,PackageSha256=new byte[32],Recipient="Fictional recipient",Method="secure-link",SentBy=actor,SentAt=now.AddYears(-8),ReceiptAt=now.AddYears(-8)};
            db.LeTransfers.Add(transfer);db.EvidenceRedactions.Add(new(){Id=Guid.NewGuid(),ExportId=policeExport.Id,ItemId=Guid.NewGuid(),Fields="fictional.field",Reason="Fictional redaction",RedactedBy=actor,At=now.AddYears(-8)});await db.SaveChangesAsync();
            check("police export activity retains metadata for seven calendar years",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==0);
            using var accountServices=new ServiceCollection().AddSingleton(db).AddSingleton(evidence).AddSingleton(new SafetyRetention(db,evidence)).BuildServiceProvider();
            // No source accounts/provider sessions exist in this owned DB; only the actual expired-police-record path runs.
            var accounts=new AccountDeletion(accountServices,null!,null!,founders,config,NullLogger<AccountDeletion>.Instance);
            check("expired police source stays while its operational group is retained",(await accounts.PurgeAsync(now,CancellationToken.None)).CasesExpired==0&&await db.LeCases.AnyAsync(c=>c.Id==police.Id));
            policeExport.DownloadedAt=now.AddYears(-8);await db.SaveChangesAsync();
            check("lawful police group expires after the latest seven-year activity window",(await purge.RunAsync(now,CancellationToken.None)).HoldsDisposed==1);
            check("police disclosure/redaction/transfer expire with the group",!await db.Disclosures.AnyAsync(d=>d.HoldId==policeHold.Id)&&!await db.EvidenceRedactions.AnyAsync(d=>d.ExportId==policeExport.Id)&&!await db.LeTransfers.AnyAsync(t=>t.Id==transfer.Id));
            check("police record expiry resumes after related operational disposal",(await accounts.PurgeAsync(now,CancellationToken.None)).CasesExpired==1&&!await db.LeCases.AnyAsync(c=>c.Id==police.Id));
            check("empty legacy inventory is import-ready",!(await RegistryLegacyAudit.ReadAsync(db,now,CancellationToken.None)).RequiresReview);
            var legacyHash=Enumerable.Repeat((byte)17,32).ToArray();
            db.TerminatedEmailHolds.Add(new(){EmailHash=legacyHash,CreatedAt=now.AddYears(-1),ExpiresAt=now.AddYears(1)});
            db.TerminatedPhoneHolds.Add(new(){PhoneHash=new byte[31],CreatedAt=now.AddYears(-3),ExpiresAt=now.AddYears(-1)});await db.SaveChangesAsync();
            var legacy=await RegistryLegacyAudit.ReadAsync(db,now,CancellationToken.None);
            check("legacy inventory distinguishes type/expiry/malformed without inventing provenance",legacy is{Emails:1,Mobiles:1,Expired:1,Malformed:1,RequiresReview:true});
            check("legacy inspection never creates a blocking restriction or case",!await db.BannedIdentifiers.AnyAsync()&&!await db.ModerationCases.AnyAsync());
            check("legacy inspection preserves original bytes and clocks",(await db.TerminatedEmailHolds.AsNoTracking().SingleAsync()).EmailHash.SequenceEqual(legacyHash));

        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE IF EXISTS "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    sealed class Development:IHostEnvironment
    {
        public string EnvironmentName{get;set;}=Environments.Development;public string ApplicationName{get;set;}="Swoovr.Checks";
        public string ContentRootPath{get;set;}="/";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    sealed class Sink:IEmailSender{public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>Task.FromResult(false);}
}
