using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Swoovr.Api;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Media;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

/// <summary>Actual API orchestration with throwaway PostgreSQL and explicit fake providers; never real user deletion.</summary>
static class StaffConflictPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var connectionSource = new SwoovrDbContext(options);
        var folder = Path.Combine(Path.GetTempPath(), "swoovr-conflict-" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Swoovr"] = connectionSource.Database.GetDbConnection().ConnectionString,
            ["Founders:Secrets:1"] = Convert.ToHexString(Enumerable.Repeat((byte)17, 32).ToArray()),
            ["Media:Store"] = "local", ["Media:Local:Path"] = folder, ["Media:Screening:Worker"] = "false",
        }).Build();
        var environment = new DevelopmentEnvironment(); var identity = new FakeIdentity();
        var services = new ServiceCollection();
        services.AddLogging();services.AddSingleton<IConfiguration>(config);services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton(TimeProvider.System);services.AddScoped<Founders>();services.AddSwoovrProviders(config);
        services.AddSwoovrPersistence(config.GetConnectionString("Swoovr"));services.AddSwoovrEmail(null, 0, "fictional@example.test");
        services.AddSwoovrMedia(config, environment, true);
        services.AddSingleton<IIdentityAdmin>(identity);services.AddSingleton<IIdVerificationProvider>(new NoVerification());
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        try
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SwoovrDbContext>();
            var now = DateTimeOffset.UtcNow;var member = Guid.NewGuid();var staff = Guid.NewGuid();var independent = Guid.NewGuid();
            db.Accounts.AddRange(new Account { Id = member, Issuer = "https://issuer.example.test/realms/members", Subject = "fictional-" + member,
                Email = "fictional.conflict@example.test", Realm = AccountRealm.Members, Status = AccountStatus.PendingDeletion, CreatedAt = now.AddYears(-3), LastSeenAt = now.AddYears(-3),
                DeletionRequestedAt = now.AddDays(-31), DeletionDueAt = now.AddMinutes(-1), DeletionRestoreStatus = AccountStatus.Active },
                new Account { Id = staff, Issuer = "https://issuer.example.test/realms/staff", Subject = "fictional-" + staff, Realm = AccountRealm.Staff, Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now },
                new Account { Id = independent, Issuer = "https://issuer.example.test/realms/staff", Subject = "fictional-" + independent, Realm = AccountRealm.Staff, Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now });
            db.MemberProfiles.Add(new MemberProfile { AccountId = member, PublicId = Guid.NewGuid(), DisplayName = "Fictional Conflict Tester", AgeStatus = AgeStatus.Attested, DateOfBirth = new DateOnly(1990, 1, 1) });
            db.StaffMemberLinks.Add(new StaffMemberLink { StaffAccountId = staff, MemberAccountId = member, LinkedAt = now.AddYears(-3), LinkedBy = independent });
            await db.SaveChangesAsync();
            var evidence = scope.ServiceProvider.GetRequiredService<EvidencePreservation>();
            var hold = await LegalHoldEndpoints.PlaceAsync(db, evidence, AccountRole.Legal, independent, "Fictional held personal association", [member], [], null, null, null, null, null, "staff", now, CancellationToken.None);
            check("current personal link excludes its staff owner", await StaffAccessEndpoints.ExcludedAsync(db, staff, [member], CancellationToken.None));
            var deletion = scope.ServiceProvider.GetRequiredService<AccountDeletion>();
            var first = await deletion.PurgeAsync(now, CancellationToken.None, member);
            var account = await db.Accounts.SingleAsync(a => a.Id == member);await db.Entry(account).ReloadAsync();
            check("fake unconfirmed identity deletion preserves finalizing state", first.Waiting == 1 && account.Status == AccountStatus.PendingDeletion && account.DeletionFinalizingAt is not null);
            check("actual finalization prevents cancellation after provider uncertainty", !AccountDeletionPolicy.Cancel(account));
            identity.DeleteConfirmed = true;
            var retry = await deletion.PurgeAsync(now, CancellationToken.None, member);
            check("fake confirmed retry finishes deletion once", retry.Deleted == 1 && identity.Deletes == 2 && await db.Accounts.AnyAsync(a => a.Id == member && a.Status == AccountStatus.Deleted));
            check("ordinary member profile and current link are erased", !await db.MemberProfiles.AnyAsync(p => p.AccountId == member) && !await db.StaffMemberLinks.AnyAsync(l => l.MemberAccountId == member));
            check("independent held copies survive member deletion", await db.EvidenceItems.AnyAsync(e => e.HoldId == hold.Id));
            check("deletion preserves staff personal exclusion for retained evidence", await StaffAccessEndpoints.ExcludedAsync(db, staff, [member], CancellationToken.None));
            check("independent officer remains eligible", !await StaffAccessEndpoints.ExcludedAsync(db, independent, [member], CancellationToken.None));
            hold = await db.LegalHolds.SingleAsync(h => h.Id == hold.Id); // Account purge deliberately clears its tracker.
            var history = scope.ServiceProvider.GetRequiredService<StaffConflictHistory>();
            var orphan = Guid.NewGuid();var recent = Guid.NewGuid();var policeSubject = Guid.NewGuid();
            // Models fully expired old accounts: only their explicit historical association remains.
            Audit.Record(db, now.AddYears(-3), independent, "account", "staff-link.set", "account", staff.ToString(), orphan.ToString());
            db.StaffExclusions.Add(new StaffExclusion { StaffAccountId = staff, SubjectAccountId = recent, Reason = "Fictional declared conflict", CreatedBy = independent, CreatedAt = now.AddYears(-3) });
            Audit.Record(db, now.AddDays(-1), independent, "account", "access.excluded", "account", staff.ToString(), recent.ToString());
            var police = new LeCase { Id = Guid.NewGuid(), Reference = "LE-FICTIONAL-CONFLICT", Status = LeCaseStatus.Open, Channel = "email", Agency = "Fictional agency", OfficerName = "Fictional officer", OwnerId = independent, CreatedBy = independent, ReceivedAt = now, CreatedAt = now };
            db.LeCases.Add(police);db.LeCasePeople.Add(new LeCasePerson { CaseId = police.Id, AccountId = policeSubject, Kind = "subject", AddedBy = independent, AddedAt = now });
            db.StaffExclusions.Add(new StaffExclusion { StaffAccountId = staff, SubjectAccountId = policeSubject, Reason = "Fictional retained police conflict", CreatedBy = independent, CreatedAt = now.AddYears(-3) });
            await db.SaveChangesAsync();
            check("audited old personal link excludes before materialisation", await StaffAccessEndpoints.ExcludedAsync(db, staff, [orphan], CancellationToken.None));
            await history.ReconcileAsync(now, CancellationToken.None);
            check("historical explicit association is materialised", await db.StaffExclusions.AnyAsync(e => e.StaffAccountId == staff && e.SubjectAccountId == orphan && e.CreatedBy == independent && e.CreatedAt == now.AddYears(-3)));
            check("reconciliation preserves declared reason", (await db.StaffExclusions.SingleAsync(e => e.StaffAccountId == staff && e.SubjectAccountId == recent)).Reason == "Fictional declared conflict");
            hold.RelatedAccountIds = null;await db.SaveChangesAsync();
            await history.PurgeAsync(now, CancellationToken.None);
            check("unknown held membership prevents a false absence inference", await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == orphan));
            hold.RelatedAccountIds = [member];await db.SaveChangesAsync();
            var purged = await history.PurgeAsync(now, CancellationToken.None);
            check("expired unnecessary association is disposed", purged.Expired >= 1 && !await StaffAccessEndpoints.ExcludedAsync(db, staff, [orphan], CancellationToken.None));
            check("recent declaration extends the association minimum", await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == recent));
            check("police record outliving an account keeps its exclusion", await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == policeSubject));
            check("held deleted account remains excluded", await StaffAccessEndpoints.ExcludedAsync(db, staff, [member], CancellationToken.None));
            await history.ReconcileAsync(now, CancellationToken.None);
            check("reconciliation cannot resurrect expired source assertions", !await StaffAccessEndpoints.ExcludedAsync(db, staff, [orphan], CancellationToken.None));
            await history.PurgeAsync(now.AddMonths(25), CancellationToken.None);
            check("minimum expiry resumes when no related records remain", !await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == recent));
            check("open police data remains protected beyond the minimum", await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == policeSubject));
            // Failure is opt-in only in this Development fixture; no shared application data is touched.
            var unavailable = Guid.NewGuid();
            db.StaffExclusions.Add(new StaffExclusion { StaffAccountId = staff, SubjectAccountId = unavailable, Reason = "Fictional expiry candidate", CreatedBy = independent, CreatedAt = now.AddYears(-3) });await db.SaveChangesAsync();
            config["Evidence:HoldCheckUnavailable"] = "true";
            check("unavailable preservation status stops association expiry", (await history.PurgeAsync(now, CancellationToken.None)).HoldStatusUnavailable && await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == unavailable));
            config["Evidence:HoldCheckUnavailable"] = "false";
            await history.PurgeAsync(now, CancellationToken.None);
            check("reliable status resumes eligible expiry", !await db.StaffExclusions.AnyAsync(e => e.SubjectAccountId == unavailable));
            // Pause an actual exclusion read after its SQL snapshot, then atomically archive/delete the link on another connection.
            var raceStaff = Guid.NewGuid();var raceMember = Guid.NewGuid();
            db.Accounts.AddRange(new Account { Id = raceStaff, Issuer = "https://issuer.example.test/realms/staff", Subject = "fictional-race-" + raceStaff, Realm = AccountRealm.Staff, CreatedAt = now, LastSeenAt = now },
                new Account { Id = raceMember, Issuer = "https://issuer.example.test/realms/members", Subject = "fictional-race-" + raceMember, Realm = AccountRealm.Members, CreatedAt = now, LastSeenAt = now });
            var raceLink = new StaffMemberLink { StaffAccountId = raceStaff, MemberAccountId = raceMember, LinkedBy = independent, LinkedAt = now };
            db.StaffMemberLinks.Add(raceLink);await db.SaveChangesAsync();
            var gate = new ReadGate();
            var probeOptions = new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(config.GetConnectionString("Swoovr")).AddInterceptors(gate).Options;
            await using var probe = new SwoovrDbContext(probeOptions);
            var reading = StaffAccessEndpoints.ExcludedAsync(probe, raceStaff, [raceMember], CancellationToken.None);
            try
            {
                await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
                await using var barrier = await Swoovr.Api.Retention.PreservationLease.ExclusiveAsync(db, CancellationToken.None);
                await using var transaction = await db.Database.BeginTransactionAsync();
                await history.PreserveAsync(raceLink, now, CancellationToken.None);
                db.StaffMemberLinks.Remove(raceLink);await db.SaveChangesAsync();await transaction.CommitAsync();
            }
            finally { gate.Release.TrySetResult(); }
            check("one SQL snapshot cannot miss old link and new exclusion across atomic transition", await reading);
            check("later reads see archived exclusion", await StaffAccessEndpoints.ExcludedAsync(db, raceStaff, [raceMember], CancellationToken.None));


        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
    private sealed class ReadGate : DbCommandInterceptor
    {
        private int reads;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data, DbDataReader result, CancellationToken token = default)
        {
            if (Interlocked.Increment(ref reads) == 1) { Entered.TrySetResult();await Release.Task.WaitAsync(TimeSpan.FromSeconds(10), token); }
            return result;
        }
    }
    private sealed class FakeIdentity : IIdentityAdmin
    {
        public bool Configured => true;
        public bool DeleteConfirmed { get; set; }
        public int Deletes { get; private set; }
        public Task<bool> DeleteUserAsync(string subject, CancellationToken token) { Deletes++;return Task.FromResult(DeleteConfirmed); }
        public Task<bool> SignOutEverywhereAsync(string subject, CancellationToken token) => throw new Exception("Unexpected fake signout.");
        public Task<bool> SignOutOtherSessionsAsync(string subject, string? keep, CancellationToken token) => throw new Exception("Unexpected fake signout.");
    }
    private sealed class NoVerification : IIdVerificationProvider
    {
        public bool Configured => false;
        public string WorkflowId => "fictional";
        public Task<ProviderSession?> CreateSessionAsync(Guid account, string callback, CancellationToken token) => throw new Exception("Unexpected verification provider.");
        public Task<ProviderSession?> GetDecisionAsync(string session, CancellationToken token) => throw new Exception("Unexpected verification provider.");
        public Task<bool> DeleteSessionAsync(string session, CancellationToken token) => throw new Exception("Unexpected verification provider.");
    }
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Swoovr.Checks";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
