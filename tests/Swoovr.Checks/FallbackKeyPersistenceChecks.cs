using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

/// <summary>Actual legacy email/mobile lookup consumers against the existing throwaway PostgreSQL checks database.</summary>
static class FallbackKeyPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options);
        using var services = new ServiceCollection().AddSingleton(db).BuildServiceProvider();
        var first = Enumerable.Repeat((byte)17, 32).ToArray();
        var second = Enumerable.Repeat((byte)29, 32).ToArray();
        const string email = "fictional.legacy@example.test", phone = "+61491570156", explicitEmail = "fictional.explicit@example.test";
        var now = DateTimeOffset.UtcNow;
        var accounts = Enumerable.Range(0, 4).Select(_ => new Account { Id = Guid.NewGuid(), Issuer = "https://issuer.example.test/realms/members",
            Subject = "fictional-key-" + Guid.NewGuid(), Realm = AccountRealm.Members, Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now }).ToArray();
        db.Accounts.AddRange(accounts);
        // Independent historical seed: the original founder1 label and original typed hashing, not the new helper.
        var oldKey = HMACSHA256.HashData(first, "swoovr:terminated-email-hold:v1"u8);
        db.TerminatedEmailHolds.Add(new TerminatedEmailHold { EmailHash = AccountDeletion.HoldHash(oldKey, email), CreatedAt = now, ExpiresAt = now.AddYears(2) });
        db.TerminatedPhoneHolds.Add(new TerminatedPhoneHold { PhoneHash = AccountDeletion.PhoneHoldHash(oldKey, phone), CreatedAt = now, ExpiresAt = now.AddYears(2) });
        var explicitKey = new string('z', 32);
        db.TerminatedEmailHolds.Add(new TerminatedEmailHold { EmailHash = AccountDeletion.HoldHash(Encoding.UTF8.GetBytes(explicitKey), explicitEmail), CreatedAt = now, ExpiresAt = now.AddYears(2) });
        await db.SaveChangesAsync();
        IConfiguration Config(bool legacy, bool explicitHold = false) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["Founders:Secrets:1"] = legacy ? Convert.ToHexString(first) : null,
            ["Founders:Secrets:2"] = Convert.ToHexString(second), ["Accounts:HoldKey"] = explicitHold ? explicitKey : null,
        }).Build();
        AccountDeletion Deletion(IConfiguration config) => new(services, new NoIdentityCalls(), new NoVerificationCalls(),
            new Founders(services, config, new UnconfiguredEmailSender(), NullLogger<Founders>.Instance), config, NullLogger<AccountDeletion>.Instance);
        var next = Deletion(Config(true));
        await next.FlagIfReturningAsync(accounts[0].Id, email, now, CancellationToken.None);
        check("founder2 preserves the historical terminated-email match", await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == accounts[0].Id && c.SystemFlag == "returning-terminated"));
        await next.FlagIfReturningPhoneAsync(accounts[1].Id, phone, now, CancellationToken.None);
        check("founder2 preserves the historical terminated-mobile match", await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == accounts[1].Id && c.SystemFlag == "returning-terminated-phone"));
        await Deletion(Config(false)).FlagIfReturningAsync(accounts[2].Id, email, now, CancellationToken.None);
        check("missing founder1 never pretends another key can match a legacy hold", !await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == accounts[2].Id));
        await Deletion(Config(true, true)).FlagIfReturningAsync(accounts[3].Id, explicitEmail, now, CancellationToken.None);
        check("explicit terminated-hold key keeps precedence", await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == accounts[3].Id));
    }
    private sealed class NoIdentityCalls : IIdentityAdmin
    {
        public bool Configured => false;
        public Task<bool> SignOutEverywhereAsync(string subject, CancellationToken token) => throw new Exception("Unexpected identity provider call.");
        public Task<bool> SignOutOtherSessionsAsync(string subject, string? keep, CancellationToken token) => throw new Exception("Unexpected identity provider call.");
        public Task<bool> DeleteUserAsync(string subject, CancellationToken token) => throw new Exception("Unexpected identity provider call.");
    }
    private sealed class NoVerificationCalls : IIdVerificationProvider
    {
        public bool Configured => false;
        public string WorkflowId => "fictional";
        public Task<ProviderSession?> CreateSessionAsync(Guid account, string callback, CancellationToken token) => throw new Exception("Unexpected verification provider call.");
        public Task<ProviderSession?> GetDecisionAsync(string session, CancellationToken token) => throw new Exception("Unexpected verification provider call.");
        public Task<bool> DeleteSessionAsync(string session, CancellationToken token) => throw new Exception("Unexpected verification provider call.");
    }
}
