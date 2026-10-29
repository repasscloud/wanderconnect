using System.Security.Claims;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Swoovr.Api.Auth;
using Swoovr.Api.Evidence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

/// <summary>Disposable PostgreSQL durable command/reconciliation checks; provider outcomes are explicit fakes.</summary>
static class EmailRetirementPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        await using var source=new SwoovrDbContext(options);
        var connection=source.Database.GetDbConnection().ConnectionString;
        var name="swoovr_email_retire_"+Guid.NewGuid().ToString("N");
        await using var admin=new NpgsqlConnection(connection);await admin.OpenAsync();
        await using(var create=new NpgsqlCommand("CREATE DATABASE "+name,admin))await create.ExecuteNonQueryAsync();
        try
        {
            var owned=new NpgsqlConnectionStringBuilder(connection){Database=name,Pooling=false}.ConnectionString;
            var isolated=new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(owned).Options;
            await using(var migration=new SwoovrDbContext(isolated))await migration.Database.MigrateAsync();
            await RunOwned(isolated,check);
        }
        finally
        {
            await using var drop=new NpgsqlCommand("DROP DATABASE IF EXISTS "+name+" WITH (FORCE)",admin);await drop.ExecuteNonQueryAsync();
        }
    }
    private static async Task RunOwned(DbContextOptions<SwoovrDbContext> options,Action<string,bool> check)
    {
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["Beta:Enabled"]="false",
            ["Bans:HmacKeys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)25,32).ToArray()),
            ["ContactHistory:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)23,32).ToArray()),
            ["ContactHistory:LookupKey"]=Convert.ToBase64String(Enumerable.Repeat((byte)24,32).ToArray())}).Build();
        using var services=new ServiceCollection().BuildServiceProvider();
        var founders=new Founders(services,config,new Sink(),NullLogger<Founders>.Instance);
        var keys=new ContactHistoryKeys(config,founders);
        var bans=new BannedIdentifierKeys(config,new Development(),founders);
        var commands=new EmailRetirementCommands(keys);var fake=new Provider();
        var now=DateTimeOffset.UtcNow;var owner=Guid.NewGuid();var actor=Guid.NewGuid();var caseId=Guid.NewGuid();var subject=Guid.NewGuid();
        await using var db=new SwoovrDbContext(options);
        var account=new Account{Id=owner,Issuer=Provider.Issuer,Subject=subject.ToString(),Realm=AccountRealm.Members,Status=AccountStatus.Active,
            EmailRetiredAt=now,Email=null,EmailAcceptedAt=now,SessionsRevokedAt=SessionRevocationPolicy.Cutoff(now),CreatedAt=now.AddYears(-1),LastSeenAt=now};
        db.Accounts.AddRange(account,new Account{Id=actor,Issuer="https://issuer.example.test/realms/staff",Subject="fictional-retirement-reviewer-"+actor,
            Realm=AccountRealm.Staff,IsRootAdmin=true,Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});
        db.MemberProfiles.Add(new MemberProfile{AccountId=owner,PublicId=Guid.NewGuid(),DisplayName="Fictional Retired Tester",AgeStatus=AgeStatus.Attested,DateOfBirth=new DateOnly(1990,1,1)});
        db.ModerationCases.Add(new ModerationCase{Id=caseId,SubjectAccountId=owner,Status=CaseStatus.Open,Queue=CaseQueue.Admin,CreatedAt=now,UpdatedAt=now});
        var source=BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,bans.Fingerprint(BannedIdentifierType.Email,"fictional.retired@example.test",1),1,caseId,actor,now.AddDays(-1),"Fictional reviewed source.");
        var release=BannedIdentifierPolicy.Revoke(source.Entry,actor,"Fictional recycled contact review.",now);
        db.BannedIdentifiers.Add(source.Entry);db.BanTransitions.AddRange(source.Transition,release.Transition);db.BanCaseNotes.AddRange(source.Note,release.Note);
        db.Sanctions.Add(new Sanction{Id=Guid.NewGuid(),AccountId=owner,CaseId=caseId,Kind=SanctionKind.Termination,StartsAt=now.AddDays(-1),CreatedBy=actor,CreatedByRole=AccountRole.Admin,Reason="Fictional source termination"});
        var grant=new AccessGrant{Id=Guid.NewGuid(),AccountId=actor,Capability=StaffAccessPolicy.IdentifierRelease,Purpose="Fictional recycled contact review",GrantedAt=now,ExpiresAt=now.AddHours(1)};
        db.AccessGrants.Add(grant);await db.SaveChangesAsync();
        var operation=new EmailRetirementOperation{Id=Guid.NewGuid(),AccountId=owner,RegistryId=source.Entry.Id,CaseId=caseId,RequestedBy=actor,AccessGrantId=grant.Id,
            RequestedAt=now,UpdatedAt=now,NextAttemptAt=now};
        var binding=new IdentityEmailBinding(subject,"fictional.retired@example.test","fictional.unrelated.login",true,true);
        operation.EncryptedCommand=commands.Seal(operation,Provider.Issuer,binding)!;
        check("sealed command has no raw contact text",!System.Text.Encoding.UTF8.GetString(operation.EncryptedCommand).Contains("example.test"));
        check("sealed command preserves exact unrelated login",commands.Open(operation,Provider.Issuer)==binding);
        var original=operation.RegistryId;operation.RegistryId=Guid.NewGuid();var refused=false;
        try{commands.Open(operation,Provider.Issuer);}catch(InvalidOperationException){refused=true;}operation.RegistryId=original;
        check("swapped command provenance is refused",refused);
        await using(var tx=await db.Database.BeginTransactionAsync()){
            db.EmailRetirementOperations.Add(operation);await db.SaveChangesAsync();await tx.RollbackAsync();}
        db.ChangeTracker.Clear();check("rolled back intent has no durable command",!await db.EmailRetirementOperations.AnyAsync(o=>o.Id==operation.Id));
        db.EmailRetirementOperations.Add(operation);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        var processorClock=new Clock(now);
        async Task Process(){await using var replica=new SwoovrDbContext(options);await new EmailRetirementProcessor(replica,fake,commands,processorClock,new BannedIdentifierRegistry(replica,bans,new EvidencePreservation(services,new EvidenceKeys(config,founders),keys))).ProcessAsync(operation.Id,CancellationToken.None);}
        async Task<CurrentAccount?> Resolve(DateTimeOffset? at=null)
        {
            await using var replica=new SwoovrDbContext(options);
            using var dependencies=new ServiceCollection().AddSingleton(replica).AddSingleton<IHostEnvironment>(new Development()).AddSingleton<IConfiguration>(config).AddLogging().BuildServiceProvider();
            var later=at??now.AddMinutes(1);var principal=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("iss",Provider.Issuer),new Claim("sub",subject.ToString()),
                new Claim("email","fictional.retired@example.test"),new Claim("email_verified","true"),new Claim("amr","pwd"),
                new Claim("auth_time",later.ToUnixTimeSeconds().ToString()),new Claim("iat",later.ToUnixTimeSeconds().ToString())},"fictional-validated-facts"));
            var keycloak=new KeycloakOptions{BaseUrl="https://issuer.example.test",MembersRealm="members"};
            return await new AccountResolver(dependencies,keycloak,new SessionEvidencePolicy(new()),new Clock(later),config,new Development(),NullLogger<AccountResolver>.Instance).ResolveAsync(principal,CancellationToken.None);
        }
        check("actual resolver refuses pending original subject even with newer hypothetical verified claims",await Resolve() is null);
        fake.Status=IdentityContactState.Unavailable;await Process();
        var pending=await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id);
        check("provider outage keeps durable refusal and retries without a post",pending.State==EmailRetirementState.Pending&&pending.Attempts==1&&fake.Posts==0);
        check("retired source mail remains suppressed during outage",await db.Accounts.AnyAsync(a=>a.Id==owner&&a.Email==null&&a.EmailRetiredAt!=null));
        await db.AccessGrants.Where(g=>g.Id==grant.Id).ExecuteUpdateAsync(u=>u.SetProperty(g=>g.RevokedAt,now));
        fake.Status=IdentityContactState.NotRecorded;await Process();
        check("revoked approval cannot authorize a new provider effect",fake.Posts==0&&(await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id)).State==EmailRetirementState.NeedsReview);
        // Restore only this synthetic approval and pending review in the throwaway fixture, not a product recovery endpoint.
        await db.AccessGrants.Where(g=>g.Id==grant.Id).ExecuteUpdateAsync(u=>u.SetProperty(g=>g.RevokedAt,(DateTimeOffset?)null));
        await db.EmailRetirementOperations.Where(o=>o.Id==operation.Id).ExecuteUpdateAsync(u=>u.SetProperty(o=>o.State,EmailRetirementState.Pending));
        fake.Configured=false;var beforeDisabled=(await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id)).Attempts;await Process();
        check("disabled configuration retains intent without provider I/O or invented review outcome",fake.Posts==0&&(await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id)).Attempts==beforeDisabled);
        fake.Configured=true;
        var additional=BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,bans.Fingerprint(BannedIdentifierType.Email,binding.Email!,1),1,caseId,actor,now,"Fictional restriction added after preparation.");
        db.BannedIdentifiers.Add(additional.Entry);db.BanTransitions.Add(additional.Transition);db.BanCaseNotes.Add(additional.Note);await db.SaveChangesAsync();
        fake.Status=IdentityContactState.NotRecorded;await Process();
        check("restriction added after preparation prevents the first provider write",fake.Posts==0&&(await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id)).State==EmailRetirementState.NeedsReview);
        var additionalRelease=BannedIdentifierPolicy.Revoke(additional.Entry,actor,"Fictional separately reviewed later release.",now);
        db.BanTransitions.Add(additionalRelease.Transition);db.BanCaseNotes.Add(additionalRelease.Note);await db.SaveChangesAsync();
        await db.EmailRetirementOperations.Where(o=>o.Id==operation.Id).ExecuteUpdateAsync(u=>u.SetProperty(o=>o.State,EmailRetirementState.Pending));
        fake.Status=IdentityContactState.NotRecorded;fake.CommitThenThrow=true;
        var interrupted=false;try{await Process();}catch(HttpRequestException){interrupted=true;}
        check("injected crash after fake provider commit leaves completion unclaimed",interrupted&&fake.Posts==1&&(await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id)).State==EmailRetirementState.Pending);
        await db.AccessGrants.Where(g=>g.Id==grant.Id).ExecuteUpdateAsync(u=>u.SetProperty(g=>g.RevokedAt,now));
        db.StaffExclusions.Add(new StaffExclusion{StaffAccountId=actor,SubjectAccountId=owner,CreatedBy=actor,CreatedAt=now,Reason="Fictional later conflict"});await db.SaveChangesAsync();
        processorClock.Now=now.AddMinutes(10);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION fictional_email_completion_fault() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.state='Completed' THEN RAISE EXCEPTION 'fictional completion commit fault'; END IF;RETURN NEW;END $$;
            CREATE CONSTRAINT TRIGGER fictional_email_completion_fault AFTER UPDATE ON email_retirement_operations
              DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION fictional_email_completion_fault();
            """);
        try
        {
            var faulted=false;try{await Process();}catch(Exception error)when(error is DbUpdateException or PostgresException){faulted=true;}
            check("deferred completion failure leaves command pending",faulted&&(await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id)).State==EmailRetirementState.Pending);
            check("completion failure rolls back token cutoff and case audit atomically",await db.Accounts.AnyAsync(a=>a.Id==owner&&a.SessionsRevokedAt==SessionRevocationPolicy.Cutoff(now))
                &&!await db.AuditEntries.AnyAsync(a=>a.Action=="registry.email-retirement-state"&&a.TargetId==operation.Id.ToString()&&a.Detail=="Completed"));
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER fictional_email_completion_fault ON email_retirement_operations;DROP FUNCTION fictional_email_completion_fault();");}
        await Process();var completed=await db.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id);
        check("new replica reconciles committed receipt after human grant revocation and conflict",completed.State==EmailRetirementState.Completed&&completed.CompletedAt!=null&&fake.Posts==1);
        check("completion keeps encrypted evidence instead of silently erasing held material",completed.EncryptedCommand.SequenceEqual(operation.EncryptedCommand));
        await Process();check("completed retries never issue another provider attempt",fake.Posts==1);
        check("tokens issued during delayed pending window remain refused after completion",await Resolve() is null);
        var resolved=await Resolve(now.AddMinutes(11));
        check("completed source never re-adopts claimed retired email",resolved is not null&&resolved.VerifiedEmail is null&&resolved.Account!.Email is null
            &&await db.Accounts.AnyAsync(a=>a.Id==owner&&a.Email==null));
        async Task<bool> SqlRefused(FormattableString sql){try{await db.Database.ExecuteSqlInterpolatedAsync(sql);return false;}catch(PostgresException error)when(error.MessageText.StartsWith("email retirement")){return true;}}
        check("premature direct deletion is blocked",await SqlRefused($"DELETE FROM email_retirement_operations WHERE id={operation.Id}"));
        check("provenance cannot be rewritten",await SqlRefused($"UPDATE email_retirement_operations SET account_id={Guid.NewGuid()} WHERE id={operation.Id}"));
        check("completion cannot be reverted",await SqlRefused($"UPDATE email_retirement_operations SET state='Pending',completed_at=NULL WHERE id={operation.Id}"));
        check("truncate cannot bypass pending or preservation retention",await SqlRefused($"TRUNCATE email_retirement_operations"));
        var rotatedConfig=new ConfigurationBuilder().AddConfiguration(config).AddInMemoryCollection(new Dictionary<string,string?>{
            ["ContactHistory:Keys:2"]=Convert.ToBase64String(Enumerable.Repeat((byte)26,32).ToArray())}).Build();
        var rotatedKeys=new ContactHistoryKeys(rotatedConfig,founders);
        await using(var rotating=new SwoovrDbContext(options))
        {
            using var dependencies=new ServiceCollection().AddSingleton(rotating).BuildServiceProvider();
            var rotation=new Swoovr.Api.Security.KeyRotation(dependencies,rotatedKeys,new EvidenceKeys(config,founders),new Swoovr.Api.Verification.IdentityKeys(config,founders));
            check("key status includes restricted command envelopes",(await rotation.StatusAsync("contact-history",CancellationToken.None)).GetValueOrDefault((byte)1)>0);
            var result=await rotation.RotateAsync("contact-history",2,CancellationToken.None);
            var resealed=await rotating.EmailRetirementOperations.AsNoTracking().SingleAsync(o=>o.Id==operation.Id);
            check("key rotation reseals commands without changing current-state completion",result.Resealed>0&&resealed.EncryptedCommand[0]==2&&resealed.State==EmailRetirementState.Completed
                &&new EmailRetirementCommands(rotatedKeys).Open(resealed,Provider.Issuer)==binding);
        }
        // Two hundred retained commands must not hide a later disposable one. Historical clocks are explicit fixtures.
        var prefix=new List<Guid>();var history=new List<EmailRetirementOperation>();var last=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        for(var index=0;index<201;index++)
        {
            var member=Guid.NewGuid();var reviewId=Guid.NewGuid();var providerSubject=Guid.NewGuid();if(index<200)prefix.Add(member);
            db.Accounts.Add(new Account{Id=member,Issuer=Provider.Issuer,Subject=providerSubject.ToString(),Realm=AccountRealm.Members,Status=AccountStatus.Active,
                EmailRetiredAt=now.AddYears(-3),CreatedAt=now.AddYears(-4),LastSeenAt=now.AddYears(-3)});
            db.ModerationCases.Add(new ModerationCase{Id=reviewId,SubjectAccountId=member,Status=CaseStatus.Closed,Queue=CaseQueue.Admin,
                CreatedAt=now.AddYears(-4),UpdatedAt=now.AddYears(-3),ClosedAt=now.AddYears(-3),EvidencePurgedAt=now.AddYears(-3)});
            var prior=BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,new byte[32],1,reviewId,actor,now.AddYears(-4),"Fictional historical source.");
            var end=BannedIdentifierPolicy.Revoke(prior.Entry,actor,"Fictional historical release.",now.AddYears(-3));
            db.BannedIdentifiers.Add(prior.Entry);db.BanTransitions.AddRange(prior.Transition,end.Transition);db.BanCaseNotes.AddRange(prior.Note,end.Note);
            var item=new EmailRetirementOperation{Id=index==200?last:Guid.Parse("00000000-0000-0000-0000-"+(index+1).ToString("x12")),AccountId=member,
                RegistryId=prior.Entry.Id,CaseId=reviewId,RequestedBy=actor,AccessGrantId=grant.Id,RequestedAt=now.AddYears(-3),UpdatedAt=now.AddYears(-3),
                NextAttemptAt=now.AddYears(-3),CompletedAt=now.AddYears(-3),State=EmailRetirementState.Completed,Outcome="Retired"};
            item.EncryptedCommand=commands.Seal(item,Provider.Issuer,new(providerSubject,"fictional.historical@example.test","fictional.historical",true,true))!;history.Add(item);
        }
        await db.SaveChangesAsync();db.EmailRetirementOperations.AddRange(history);
        var held=new LegalHold{Id=Guid.NewGuid(),Status=LegalHoldStatus.Active,Reason="Fictional held retirement prefix",Source="staff",PlacedBy=actor,
            PlacedByRole=AccountRole.Legal,PlacedAt=now,ReviewDue=now.AddMonths(1),RelatedAccountIds=prefix.ToArray(),RetentionCaseIds=[]};
        db.LegalHolds.Add(held);await db.SaveChangesAsync();db.ChangeTracker.Clear();
        async Task Sweep()
        {
            await using var replica=new SwoovrDbContext(options);
            using var dependencies=new ServiceCollection().AddSingleton(replica).AddSingleton<IHostEnvironment>(new Development()).AddSingleton<IConfiguration>(config).AddLogging().BuildServiceProvider();
            var evidence=new EvidencePreservation(dependencies,new EvidenceKeys(config,founders),keys);
            var result=await new RegistryDisposal(replica,evidence).RunAsync(DateTimeOffset.UtcNow,CancellationToken.None);
            check("command retention sweep has known preservation authority",!result.FailedClosed);
        }
        await Sweep();
        check("held prefix does not starve a later eligible encrypted command",!await db.EmailRetirementOperations.AnyAsync(o=>o.Id==last)
            &&await db.EmailRetirementOperations.CountAsync(o=>prefix.Contains(o.AccountId))==200);
        await db.LegalHolds.Where(h=>h.Id==held.Id).ExecuteUpdateAsync(u=>u.SetProperty(h=>h.Status,LegalHoldStatus.Released));
        await Sweep();
        check("release resumes bounded command disposal without losing active source refusal",!await db.EmailRetirementOperations.AnyAsync(o=>prefix.Contains(o.AccountId))
            &&await db.EmailRetirementOperations.AnyAsync(o=>o.Id==operation.Id)&&await db.Accounts.AnyAsync(a=>a.Id==owner&&a.EmailRetiredAt!=null));
        // Fixture deliberately remains in this throwaway database until its owner drops it; no guard bypass/real data touched.
    }
    sealed class Clock(DateTimeOffset now):TimeProvider{public DateTimeOffset Now=now;public override DateTimeOffset GetUtcNow()=>Now;}
    sealed class Development:IHostEnvironment
    {
        public string EnvironmentName{get;set;}=Environments.Development;public string ApplicationName{get;set;}="Swoovr.Checks";
        public string ContentRootPath{get;set;}="/";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
    }
    sealed class Sink:IEmailSender {public Task<bool> SendAsync(EmailMessage message,CancellationToken token)=>Task.FromResult(false);}
    sealed class Provider:IContactIdentityAdmin
    {
        public const string Issuer="https://issuer.example.test/realms/members";
        public bool Configured=true;public bool ContactRetirementConfigured=>Configured;public string ContactIssuer=>Issuer;
        public IdentityContactState Status;public bool CommitThenThrow;public int Posts;
        public Task<IdentityEmailBindingResult> ReadEmailBindingAsync(Guid subject,CancellationToken token)=>throw new NotSupportedException();
        public Task<IdentityContactState> EmailRetirementStatusAsync(Guid subject,Guid operation,CancellationToken token)=>Task.FromResult(Status);
        public Task<IdentityContactState> RetireEmailBindingAsync(IdentityEmailBinding binding,Guid operation,CancellationToken token){Posts++;if(CommitThenThrow){Status=IdentityContactState.Retired;throw new HttpRequestException("Fictional post-commit crash");}return Task.FromResult(Status);}
    }
}
