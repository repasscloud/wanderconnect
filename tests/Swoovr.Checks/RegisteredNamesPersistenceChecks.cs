using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Api.Verification;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

static class RegisteredNamesPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> sourceOptions,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(sourceOptions);var connection=source.Database.GetDbConnection().ConnectionString;
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();var name="swoovr_names_"+Guid.NewGuid().ToString("N");
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var options=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(connection){Database=name,Pooling=false}.ConnectionString).Options;
            await using var db=new SwoovrDbContext(options);await db.Database.MigrateAsync();
            var now=DateTimeOffset.UtcNow;var id=Guid.NewGuid();
            var account=new Account{Id=id,Issuer="https://names.example.test/realms/members",Subject=id.ToString(),Realm=AccountRealm.Members,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now};
            var profile=new MemberProfile{AccountId=id,DisplayName="Custom public name",AgeStatus=AgeStatus.SelfDeclaredAdult,DateOfBirth=new(1990,1,1),IdStatus=VerificationStatus.Verified};
            db.Accounts.Add(account);db.MemberProfiles.Add(profile);await db.SaveChangesAsync();
            check("migration invents no registered surname or confirmation",account.RegisteredLastName is null&&!RegisteredNamePolicy.Complete(account));
            RegisteredNamesService.SuggestFromTrustedClaims(account,new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("given_name","Provider"),new Claim("family_name","Suggestion")},"validated-fictional")));
            check("provider claims suggest but never auto-confirm",account.RegisteredFirstName=="Provider"&&account.RegisteredLastName=="Suggestion"&&!RegisteredNamePolicy.Complete(account));
            await db.SaveChangesAsync();
            var invalid=await RegisteredNamesService.SaveAsync(db,id,new("Fictional",null,false,0,true),now,default);
            check("missing surname cannot save without explicit mononym",invalid.Error=="invalid-registered-names"&&account.RegisteredNamesRevision==0);
            var saved=await RegisteredNamesService.SaveAsync(db,id,new("Fictional","Example",false,0,true),now,default);
            check("confirmed declaration retains issuer and subject",saved.Value is{Confirmed:true,Revision:1}&&account.Subject==id.ToString()&&account.Issuer=="https://names.example.test/realms/members");
            // Q125 A (D317): an ID-verified member's name change keeps the badge and opens one staff review (ids only).
            var reviews=await db.NameChangeReviews.Where(r=>r.AccountId==id).ToListAsync();
            check("name change keeps ID Verified and opens one staff review",profile.IdStatus==VerificationStatus.Verified&&reviews.Count==1&&reviews[0].ResolvedAt==null&&reviews[0].Revision==1&&account.RegisteredNamesMatchId is null);
            RegisteredNamesService.SuggestFromTrustedClaims(account,new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("given_name","Stale"),new Claim("family_name","Provider")},"validated-fictional")));
            check("later provider claims cannot reverse member-confirmed declarations",account.RegisteredFirstName=="Fictional"&&account.RegisteredLastName=="Example");
            check("custom public name is never overwritten by registered identity",profile.DisplayName=="Custom public name");
            check("registered audit never includes declared names",!await db.AuditEntries.AnyAsync(a=>a.Detail!=null&&(a.Detail.Contains("Fictional")||a.Detail.Contains("Example"))));
            var stale=await RegisteredNamesService.SaveAsync(db,id,new("Wrong","Writer",false,0,true),now,default);
            check("stale editor cannot overwrite registered names",stale.Error=="record-changed"&&account.RegisteredFirstName=="Fictional");
            var old=new IdVerificationAttempt{Id=Guid.NewGuid(),AccountId=id,SessionId=Guid.NewGuid().ToString(),State=IdAttemptState.Created,RegisteredNamesRevision=0,CreatedAt=now,UpdatedAt=now};db.IdVerificationAttempts.Add(old);await db.SaveChangesAsync();
            await VerificationEndpoints.ApplyAsync(db,old,new(old.SessionId,"Approved",id.ToString(),null,true,DateOfBirth:new(1990,1,1)),now,"test",default);
            check("late pre-edit provider result is never compared with the new name",account.RegisteredNamesMatchId is null&&await db.NameChangeReviews.CountAsync(r=>r.AccountId==id&&r.ResolvedAt==null)==1);
            var current=new IdVerificationAttempt{Id=Guid.NewGuid(),AccountId=id,SessionId=Guid.NewGuid().ToString(),State=IdAttemptState.Created,RegisteredNamesRevision=1,CreatedAt=now,UpdatedAt=now};db.IdVerificationAttempts.Add(current);await db.SaveChangesAsync();
            await VerificationEndpoints.ApplyAsync(db,current,new(current.SessionId,"Approved",id.ToString(),null,true,DateOfBirth:new(1990,1,1),Identity:new("Fictional","Example",new(1990,1,1),null,null,null)),now,"test",default);
            check("fresh approved result compares declaration without legal decryption",account.RegisteredNamesMatchId==true&&profile.IdStatus==VerificationStatus.Verified&&!await db.IdentityAccesses.AnyAsync());
            profile.DisplayName=null;await db.SaveChangesAsync();
            var mononym=await RegisteredNamesService.SaveAsync(db,id,new("Ari",null,true,1,true),now,default);
            check("explicit mononym clears declared surname and initializes unset public name only from first",mononym.Value is{Mononym:true,LastName:null}&&profile.DisplayName=="Ari");
            var inFlight=new IdVerificationAttempt{Id=Guid.NewGuid(),AccountId=id,SessionId=Guid.NewGuid().ToString(),State=IdAttemptState.InProgress,RegisteredNamesRevision=account.RegisteredNamesRevision,CreatedAt=now,UpdatedAt=now};db.IdVerificationAttempts.Add(inFlight);await db.SaveChangesAsync();
            var renamed=await RegisteredNamesService.SaveAsync(db,id,new("Ari","Example",false,account.RegisteredNamesRevision,true),now,default);
            check("name edit expires the in-flight ID attempt",renamed.Error is null&&inFlight.State==IdAttemptState.Expired);
            await VerificationEndpoints.ApplyAsync(db,inFlight,new(inFlight.SessionId,"Approved",id.ToString(),null,false,DateOfBirth:new(2012,1,1)),now,"test",default);
            check("confirmed underage result on a name-expired attempt still locks the account",account.Status==AccountStatus.Locked&&account.StatusReason=="underage"&&profile.AgeStatus==AgeStatus.Underage&&profile.IdStatus!=VerificationStatus.Verified&&inFlight.State==IdAttemptState.Expired);
            AccountDeletionPolicy.Tombstone(account,now);
            check("deletion tombstone removes registered names and ID comparison",account.RegisteredFirstName is null&&account.RegisteredLastName is null&&account.RegisteredNamesConfirmedAt is null&&account.RegisteredNamesMatchId is null);
        }
        finally{await using var drop=new NpgsqlCommand("DROP DATABASE "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();}
    }
}
