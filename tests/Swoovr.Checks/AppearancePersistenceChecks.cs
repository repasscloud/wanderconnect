using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class AppearancePersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);var original=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(original);await admin.OpenAsync();var name="swoovr_appearance_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var connection=new NpgsqlConnectionStringBuilder(original){Database=name,Pooling=false}.ConnectionString;
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            var now=DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var member=Guid.NewGuid();db.Accounts.Add(new(){Id=member,Issuer="https://appearance.example.test/realms/members",Subject=member.ToString(),Email="fictional.appearance@example.test",Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});
            db.MemberProfiles.Add(new(){AccountId=member,PublicId=Guid.NewGuid(),DateOfBirth=new(1990,1,1),AgeStatus=AgeStatus.SelfDeclaredAdult});await db.SaveChangesAsync();
            var store=new AppearanceStore(db,new Clock(now));
            check("appearance GET absent never writes",await store.LoadAsync(member,default) is null&&!await db.Set<MemberAppearance>().AnyAsync());
            foreach(var update in new[]{new AppearanceUpdate(new("retired","system"),0,Guid.NewGuid()),new(new("standard","auto"),0,Guid.NewGuid()),new(new("standard","system"),-1,Guid.NewGuid()),new(new("standard","system"),0,Guid.Empty)})
                check("appearance rejects unsupported values/revision/retry identity",(await store.SaveAsync(member,update,default)).Outcome=="invalid");
            var first=new AppearanceUpdate(new("eucalyptus","system"),0,Guid.NewGuid());
            var saved=await store.SaveAsync(member,first,default);check("first explicit appearance write creates revision1",saved.Outcome=="saved"&&saved.Snapshot==new AppearanceSnapshot(first.Preference,1));
            var repeated=await store.SaveAsync(member,first,default);check("appearance exact retry is idempotent",repeated.Outcome=="saved"&&repeated.Snapshot==saved.Snapshot);
            check("appearance retry ID cannot carry a different preference",(await store.SaveAsync(member,first with{Preference=new("pride","dark")},default)).Outcome=="invalid");
            check("stale appearance write returns current revision without overwrite",(await store.SaveAsync(member,first with{ChangeId=Guid.NewGuid()},default)) is{Outcome:"conflict",Snapshot.Revision:1});
            async Task<AppearanceStoreResult> Write(string scheme){await using var other=new SwoovrDbContext(options);return await new AppearanceStore(other,new Clock(now)).SaveAsync(member,new(new(scheme,"dark"),1,Guid.NewGuid()),default);}
            var writes=await Task.WhenAll(Write("pride"),Write("coral"));check("concurrent appearance revisions have one winner",writes.Count(r=>r.Outcome=="saved")==1&&writes.Count(r=>r.Outcome=="conflict")==1&&writes.All(r=>r.Snapshot?.Revision==2));
            await db.Set<MemberAppearance>().Where(a=>a.AccountId==member).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Scheme,"retired").SetProperty(a=>a.Mode,"unknown"));
            check("unknown stored appearance displays safe default with original revision",await store.LoadAsync(member,default)==new AppearanceSnapshot(new("standard","system"),2));
            check("reading unknown appearance never repairs stored values",await db.Set<MemberAppearance>().AnyAsync(a=>a.AccountId==member&&a.Scheme=="retired"&&a.Mode=="unknown"&&a.Revision==2));
            var maxId=Guid.NewGuid();await db.Set<MemberAppearance>().Where(a=>a.AccountId==member).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Scheme,"standard").SetProperty(a=>a.Mode,"system").SetProperty(a=>a.Revision,long.MaxValue).SetProperty(a=>a.LastChangeId,maxId));
            check("appearance revision exhaustion is explicit",(await store.SaveAsync(member,new(new("coral","dark"),long.MaxValue,Guid.NewGuid()),default)).Outcome=="exhausted");
            check("last accepted appearance retry still works at maximum revision",(await store.SaveAsync(member,new(new("standard","system"),long.MaxValue-1,maxId),default)).Snapshot?.Revision==long.MaxValue);
            await db.Accounts.Where(a=>a.Id==member).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,AccountStatus.PendingDeletion));
            check("appearance write refuses inactive account after lifecycle recheck",(await store.SaveAsync(member,new(new("coral","dark"),0,Guid.NewGuid()),default)).Outcome=="unavailable");
            var racing=Guid.NewGuid();db.Accounts.Add(new(){Id=racing,Issuer="https://appearance.example.test/realms/members",Subject=racing.ToString(),Email="fictional.appearance-race@example.test",Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});await db.SaveChangesAsync();
            await using(var blocker=new SwoovrDbContext(options))
            {
                var application="appearance_wait_"+Guid.NewGuid().ToString("N");
                await using var other=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection){ApplicationName=application}.ConnectionString).Options);
                var lease=await AccountLifecycleLease.AcquireAsync(blocker,racing,default);
                var write=new AppearanceStore(other,new Clock(now)).SaveAsync(racing,new(new("coral","system"),0,Guid.NewGuid()),default);
                var waiting=false;AppearanceStoreResult? outcome=null;
                try
                {
                    for(var n=0;n<100&&!waiting;n++)
                    {
                        await using var probe=new NpgsqlCommand("SELECT EXISTS(SELECT 1 FROM pg_stat_activity WHERE application_name=@name AND wait_event_type='Lock')",admin);probe.Parameters.AddWithValue("name",application);
                        waiting=(bool)(await probe.ExecuteScalarAsync())!;if(!waiting)await Task.Delay(20);
                    }
                    await blocker.Accounts.Where(a=>a.Id==racing).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Status,AccountStatus.PendingDeletion));
                }
                finally{await lease.DisposeAsync();outcome=await write;}
                check("appearance write positively contends with deletion lifecycle",waiting);
                check("appearance cannot recreate preference after contested deletion starts",outcome?.Outcome=="unavailable"&&!await db.Set<MemberAppearance>().AnyAsync(a=>a.AccountId==racing));
            }
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
    private sealed class Clock(DateTimeOffset value):TimeProvider{public override DateTimeOffset GetUtcNow()=>value;}
}
