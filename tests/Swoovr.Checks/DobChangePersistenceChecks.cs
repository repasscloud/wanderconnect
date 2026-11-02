using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Verification;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

// Real PostgreSQL inside PersistenceChecks' disposable database, never installed application records.
static class DobChangePersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var db=new SwoovrDbContext(options);
        using var services=new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var key=RandomNumberGenerator.GetBytes(32);
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["IdentityStore:Keys:1"]=Convert.ToBase64String(key)}).Build();
        var keys=new IdentityKeys(config,new Founders(services,config,new UnconfiguredEmailSender(),NullLogger<Founders>.Instance));
        var now=DateTimeOffset.UtcNow;
        var owner=new Account{Id=Guid.NewGuid(),Realm=AccountRealm.Members,Issuer="https://issuer.example.test",Subject=Guid.NewGuid().ToString(),Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};
        var previous=new DateOnly(1990,1,1);var corrected=new DateOnly(1991,2,2);
        var profile=new MemberProfile{AccountId=owner.Id,PublicId=Guid.NewGuid(),DateOfBirth=previous,AgeStatus=AgeStatus.SelfDeclaredAdult};
        var grant=new DobChangeGrant{Id=Guid.NewGuid(),AccountId=owner.Id,GrantedBy=Guid.NewGuid(),Reason="Fictional correction check",CreatedAt=now,ExpiresAt=now+DobChangePolicy.Window};
        db.Accounts.Add(owner);db.MemberProfiles.Add(profile);db.DobChangeGrants.Add(grant);await db.SaveChangesAsync();
        check("Q116 populated unverified date locks immediately",!DobChangePolicy.CanSave(profile.DateOfBirth,true,null,owner.Id,now));
        check("Q116 wrong member cannot consume",!await DobChangeEndpoints.ConsumeAsync(db,grant.Id,Guid.NewGuid(),now,default));
        check("Q116 expired boundary cannot consume",!await DobChangeEndpoints.ConsumeAsync(db,grant.Id,owner.Id,now+DobChangePolicy.Window,default));
        await using(var tx=await db.Database.BeginTransactionAsync())
        {
            check("Q116 valid grant consumes within update transaction",await DobChangeEndpoints.ConsumeAsync(db,grant.Id,owner.Id,now,default));
            DobChangeEndpoints.PreservePrevious(db,keys,profile,corrected,grant.Id,now);
            profile.DateOfBirth=corrected;await db.SaveChangesAsync();
            // Simulate a downstream failure: disposing without commit must undo consumption, history and date together.
            await tx.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        check("Q116 failure rolls back grant consumption",(await db.DobChangeGrants.FindAsync(grant.Id))!.ConsumedAt is null);
        check("Q116 failure rolls back date and history",(await db.MemberProfiles.FindAsync(owner.Id))!.DateOfBirth==previous&&!await db.DobChangeHistories.AnyAsync(h=>h.AccountId==owner.Id));
        async Task<bool> Consume(){await using var other=new SwoovrDbContext(options);return await DobChangeEndpoints.ConsumeAsync(other,grant.Id,owner.Id,now,default);}
        var race=await Task.WhenAll(Consume(),Consume());
        check("Q116 concurrent consumers have exactly one winner",race.Count(won=>won)==1);
        check("Q116 replay refused",!await DobChangeEndpoints.ConsumeAsync(db,grant.Id,owner.Id,now,default));
        profile=await db.MemberProfiles.FindAsync(owner.Id)??throw new Exception("Missing fictional member");
        DobChangeEndpoints.PreservePrevious(db,keys,profile,corrected,grant.Id,now);profile.DateOfBirth=corrected;await db.SaveChangesAsync();
        var sealedHistory=await db.DobChangeHistories.SingleAsync(h=>h.AccountId==owner.Id);
        check("Q116 database history contains ciphertext, not the old date",!System.Text.Encoding.UTF8.GetString(sealedHistory.Sealed).Contains("1990-01-01"));
        var ownHistory=await DobChangeEndpoints.HistoryAsync(db,keys,owner.Id,default);
        var document=JsonSerializer.Serialize(await DataExportBuilder.BuildAsync(db,owner,now,default,dateOfBirthHistory:ownHistory));
        check("Q116 own export includes prior date without staff authority data",document.Contains("1990-01-01")&&!document.Contains(grant.GrantedBy.ToString()));
        var unrelated=await DobChangeEndpoints.HistoryAsync(db,keys,Guid.NewGuid(),default);
        check("Q116 history query is member-bound",unrelated.Count==0);
        // Real revoked grant refuses even before its expiry.
        var revoked=new DobChangeGrant{Id=Guid.NewGuid(),AccountId=owner.Id,GrantedBy=grant.GrantedBy,CreatedAt=now,ExpiresAt=now+DobChangePolicy.Window,RevokedAt=now};
        db.DobChangeGrants.Add(revoked);await db.SaveChangesAsync();
        check("Q116 revoked grant cannot consume",!await DobChangeEndpoints.ConsumeAsync(db,revoked.Id,owner.Id,now,default));
        await db.DobChangeHistories.Where(h=>h.AccountId==owner.Id).ExecuteDeleteAsync();
        await db.DobChangeGrants.Where(g=>g.AccountId==owner.Id).ExecuteDeleteAsync();
        db.Accounts.Remove(owner);await db.SaveChangesAsync();
    }
}
