using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Swoovr.Api.Moderation;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

// Actual source/receipt transactions and aggregate revisions in the owned migrated PostgreSQL database.
static class ModerationTransparencyPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using(var gate=new SwoovrDbContext(options))
        {
            var name=gate.Database.GetDbConnection().Database;
            if(!name.StartsWith("swoovr_check_",StringComparison.Ordinal)&&!name.StartsWith("swoovr_transparency_",StringComparison.Ordinal))
                throw new InvalidOperationException("Transparency checks require an owned disposable database.");
        }
        var now=DateTimeOffset.UtcNow;var actor=Guid.NewGuid();var member=Guid.NewGuid();var other=Guid.NewGuid();var caseId=Guid.NewGuid();
        var au=1900000000+Random.Shared.Next(100000);var nz=au+100001;var sourceId=Guid.NewGuid();
        var configuration=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>()).Build();
        var metadata=new ModerationTransparencyMetadata("harassment","staff-review","own-initiative","none");
        ModerationTransparencyRecorder Recorder(SwoovrDbContext db)=>new(db,new Host("Development"),configuration,new FixedClock(now));
        await using(var db=new SwoovrDbContext(options))
        {
            Account A(Guid id,AccountRealm realm)=>new(){Id=id,Realm=realm,Issuer="https://issuer.example.test/"+realm,Subject="fictional-transparency-"+id,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};
            db.Accounts.AddRange(A(actor,AccountRealm.Staff),A(member,AccountRealm.Members),A(other,AccountRealm.Members));
            db.GeoCities.AddRange(new(){GeoNameId=au,Name="Fictional AU",AsciiName="Fictional AU",CountryCode="AU",CountryName="Australia",TimeZone="UTC"},
                new(){GeoNameId=nz,Name="Fictional NZ",AsciiName="Fictional NZ",CountryCode="NZ",CountryName="New Zealand",TimeZone="UTC"});
            db.MemberProfiles.Add(new(){AccountId=member,PublicId=Guid.NewGuid(),HomeGeoNameId=au,About="Fictional private original body",AgeStatus=AgeStatus.Attested});
            db.ModerationCases.Add(new(){Id=caseId,SubjectAccountId=member,Status=CaseStatus.Open,Queue=CaseQueue.Admin,CreatedAt=now,UpdatedAt=now});await db.SaveChangesAsync();
        }
        Sanction S(Guid id)=>new(){Id=id,AccountId=member,CaseId=caseId,Kind=SanctionKind.Termination,Reason="Fictional private staff reason",StartsAt=now,CreatedBy=actor,CreatedByRole=AccountRole.Admin};
        await using(var db=new SwoovrDbContext(options))
        {
            await using var tx=await db.Database.BeginTransactionAsync();var source=S(sourceId);db.Sanctions.Add(source);
            var fact=await Recorder(db).StageSanctionAsync(source,now,metadata,default);await db.SaveChangesAsync();await tx.CommitAsync();
            check("transparency new source captures declared AU with explicit provenance",fact.HomeCountryAtAction=="AU"&&fact.CountryProvenance=="member-declared-home-at-action"&&fact.SourceCaseId==caseId);
            check("transparency local environment never production",fact.Environment=="local-synthetic"&&fact.EnforcementMechanism=="human");
            check("transparency explicit basis recorded separately from private reason",fact.BasisReviewed&&fact.ReviewedPolicyBasis=="harassment");
            var json=JsonSerializer.Serialize(fact);
            check("transparency receipt excludes body and staff reason",!json.Contains("Fictional private",StringComparison.Ordinal)&&!typeof(ModerationTransparencyFact).GetProperties().Any(p=>p.Name is "Text" or "Reason" or "Email" or "CountryCode" or "HomeGeoNameId"));
        }
        await using(var db=new SwoovrDbContext(options))
        {
            var profile=await db.MemberProfiles.SingleAsync(p=>p.AccountId==member);profile.HomeGeoNameId=nz;await db.SaveChangesAsync();
            await using var tx=await db.Database.BeginTransactionAsync();var source=await db.Sanctions.SingleAsync(s=>s.Id==sourceId);
            var prior=await Recorder(db).StageSanctionAsync(source,now,metadata,default);await tx.CommitAsync();
            check("transparency receipt replay after country move preserves original AU",prior.HomeCountryAtAction=="AU"&&await db.Set<ModerationTransparencyFact>().CountAsync(f=>f.SourceId==sourceId)==1);
        }
        var rollbackId=Guid.NewGuid();
        await using(var db=new SwoovrDbContext(options))
        {
            await using var tx=await db.Database.BeginTransactionAsync();var source=S(rollbackId);db.Sanctions.Add(source);
            var fact=await Recorder(db).StageSanctionAsync(source,now,null,default);await db.SaveChangesAsync();
            check("transparency unknown basis never inferred from reason",!fact.BasisReviewed&&fact.ReviewedPolicyBasis=="unknown"&&fact.HomeCountryAtAction=="other");
            await tx.RollbackAsync();
        }
        await using(var db=new SwoovrDbContext(options))
            check("transparency source and receipt rollback together",!await db.Sanctions.AnyAsync(s=>s.Id==rollbackId)&&!await db.Set<ModerationTransparencyFact>().AnyAsync(f=>f.SourceId==rollbackId));
        var historical=Guid.NewGuid();
        await using(var db=new SwoovrDbContext(options))
        {
            db.Sanctions.Add(S(historical));await db.SaveChangesAsync();
            await using var tx=await db.Database.BeginTransactionAsync();var source=await db.Sanctions.SingleAsync(s=>s.Id==historical);
            bool refused=false;try{await Recorder(db).StageSanctionAsync(source,now,null,default);}catch(InvalidOperationException){refused=true;}
            check("transparency old unrecorded source cannot acquire today's country",refused&&!db.Set<ModerationTransparencyFact>().Local.Any(f=>f.SourceId==historical));await tx.RollbackAsync();
        }
        await using(var db=new SwoovrDbContext(options))
        {
            var original=await db.Set<ModerationTransparencyFact>().AsNoTracking().SingleAsync(f=>f.SourceId==sourceId);
            var duplicate=Copy(original);duplicate.Id=Guid.NewGuid();db.Set<ModerationTransparencyFact>().Add(duplicate);
            bool refused=false;try{await db.SaveChangesAsync();}catch(DbUpdateException error)when(error.InnerException is PostgresException{SqlState:"23505"}){refused=true;}
            check("transparency database unique source receipt",refused);
        }
        await using(var db=new SwoovrDbContext(options))
        {
            var original=await db.Set<ModerationTransparencyFact>().SingleAsync(f=>f.SourceId==sourceId);original.HomeCountryAtAction="other";
            bool refused=false;try{await db.SaveChangesAsync();}catch(DbUpdateException error)when(error.InnerException is PostgresException{SqlState:"23514"}){refused=true;}
            check("transparency database rejects original-country rewrite",refused);
        }
        await using(var db=new SwoovrDbContext(options))
        {
            var original=await db.Set<ModerationTransparencyFact>().AsNoTracking().SingleAsync(f=>f.SourceId==sourceId);
            await using var tx=await db.Database.BeginTransactionAsync();var invalid=Copy(original);invalid.Id=Guid.NewGuid();invalid.SourceId=historical;invalid.SubjectAccountId=other;
            db.Set<ModerationTransparencyFact>().Add(invalid);bool refused=false;
            try{await db.SaveChangesAsync();await tx.CommitAsync();}catch(PostgresException error)when(error.SqlState=="23514"){refused=true;}
            catch(DbUpdateException error)when(error.InnerException is PostgresException{SqlState:"23514"}){refused=true;}
            check("transparency deferred source subject mismatch refuses commit",refused);
        }
        // A completed, isolated reporting year: synthetic fixtures explicitly exercise the production projection.
        var at=new DateTimeOffset(2024,8,1,0,0,0,TimeSpan.Zero);
        var production=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Reporting:Dataset"]="production"}).Build();
        for(var n=0;n<5;n++)
        {
            await using var db=new SwoovrDbContext(options);
            var id=Guid.NewGuid();
            db.Accounts.Add(new(){Id=id,Realm=AccountRealm.Members,Issuer="https://annual.example.test",Subject=id.ToString(),Status=AccountStatus.Active,CreatedAt=at,LastSeenAt=at});
            db.MemberProfiles.Add(new(){AccountId=id,PublicId=Guid.NewGuid(),HomeGeoNameId=au,AgeStatus=AgeStatus.Attested});await db.SaveChangesAsync();
            await using var tx=await db.Database.BeginTransactionAsync();
            var source=new Sanction{Id=Guid.NewGuid(),AccountId=id,Kind=SanctionKind.Termination,Reason="Fictional private annual reason",StartsAt=at,CreatedBy=actor,CreatedByRole=AccountRole.Admin};
            db.Sanctions.Add(source);await new ModerationTransparencyRecorder(db,new Host("Production"),production,new FixedClock(now)).StageSanctionAsync(source,at,metadata,default);
            await db.SaveChangesAsync();await tx.CommitAsync();
        }
        await using(var db=new SwoovrDbContext(options))
        {
            var query=new ModerationTransparencyQuery(db,new FixedClock(now));var report=await query.ExportAsync(2025,default);
            check("annual query counts five distinct declared AU accounts in completed UTC period",report.Counts.Single(c=>c.Metric=="terminated-declared-au-unique-accounts").Count==5);
            check("annual query separates explicit reviewed basis",report.Counts.Single(c=>c.Metric=="terminated-declared-au-unique-accounts-by-reviewed-basis"&&c.Category=="harassment").Count==5);
            check("annual unknown accuracy and training stay unmeasured",report.Counts.Where(c=>c.Status=="not-recorded").All(c=>c.Count is null)&&report.TrainingStatus.StartsWith("not-recorded"));
            var encoded=JsonSerializer.Serialize(report);
            check("annual aggregate excludes source and subject identifiers and private content",!encoded.Contains(sourceId.ToString())&&!encoded.Contains(member.ToString())&&!encoded.Contains("Fictional private"));
            var reports=new ModerationTransparencyReports(db,query,new FixedClock(now));
            var firstRevision=await reports.SaveAsync(2025,default);var replayRevision=await reports.SaveAsync(2025,default);
            check("annual snapshot save is idempotent for identical aggregate",firstRevision==1&&replayRevision==1&&await db.Set<ModerationTransparencySnapshot>().CountAsync(r=>r.EndingYear==2025)==1);
            var saved=await reports.ReadAsync(2025,default);
            check("annual saved snapshot round trips its measured subset",saved.Counts.Single(c=>c.Metric=="terminated-declared-au-unique-accounts").Count==5);
            bool refused=false;try{await db.Set<ModerationTransparencySnapshot>().Where(r=>r.EndingYear==2025).ExecuteDeleteAsync();}catch(PostgresException error)when(error.SqlState=="23514"){refused=true;}
            check("annual database prevents deleting immutable saved revision",refused);
        }
        await using(var db=new SwoovrDbContext(options))
        {
            bool refused=false;try{await db.Set<ModerationTransparencyFact>().Where(f=>f.SourceId==sourceId).ExecuteDeleteAsync();}catch(PostgresException error)when(error.SqlState=="23514"){refused=true;}
            check("annual database prevents premature receipt disposal",refused);
        }
        var requestedProduction=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Reporting:Dataset"]="production"}).Build();
        bool wrongHost=false;try{ModerationTransparencyRecorder.Dataset(new Host("UAT"),requestedProduction);}catch(InvalidOperationException){wrongHost=true;}
        check("transparency UAT cannot opt into production dataset",wrongHost);
        check("transparency implicit Production default remains unknown",ModerationTransparencyRecorder.Dataset(new Host("Production"),configuration)=="unknown");
    }
    private static ModerationTransparencyFact Copy(ModerationTransparencyFact value)=>JsonSerializer.Deserialize<ModerationTransparencyFact>(JsonSerializer.Serialize(value))!;
    private sealed class FixedClock(DateTimeOffset at):TimeProvider{public override DateTimeOffset GetUtcNow()=>at;}
    private sealed class Host(string name):IHostEnvironment
    {public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="Swoovr.Checks";public string ContentRootPath{get;set;}="/tmp";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
