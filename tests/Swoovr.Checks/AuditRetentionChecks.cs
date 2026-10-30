using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class AuditRetentionChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(options);
        var sourceConnection=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(sourceConnection);await admin.OpenAsync();
        var name="swoovr_audit_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(sourceConnection){Database=name,Pooling=false}.ConnectionString;
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options);await db.Database.MigrateAsync();
            var now=DateTimeOffset.UtcNow;
            await SensitiveAccessMiddleware.AppendAsync(db,now.AddMonths(-25),null,"view","identity",null,200,CancellationToken.None);
            await SensitiveAccessMiddleware.AppendAsync(db,now.AddMonths(-25).AddSeconds(1),null,"query","holds",null,200,CancellationToken.None);
            await SensitiveAccessMiddleware.AppendAsync(db,now,null,"view","identity",null,200,CancellationToken.None);
            var old=await db.SensitiveAccessLog.AsNoTracking().OrderBy(e=>e.Sequence).ToListAsync();
            var grant=new AccessGrant{Id=Guid.NewGuid(),AccountId=Guid.NewGuid(),Capability="identity-open",Purpose="Fictional expired purpose",GrantedAt=now.AddMonths(-26),ExpiresAt=now.AddMonths(-26).AddHours(1)};
            var incident=new SecurityIncident{Id=Guid.NewGuid(),Kind="manual",Summary="Fictional expired incident",Status=IncidentStatus.Closed,DetectedAt=now.AddMonths(-27),AssessmentDue=now.AddMonths(-27).AddDays(30),ClosedAt=now.AddMonths(-25),ClosedBy=Guid.NewGuid(),AssessedAt=now.AddMonths(-26),AssessedBy=Guid.NewGuid(),Notifiable=false};
            db.AccessGrants.Add(grant);db.SecurityIncidents.Add(incident);
            db.AuditEntries.Add(new(){At=now.AddMonths(-25),Actor="system",Action="fictional.expired"});await db.SaveChangesAsync();
            var hold=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,PlacedAt=now,ReviewDue=now.AddDays(30),RelatedAccountIds=[],RetentionCaseIds=[],Reason="Fictional audit preservation",PlacedByRole=AccountRole.Legal};
            db.LegalHolds.Add(hold);await db.SaveChangesAsync();
            check("audit prefix remains during active preservation",await AuditRetention.RunAsync(db,now,CancellationToken.None)==0);
            check("preservation retains expired operational purposes and incidents",await db.AccessGrants.AnyAsync(g=>g.Id==grant.Id)&&await db.SecurityIncidents.AnyAsync(i=>i.Id==incident.Id));
            hold.Status=LegalHoldStatus.Released;await db.SaveChangesAsync();
            check("unresolved released hold metadata still refuses audit expiry",await AuditRetention.RunAsync(db,now,CancellationToken.None)==0);
            db.LegalHolds.Remove(hold);await db.SaveChangesAsync();
            check("eligible audit prefix expires as one bounded checkpoint",await AuditRetention.RunAsync(db,now,CancellationToken.None)==2);
            var remaining=await db.SensitiveAccessLog.AsNoTracking().OrderBy(e=>e.Sequence).ToListAsync();
            check("audit retained suffix preserves original hashes",remaining.Count==1&&remaining[0].Hash.SequenceEqual(old[2].Hash)&&remaining[0].PreviousHash.SequenceEqual(old[1].Hash));
            check("audit checkpoint verifies surviving suffix",await AuditRetention.IntactAsync(db,remaining,CancellationToken.None));
            check("null audit expiry authority is refused",await Refused(db,"SELECT swoovr_expire_access_prefix(NULL)"));
            check("future audit expiry authority is refused",await Refused(db,"SELECT swoovr_expire_access_prefix(now()+interval '1 year')"));
            check("audit expiry is idempotent",await AuditRetention.RunAsync(db,now,CancellationToken.None)==0);
            await SensitiveAccessMiddleware.AppendAsync(db,now.AddMonths(-25),null,"change","staff",null,200,CancellationToken.None);
            check("backdated tail never skips retained prefix",await AuditRetention.RunAsync(db,now,CancellationToken.None)==0);
            await AuditRetention.RunAsync(db,now,CancellationToken.None);
            check("expired unused grant purpose is removed",!await db.AccessGrants.AnyAsync(g=>g.Id==grant.Id));
            check("closed assessed incident expires after latest activity",!await db.SecurityIncidents.AnyAsync(i=>i.Id==incident.Id));
            check("ordinary aged audit metadata expires",!await db.AuditEntries.AnyAsync(a=>a.Action=="fictional.expired"));
            check("operational metadata disposal records a minimal completion",await db.AuditEntries.AnyAsync(a=>a.Action=="retention.operational-metadata-expired"));
            check("ordinary audit deletion remains forbidden",await Refused(db,"DELETE FROM sensitive_access_log"));
            check("ordinary audit update remains forbidden",await Refused(db,"UPDATE sensitive_access_log SET status=201"));
            check("audit truncate remains forbidden",await Refused(db,"TRUNCATE sensitive_access_log"));
            await EmptyPrefix(sourceConnection,now,check);
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    static async Task EmptyPrefix(string sourceConnection,DateTimeOffset now,Action<string,bool> check)
    {
        await using var admin=new NpgsqlConnection(sourceConnection);await admin.OpenAsync();
        var name="swoovr_audit_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(sourceConnection){Database=name,Pooling=false}.ConnectionString).Options);await db.Database.MigrateAsync();
            await SensitiveAccessMiddleware.AppendAsync(db,now.AddMonths(-25),null,"query","identity",null,200,CancellationToken.None);
            var hash=(await db.SensitiveAccessLog.AsNoTracking().SingleAsync()).Hash;
            check("whole expired log leaves a checkpoint",await AuditRetention.RunAsync(db,now,CancellationToken.None)==1&&!await db.SensitiveAccessLog.AnyAsync());
            await SensitiveAccessMiddleware.AppendAsync(db,now,null,"query","identity",null,200,CancellationToken.None);
            var next=await db.SensitiveAccessLog.AsNoTracking().SingleAsync();
            check("new access entry extends an emptied log checkpoint",next.PreviousHash.SequenceEqual(hash)&&await AuditRetention.IntactAsync(db,[next],CancellationToken.None));
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    static async Task<bool> Refused(SwoovrDbContext db,string sql)
    {try{await db.Database.ExecuteSqlRawAsync(sql);return false;}catch(PostgresException){return true;}}
}
