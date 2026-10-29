using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Infrastructure.Email;
using Swoovr.Domain;

static class BannedIdentifierChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label);count++; }
        var key = Enumerable.Repeat((byte)31, 32).ToArray();
        var now = DateTimeOffset.Parse("2026-10-29T00:00:00Z");
        var email = BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, " Fictional.Member+tag@Example.Test ", key);
        Check(email.SequenceEqual(BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, "fictional.member+tag@example.test", key)), "Conservative email case/space normalization matches.");
        Check(!email.SequenceEqual(BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, "fictionalmember+tag@example.test", key)), "Email dots are not removed.");
        Check(!email.SequenceEqual(BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, "fictional.member@example.test", key)), "Email plus tags are not removed.");
        Check(BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Mobile, "0491 570 159", key).SequenceEqual(BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Mobile, "+61491570159", key)), "Mobile normalization uses existing E.164 rules.");
        Check(!BannedIdentifierPolicy.HashCanonical(BannedIdentifierType.Email, "same-fictional-input", key).SequenceEqual(BannedIdentifierPolicy.HashCanonical(BannedIdentifierType.Mobile, "same-fictional-input", key)), "Identifier types are cryptographically separated, even with the same input.");
        Check(!email.SequenceEqual(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("fictional.member+tag@example.test"))), "A fingerprint is not unkeyed SHA256.");
        Check(!email.SequenceEqual(BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, "fictional.member+tag@example.test", Enumerable.Repeat((byte)32, 32).ToArray())), "Different keys produce different fingerprints.");
        foreach (var invalid in new[] { "", "not-an-email", "a@b", "a b@example.test" }) {
            try { BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, invalid, key);throw new Exception("Invalid email accepted."); }
            catch (ArgumentException) { count++; }
        }
        try { BannedIdentifierPolicy.HashCanonical((BannedIdentifierType)99, "fictional", key);throw new Exception("Unknown type accepted."); }
        catch (ArgumentOutOfRangeException) { count++; }
        try { BannedIdentifierPolicy.Fingerprint(BannedIdentifierType.Email, "fictional@example.test", new byte[8]);throw new Exception("Weak key accepted."); }
        catch (ArgumentException) { count++; }
        var issued = BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,email,1,Guid.NewGuid(),Guid.NewGuid(),now,"Fictional permanent account restriction.");
        Check(issued.Entry.Status == BanStatus.Active && issued.Entry.BanExpiresAt is null && issued.Entry.IdentifierHash.SequenceEqual(email), "Issuing a permanent restriction preserves only its typed fingerprint.");
        Check(issued.Transition.OldStatus is null && issued.Transition.NewStatus == BanStatus.Active && issued.Note.TransitionId == issued.Transition.Id && issued.Note.CaseId == issued.Entry.CaseId, "Initial transition and associated note describe the same restriction/case.");
        var row = new BannedIdentifier { Status = BanStatus.Active, BannedAt = now.AddDays(-1) };
        Check(BannedIdentifierPolicy.Blocks(row, now), "Active indefinite restriction blocks.");
        row.BanExpiresAt = now.AddTicks(1);Check(BannedIdentifierPolicy.Blocks(row, now), "Future expiry blocks.");
        row.BanExpiresAt = now;Check(!BannedIdentifierPolicy.Blocks(row, now), "Exact expiry does not block without a worker.");
        row.BanExpiresAt = now.AddTicks(-1);Check(!BannedIdentifierPolicy.Blocks(row, now), "Past expiry does not block.");
        foreach (var state in new[] { BanStatus.Revoked, BanStatus.Expired }) {
            row.Status = state;row.BanExpiresAt = null;Check(!BannedIdentifierPolicy.Blocks(row, now), "Inactive restriction never blocks.");
        }
        row.Status = BanStatus.Active;row.BanExpiresAt = null;
        Check(BannedIdentifierPolicy.HistoryUntil(row) is null, "Active indefinite restrictions have no automatic history disposal.");
        row.Status = BanStatus.Revoked;row.RevokedAt = now;row.LastReviewedAt = now.AddMonths(1);
        Check(BannedIdentifierPolicy.HistoryUntil(row) == now.AddMonths(25), "Latest review extends the inactive history minimum by24 calendar months.");
        row.Status = BanStatus.Expired;row.RevokedAt = null;row.BanExpiresAt = now.AddMonths(2);
        Check(BannedIdentifierPolicy.HistoryUntil(row) == now.AddMonths(26), "Scheduled expiry controls inactive history clock.");
        row.Status = BanStatus.Revoked;row.RevokedAt = null;
        Check(BannedIdentifierPolicy.HistoryUntil(row) is null, "Missing revocation provenance cannot permit history disposal.");
        row.Id = Guid.NewGuid();row.CaseId = Guid.NewGuid();row.Status = BanStatus.Active;row.LastReviewedAt = null;
        var overdue=BannedIdentifierPolicy.Issue(BannedIdentifierType.Email,email,1,Guid.NewGuid(),null,now.AddYears(-3),"Fictional original restriction.").Entry;
        overdue.BanExpiresAt=now.AddYears(-2);
        var expiry=BannedIdentifierPolicy.Expire(overdue,now);
        Check(overdue.Status==BanStatus.Expired&&overdue.BanExpiresAt==now.AddYears(-2)&&overdue.LastReviewedAt is null,"Worker expiry keeps original expiry and does not invent necessity review.");
        Check(expiry.Transition.ActorAccountId is null&&expiry.Transition.At==now&&expiry.Transition.Action=="expired"&&expiry.Note.AuthorAccountId is null,"Expiry records actual system processing time without staff attribution.");
        Check(expiry.Transition.OldStatus==BanStatus.Active&&expiry.Transition.NewStatus==BanStatus.Expired&&expiry.Note.TransitionId==expiry.Transition.Id,"Expiry event and case note share provenance.");
        Check(BannedIdentifierPolicy.HistoryUntil(overdue)==now,"Delayed expiry processing does not restart the24-month inactive clock.");
        foreach(var candidate in new[]{new BannedIdentifier{Status=BanStatus.Active},new BannedIdentifier{Status=BanStatus.Active,BanExpiresAt=now.AddTicks(1)},overdue})
        {
            try{BannedIdentifierPolicy.Expire(candidate,now);throw new Exception("Non-due expiry accepted.");}catch(InvalidOperationException){count++;}
        }
        var actor = Guid.NewGuid();
        var (transition, note) = BannedIdentifierPolicy.Revoke(row, actor, "Fictional reassigned identifier reviewed independently.", now);
        Check(row.Status == BanStatus.Revoked && row.RevokedAt == now && row.RevokedBy == actor && row.BanExpiresAt == now && row.LastReviewedAt == now, "Identifier release sets all required timestamps and staff attribution.");
        Check(transition.OldStatus == BanStatus.Active && transition.NewStatus == BanStatus.Revoked && transition.ActorAccountId == actor && transition.BannedIdentifierId == row.Id, "Transition records old/new state and staff provenance.");
        Check(note.CaseId == row.CaseId && note.TransitionId == transition.Id && note.AuthorAccountId == actor && note.Text == transition.Reason, "Case note is bound to the same transition, case, actor and mandatory reason.");
        using (var oldState = System.Text.Json.JsonDocument.Parse(transition.OldValues))
        using (var newState = System.Text.Json.JsonDocument.Parse(transition.NewValues))
            Check(oldState.RootElement.GetProperty("Status").GetString() == "Active" && newState.RootElement.GetProperty("Status").GetString() == "Revoked", "Immutable snapshots preserve named enum meaning across future enum reordering.");
        Check(!BannedIdentifierPolicy.Blocks(row, now), "Released identifier is immediately allowed independently of account sanctions.");
        try { BannedIdentifierPolicy.Revoke(row, actor, "Fictional repeated revocation attempt.", now);throw new Exception("Repeated transition accepted."); }
        catch (InvalidOperationException) { count++; }
        row.Status = BanStatus.Active;
        foreach (var reason in new[] { "", " ", new string('x',1001) }) {
            try { BannedIdentifierPolicy.Revoke(row, actor, reason, now);throw new Exception("Invalid explanation accepted."); }
            catch (ArgumentException) { count++; }
        }
        try { BannedIdentifierPolicy.Revoke(row, Guid.Empty, "Fictional reviewed release explanation.", now);throw new Exception("Missing actor accepted."); }
        catch (ArgumentException) { count++; }
        using var services = new ServiceCollection().BuildServiceProvider();
        BannedIdentifierKeys Keys(bool development, params (string Key, string Value)[] values) {
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(values.Select(p => KeyValuePair.Create<string,string?>(p.Key,p.Value))).Build();
            var founders = new Founders(services, configuration, new UnconfiguredEmailSender(), NullLogger<Founders>.Instance);
            return new BannedIdentifierKeys(configuration, new Environment(development), founders);
        }
        var one = Convert.ToBase64String(key);var two = Convert.ToBase64String(Enumerable.Repeat((byte)32,32).ToArray());
        var ring = Keys(false, ("Bans:HmacKeys:1",one),("Bans:HmacKeys:2",two));
        Check(!ring.QuotaHash("caller","fictional@example.test",1).SequenceEqual(ring.QuotaHash("address","fictional@example.test",1)), "Quota caller and address purposes are separated.");
        Check(!ring.QuotaHash("caller","fictional@example.test",1).SequenceEqual(ring.Fingerprint(BannedIdentifierType.Email,"fictional@example.test",1)), "Quota fingerprints cannot be reused as identifier fingerprints.");
        try { ring.QuotaHash("email","fictional@example.test",1);throw new Exception("Unknown quota purpose accepted."); }
        catch (ArgumentException) { count++; }
        try { ring.QuotaHash("caller","fictional",99);throw new Exception("Missing quota key accepted."); }
        catch (InvalidOperationException) { count++; }
        Check(ring.Configured && ring.CurrentVersion == 2 && ring.HasVersion(1), "Hosted explicit ring retains historical1 and uses2 for new fingerprints.");
        Check(ring.Fingerprint(BannedIdentifierType.Email,"fictional.member+tag@example.test",1).SequenceEqual(email), "Actual server key consumer preserves old fingerprint matching.");
        Check(!Keys(false,("Founders:Secrets:1",Convert.ToHexString(key))).Configured, "Hosted environments never derive an implicit ban key.");
        var local = Keys(true,("Founders:Secrets:1",Convert.ToHexString(key)));
        var localNext = Keys(true,("Founders:Secrets:1",Convert.ToHexString(key)),("Founders:Secrets:2",Convert.ToHexString(new byte[32])));
        Check(local.Fingerprint(BannedIdentifierType.Email,"fictional@example.test",1).SequenceEqual(localNext.Fingerprint(BannedIdentifierType.Email,"fictional@example.test",1)), "Local fallback1 is stable when founder coupons rotate.");
        Check(!Keys(true,("Founders:Secrets:2",Convert.ToHexString(key))).Configured, "Missing founder1 cannot guess a fallback.");
        Check(!Keys(true,("Founders:Secrets:1",Convert.ToHexString(key)),("Bans:HmacKeys:2",two)).HasVersion(1), "Explicit ring is authoritative; no hidden Development old key is invented.");
        Check(Keys(false,("Bans:HmacKeys:1",one),("Bans:HmacKeys:2",two),("Bans:CurrentKeyVersion","1")).CurrentVersion == 1, "Explicit current version can stage a future key without issuing with it.");
        foreach (var setting in new[] { ("Bans:HmacKeys:0",one),("Bans:HmacKeys:1","not-base64"),("Bans:HmacKeys:1",Convert.ToBase64String(new byte[8])),("Bans:CurrentKeyVersion","99") }) {
            try { Keys(false, setting);throw new Exception("Invalid server key configuration accepted."); }
            catch (InvalidOperationException) { count++; }
        }
        try { ring.Fingerprint(BannedIdentifierType.Email,"fictional@example.test",99);throw new Exception("Missing historical key accepted."); }
        catch (InvalidOperationException) { count++; }
        Console.WriteLine($"PASS: banned identifier normalization, HMAC separation and status/calendar boundaries ({count} checks; no registration integration).");
    }
    private sealed class Environment(bool development) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = development ? Environments.Development : Environments.Production;
        public string ApplicationName { get; set; } = "Swoovr.Checks";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

}
