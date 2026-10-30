using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Swoovr.Api;
using Swoovr.Api.Accounts;
using Swoovr.Api.Safety;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

/// <summary>Owned PostgreSQL fixtures and fake mail acceptance only; never real external ownership/delivery proof.</summary>
static class MeetingSafetyContactVerificationPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var source = new SwoovrDbContext(options);
        // Npgsql may redact password after open: capture before opening any source connection.
        var sourceConnection = source.Database.GetDbConnection().ConnectionString;
        await using var admin = new NpgsqlConnection(sourceConnection);
        await admin.OpenAsync();
        var name = "swoovr_meeting_access_" + Guid.NewGuid().ToString("N");
        await using (var create = new NpgsqlCommand("CREATE DATABASE " + name, admin)) await create.ExecuteNonQueryAsync();
        try
        {
            var connection = new NpgsqlConnectionStringBuilder(sourceConnection) { Database = name, Pooling = false }.ConnectionString;
            var ownedOptions = new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(connection).Options;
            await using var db = new SwoovrDbContext(ownedOptions);
            await db.Database.MigrateAsync();
            var lookup = RandomNumberGenerator.GetBytes(32);
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Swoovr"] = connection,
                ["ContactHistory:Keys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
                ["ContactHistory:LookupKey"] = Convert.ToBase64String(lookup)
            }).Build();
            var clock = new Clock();
            var sink = new FakeEmailSink(ownedOptions, lookup);
            using var services = new ServiceCollection().AddSingleton<IConfiguration>(config)
                .AddSingleton<IHostEnvironment>(new Development()).AddSingleton<TimeProvider>(clock)
                .AddSingleton(db).AddSingleton<IEmailSender>(sink).AddLogging().AddScoped<Founders>()
                .AddSwoovrProviders(config).BuildServiceProvider();
            using var scope = services.CreateScope();
            var access = scope.ServiceProvider.GetRequiredService<MeetingSafetyContactVerification>();
            var store = scope.ServiceProvider.GetRequiredService<MeetingSafetyStore>();
            var firstMember = Guid.NewGuid(); var secondMember = Guid.NewGuid();
            foreach (var id in new[] { firstMember, secondMember })
                db.Accounts.Add(new() { Id = id, Issuer = "http://localhost/fictional-members", Subject = id.ToString(),
                    Realm = AccountRealm.Members, Status = AccountStatus.Active, CreatedAt = clock.Now, LastSeenAt = clock.Now });
            await db.SaveChangesAsync();

            async Task<Fixture> New(Guid member, string destination)
            {
                var now = clock.Now;
                var contact = new TrustedContact { Id = Guid.NewGuid(), MemberId = member,
                    DisplayName = "Fictional adult trusted contact", PreferredFirstName = "Fictional", Channel = "email",
                    DestinationEncrypted = store.Encrypt(destination),
                    DestinationLookupHash = HMACSHA256.HashData(lookup, Encoding.UTF8.GetBytes("swoovr:meeting-contact|email|" + destination)),
                    Consent = TrustedContactConsent.Confirmed, ConfirmedAt = now, CreatedAt = now };
                var session = new MeetingSafetySession { Id = Guid.NewGuid(), MemberId = member, PreferredFirstName = "Fictional",
                    StartsAt = now, FinalCheckInAt = now.AddDays(2), TimeZoneId = "Australia/Adelaide",
                    Status = MeetingSafetyStatus.Active, CreatedAt = now, UpdatedAt = now };
                var recipient = new MeetingSafetyRecipient { Id = Guid.NewGuid(), SessionId = session.Id, TrustedContactId = contact.Id, CreatedAt = now };
                var raw = MeetingSafetyPolicy.Token();
                var link = new MeetingSafetyLink { Id = Guid.NewGuid(), RecipientId = recipient.Id, TokenHash = MeetingSafetyPolicy.TokenHash(raw), CreatedAt = now, ExpiresAt = now.AddDays(3) };
                db.AddRange(contact, session, recipient, link); await db.SaveChangesAsync();
                return new(contact, session, recipient, link, raw);
            }
            async Task<MeetingSafetyContactChallenge> Latest(Fixture fixture) => await db.Set<MeetingSafetyContactChallenge>()
                .AsNoTracking().Where(c => c.RecipientId == fixture.Recipient.Id).OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).FirstAsync();
            async Task<bool> Closed(Fixture fixture, string? grant = null)
            {
                var before = sink.Messages.Count;
                return !(await access.ScopeAsync(fixture.Raw, default)).Available
                    && (await access.RequestCodeAsync(fixture.Raw, default)).Status == "unavailable"
                    && !(await access.VerifyAsync(fixture.Raw, "000000", default)).Verified
                    && !await access.AllowedAsync(fixture.Raw, grant ?? MeetingSafetyPolicy.Token(), default)
                    && sink.Messages.Count == before;
            }

            var first = await New(firstMember, "fictional-code-a@example.test");
            check("external access scope exposes availability without meeting or contact details", (await access.ScopeAsync(first.Raw, default)).Available);
            check("code request is accepted by the fake mail boundary", (await access.RequestCodeAsync(first.Raw, default)).Status == "requested");
            var firstCode = Code(sink.Messages.Last()); var firstChallenge = await Latest(first);
            check("code HMAC is durably committed before external send", sink.AllObservedCommittedHashes && firstChallenge.CodeHash.SequenceEqual(
                MeetingSafetyContactAccessPolicy.CodeHash(lookup, first.Recipient.Id, firstChallenge.Id, firstCode)));
            check("challenge stores destination HMAC and five minute clock only", firstChallenge.DestinationHash.SequenceEqual(
                MeetingSafetyContactAccessPolicy.DestinationHash(lookup, "email", "fictional-code-a@example.test")) && firstChallenge.ExpiresAt == clock.Now.AddMinutes(5));
            check("code message contains no meeting detail or resource bearer link", sink.Messages.Last().Subject.Contains("meeting safety access code")
                && !sink.Messages.Last().Text.Contains("/safety/") && !sink.Messages.Last().Text.Contains("Australia/Adelaide"));
            var grant = await access.VerifyAsync(first.Raw, firstCode, default);
            var persistedGrant = await db.Set<MeetingSafetyContactReadGrant>().AsNoTracking().SingleAsync(g => g.RecipientId == first.Recipient.Id);
            check("verification persists a digest and bounds read grant to four hours", grant.Verified && MeetingSafetyPolicy.TokenShape(grant.Grant)
                && persistedGrant.TokenHash.SequenceEqual(MeetingSafetyPolicy.TokenHash(grant.Grant!)) && grant.ExpiresAt == clock.Now.AddHours(4));
            check("successful code cannot issue a second grant", !(await access.VerifyAsync(first.Raw, firstCode, default)).Verified
                && await db.Set<MeetingSafetyContactReadGrant>().CountAsync(g => g.RecipientId == first.Recipient.Id) == 1);
            var secondRaw = MeetingSafetyPolicy.Token();
            db.MeetingSafetyLinks.Add(new() { Id = Guid.NewGuid(), RecipientId = first.Recipient.Id, TokenHash = MeetingSafetyPolicy.TokenHash(secondRaw), CreatedAt = clock.Now, ExpiresAt = clock.Now.AddDays(3) });
            await db.SaveChangesAsync();
            var different = await New(firstMember, "fictional-code-b@example.test");
            check("resource grant permits another current link for the same recipient only", await access.AllowedAsync(secondRaw, grant.Grant, default)
                && !await access.AllowedAsync(different.Raw, grant.Grant, default));
            var expiryClock = clock.Now;
            clock.Now = grant.ExpiresAt!.Value;
            check("read grant fails at its expiry boundary", !await access.AllowedAsync(first.Raw, grant.Grant, default));
            clock.Now = expiryClock;
            first.Contact.Consent = TrustedContactConsent.Withdrawn; first.Contact.WithdrawnAt = clock.Now; await db.SaveChangesAsync();
            check("current consent withdrawal invalidates a previously verified grant", await Closed(first, grant.Grant));

            var shortLink = await New(firstMember, "fictional-short-link@example.test");
            shortLink.Link.ExpiresAt = clock.Now.AddMinutes(2); await db.SaveChangesAsync();
            await access.RequestCodeAsync(shortLink.Raw, default);
            var shortGrant = await access.VerifyAsync(shortLink.Raw, Code(sink.Messages.Last()), default);
            check("read grant cannot outlive its current short link", shortGrant.Verified && shortGrant.ExpiresAt == shortLink.Link.ExpiresAt);

            var guesses = await New(firstMember, "fictional-guesses@example.test");
            await access.RequestCodeAsync(guesses.Raw, default); var good = Code(sink.Messages.Last());
            var wrong = good == "000000" ? "000001" : "000000";
            for (var i = 0; i < 5; i++) check("persisted wrong attempt is refused " + (i + 1), !(await access.VerifyAsync(guesses.Raw, wrong, default)).Verified);
            check("five persisted failures lock even the correct code", (await Latest(guesses)).FailedAttempts == 5 && !(await access.VerifyAsync(guesses.Raw, good, default)).Verified);
            var expiry = await New(firstMember, "fictional-expiry@example.test");
            await access.RequestCodeAsync(expiry.Raw, default); var expiredCode = Code(sink.Messages.Last());
            clock.Now = clock.Now.AddMinutes(5);
            check("persisted challenge fails at five minute boundary", !(await access.VerifyAsync(expiry.Raw, expiredCode, default)).Verified);
            var replace = await New(firstMember, "fictional-replace@example.test");
            await access.RequestCodeAsync(replace.Raw, default); var oldId = (await Latest(replace)).Id;
            clock.Now = clock.Now.AddSeconds(60);
            await access.RequestCodeAsync(replace.Raw, default); var replacementCode = Code(sink.Messages.Last());
            check("resend supersedes persisted prior unused challenge", (await db.Set<MeetingSafetyContactChallenge>().AsNoTracking().SingleAsync(c => c.Id == oldId)).SupersededAt != null
                && await db.Set<MeetingSafetyContactChallenge>().CountAsync(c => c.RecipientId == replace.Recipient.Id && c.SupersededAt == null) == 1);
            check("replacement challenge verifies successfully", (await access.VerifyAsync(replace.Raw, replacementCode, default)).Verified);

            var sharedA = await New(firstMember, "fictional-hour-quota@example.test");
            var sharedB = await New(secondMember, "fictional-hour-quota@example.test");
            var quotaStart = clock.Now;
            await access.RequestCodeAsync(sharedA.Raw, default); var sent = sink.Messages.Count;
            clock.Now = quotaStart.AddSeconds(59);
            check("recipient cooldown blocks before sixty seconds without sending", (await access.RequestCodeAsync(sharedA.Raw, default)).Status == "try-later" && sink.Messages.Count == sent);
            clock.Now = quotaStart.AddSeconds(60);
            check("another owner shares destination quota", (await access.RequestCodeAsync(sharedB.Raw, default)).Status == "requested");
            clock.Now = quotaStart.AddMinutes(2); await access.RequestCodeAsync(sharedA.Raw, default); sent = sink.Messages.Count;
            clock.Now = quotaStart.AddMinutes(3);
            check("three per hour destination quota spans separate owners", (await access.RequestCodeAsync(sharedB.Raw, default)).Status == "try-later" && sink.Messages.Count == sent);
            var dailyA = await New(firstMember, "fictional-day-quota@example.test");
            var dailyB = await New(secondMember, "fictional-day-quota@example.test");
            var dailyStart = clock.Now; var allTen = true;
            for (var i = 0; i < 10; i++)
            { clock.Now = dailyStart.AddHours(i * 2); allTen &= (await access.RequestCodeAsync(i % 2 == 0 ? dailyA.Raw : dailyB.Raw, default)).Status == "requested"; }
            sent = sink.Messages.Count; clock.Now = dailyStart.AddHours(20);
            check("ten per day destination quota spans separate owners", allTen && (await access.RequestCodeAsync(dailyA.Raw, default)).Status == "try-later" && sink.Messages.Count == sent);

            var unknown = await New(firstMember, "fictional-unknown@example.test");
            sink.Outcome = "unknown"; sent = sink.Messages.Count;
            var unknownResult = await access.RequestCodeAsync(unknown.Raw, default);
            var immediateRetry = await access.RequestCodeAsync(unknown.Raw, default);
            check("unknown acceptance persists one challenge and causes no blind retry", unknownResult.Status == "acceptance-unconfirmed"
                && immediateRetry.Status == "try-later" && sink.Messages.Count == sent + 1
                && await db.Set<MeetingSafetyContactChallenge>().CountAsync(c => c.RecipientId == unknown.Recipient.Id) == 1 && sink.AllObservedCommittedHashes);
            sink.Outcome = "accepted";

            var unavailable = await New(firstMember, "fictional-invalid-scope@example.test");
            var current = clock.Now;
            clock.Now = unavailable.Link.ExpiresAt;
            check("expired link fails closed across code and grant operations", await Closed(unavailable));
            clock.Now = current; unavailable.Link.RevokedAt = current; await db.SaveChangesAsync();
            check("revoked link fails closed across code and grant operations", await Closed(unavailable));
            unavailable.Link.RevokedAt = null; unavailable.Recipient.RevokedAt = current; await db.SaveChangesAsync();
            check("revoked recipient fails closed across code and grant operations", await Closed(unavailable));
            unavailable.Recipient.RevokedAt = null; unavailable.Session.Status = MeetingSafetyStatus.Revoked; await db.SaveChangesAsync();
            check("revoked safety session fails closed across code and grant operations", await Closed(unavailable));
            unavailable.Session.Status = MeetingSafetyStatus.Active; unavailable.Contact.Consent = TrustedContactConsent.Pending; unavailable.Contact.ConfirmedAt = null; await db.SaveChangesAsync();
            check("unconfirmed contact fails closed across code and grant operations", await Closed(unavailable));
            unavailable.Contact.Consent = TrustedContactConsent.Confirmed; unavailable.Contact.ConfirmedAt = current;
            unavailable.Contact.MemberId = secondMember; await db.SaveChangesAsync();
            check("cross-owner contact and session fail closed", await Closed(unavailable));
            unavailable.Contact.MemberId = firstMember; await db.SaveChangesAsync();
            var ban = new Sanction { Id = Guid.NewGuid(), AccountId = firstMember, Kind = SanctionKind.Termination,
                Reason = "Fictional owned test termination", StartsAt = current, CreatedBy = secondMember, CreatedByRole = AccountRole.Admin };
            db.Sanctions.Add(ban); await db.SaveChangesAsync();
            check("barred member fails closed across code and grant operations", await Closed(unavailable));
            ban.LiftedAt = current; ban.LiftedBy = secondMember; await db.SaveChangesAsync();
            await db.Accounts.Where(a => a.Id == firstMember).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AccountStatus.Deleted));
            check("deleted member fails closed across code and grant operations", await Closed(unavailable));
            await db.Accounts.Where(a => a.Id == firstMember).ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AccountStatus.Active));
            await access.RequestCodeAsync(different.Raw, default);
            var removedGrant = await access.VerifyAsync(different.Raw, Code(sink.Messages.Last()), default);
            check("owned deletion fixture has a persisted verified grant", removedGrant.Verified);
            await db.MeetingSafetySessions.Where(s => s.Id == different.Session.Id).ExecuteDeleteAsync();
            check("session deletion cascades external capability dependencies", !await db.MeetingSafetyRecipients.AnyAsync(r => r.Id == different.Recipient.Id)
                && !await db.MeetingSafetyLinks.AnyAsync(l => l.RecipientId == different.Recipient.Id)
                && !await db.Set<MeetingSafetyContactChallenge>().AnyAsync(c => c.RecipientId == different.Recipient.Id)
                && !await db.Set<MeetingSafetyContactReadGrant>().AnyAsync(g => g.RecipientId == different.Recipient.Id) && await Closed(different, removedGrant.Grant));
            check("unknown well-shaped link exposes no scope", !(await access.ScopeAsync(MeetingSafetyPolicy.Token(), default)).Available);
        }
        finally
        {
            await using var drop = new NpgsqlCommand("DROP DATABASE " + name + " WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }
    private sealed record Fixture(TrustedContact Contact, MeetingSafetySession Session, MeetingSafetyRecipient Recipient, MeetingSafetyLink Link, string Raw);
    private static string Code(EmailMessage message)
    {
        var match = Regex.Match(message.Text, @"access code is ([0-9]{6})\.");
        return match.Success ? match.Groups[1].Value : throw new Exception("Fake code message did not match its bounded format.");
    }
    private sealed class FakeEmailSink(DbContextOptions<SwoovrDbContext> options, byte[] lookup) : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public string Outcome = "accepted";
        public bool AllObservedCommittedHashes = true;
        public async Task<bool> SendAsync(EmailMessage message, CancellationToken token) => (await SendOutcomeAsync(message, token)).Status == "accepted";
        public async Task<EmailOutcome> SendOutcomeAsync(EmailMessage message, CancellationToken token)
        {
            Messages.Add(message);
            // A separate PostgreSQL connection cannot observe uncommitted challenges from the service transaction.
            await using var observer = new SwoovrDbContext(options);
            var destinationHash = MeetingSafetyContactAccessPolicy.DestinationHash(lookup, "email", message.To);
            var row = await observer.Set<MeetingSafetyContactChallenge>().AsNoTracking().Where(c => c.DestinationHash == destinationHash)
                .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).FirstOrDefaultAsync(token);
            AllObservedCommittedHashes &= row != null && row.CodeHash.SequenceEqual(MeetingSafetyContactAccessPolicy.CodeHash(lookup, row.RecipientId, row.Id, Code(message)));
            return new(Outcome);
        }
    }
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = new(2026, 10, 30, 0, 0, 0, TimeSpan.Zero); public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class Development : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Swoovr.Checks";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
