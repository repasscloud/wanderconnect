using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Media;
using Swoovr.Api.Notifications;
using Swoovr.Api.Verification;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;

/// <summary>Actual API key consumers, fictional keys only. Adding founder2 must not silently replace encryption/lookup1.</summary>
static class FallbackKeyChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string label) { if (!condition) throw new Exception("Fallback key check failed: " + label); checks++; }
        using var services = new ServiceCollection().BuildServiceProvider();
        var first = Enumerable.Repeat((byte)17, 32).ToArray();
        var second = Enumerable.Repeat((byte)29, 32).ToArray();
        IConfiguration Config(bool one, bool two, params (string Key, string Value)[] extra) => new ConfigurationBuilder().AddInMemoryCollection(
            (one ? new[] { KeyValuePair.Create<string, string?>("Founders:Secrets:1", Convert.ToHexString(first)) } : [])
            .Concat(two ? [KeyValuePair.Create<string, string?>("Founders:Secrets:2", Convert.ToHexString(second))] : [])
            .Concat(extra.Select(e => KeyValuePair.Create<string, string?>(e.Key, e.Value)))).Build();
        Founders Founder(IConfiguration config) => new(services, config, new UnconfiguredEmailSender(), NullLogger<Founders>.Instance);
        var oldConfig = Config(true, false); var nextConfig = Config(true, true);
        var oldFounder = Founder(oldConfig); var nextFounder = Founder(nextConfig);
        Expect(nextFounder.CurrentVersion == 2, "founder coupons still rotate to the newest actual founder key");
        var oldEvidence = new EvidenceKeys(oldConfig, oldFounder); var nextEvidence = new EvidenceKeys(nextConfig, nextFounder);
        var sealedEvidence = EvidenceSeal.Seal(1, oldEvidence.Encryption[1], "Fictional sealed review evidence");
        Expect(EvidenceSeal.Open(nextEvidence.Encryption, sealedEvidence) == "Fictional sealed review evidence", "old evidence stays readable after adding founder2");
        var oldContact = new ContactHistoryKeys(oldConfig, oldFounder); var nextContact = new ContactHistoryKeys(nextConfig, nextFounder);
        var sealedContact = ContactHistoryPolicy.Encrypt(1, oldContact.Encryption[1], "fictional.previous@example.test");
        Expect(ContactHistoryPolicy.Decrypt(nextContact.Encryption, sealedContact) == "fictional.previous@example.test", "old encrypted contact stays readable");
        Expect(ContactHistoryPolicy.Lookup(oldContact.Lookup!, ContactType.Email, "fictional.previous@example.test")
            .SequenceEqual(ContactHistoryPolicy.Lookup(nextContact.Lookup!, ContactType.Email, "fictional.previous@example.test")), "contact lookup remains stable");
        var fields = new VerifiedIdentityFields("Fictional", "Tester", new DateOnly(1990, 1, 1), "passport", "AU", new DateOnly(2030, 1, 1));
        var oldIdentity = new IdentityKeys(oldConfig, oldFounder); var nextIdentity = new IdentityKeys(nextConfig, nextFounder);
        var record = VerifiedIdentityPolicy.Record(Guid.NewGuid(), new IdVerificationAttempt { Id = Guid.NewGuid(), SessionId = "fictional" }, fields,
            DateTimeOffset.UtcNow, 1, oldIdentity.Encryption[1], oldIdentity.Hmac!);
        Expect(VerifiedIdentityPolicy.Open(nextIdentity.Encryption, record.Sealed) == fields, "old minimum identity record stays readable");
        Expect(record.MatchHmac!.SequenceEqual(VerifiedIdentityPolicy.Match(nextIdentity.Hmac!, fields)!), "identity matching remains stable");
        var account = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; const string number = "+61491570156";
        var challenge = new PhoneCode { AccountId = account, Number = number, CodeHash = PhoneVerificationPolicy.Hash(new PhoneKeys(oldConfig, oldFounder).Key!, account, number, "123456"), ExpiresAt = now.AddMinutes(5) };
        Expect(PhoneVerificationPolicy.Check(challenge, new PhoneKeys(nextConfig, nextFounder).Key!, account, number, "123456", now) == PhoneCheckResult.Verified,
            "a pending fictional phone code survives founder rotation");
        var media = Guid.NewGuid();
        Expect(new MediaLinks(oldConfig, oldFounder).For(media, "member", now) == new MediaLinks(nextConfig, nextFounder).For(media, "member", now), "existing media signatures remain stable");
        var oldPush = new PushKeys(oldConfig, new DevelopmentEnvironment(), oldFounder).Protector!;
        var nextPush = new PushKeys(nextConfig, new DevelopmentEnvironment(), nextFounder).Protector!;
        var device = Guid.NewGuid(); const string deviceToken = "fictional-device-token-never-delivered";
        var protectedToken = oldPush.Protect(device, account, "fcm", deviceToken);
        Expect(nextPush.Unprotect(device, account, "fcm", 1, protectedToken) == deviceToken, "old protected device token survives founder rotation");
        Expect(nextPush.Hash("fcm", deviceToken) == oldPush.Hash("fcm", deviceToken), "device lookup remains stable");
        // Explicit newer encryption does not remove a still-required legacy key.
        var explicitNext = Config(true, true, ("Evidence:Keys:2", Convert.ToBase64String(second)), ("ContactHistory:Keys:2", Convert.ToBase64String(second)),
            ("IdentityStore:Keys:2", Convert.ToBase64String(second)), ("Push:TokenKeys:2", Convert.ToBase64String(second)));
        var founderWithExplicit = Founder(explicitNext);
        Expect(EvidenceSeal.Open(new EvidenceKeys(explicitNext, founderWithExplicit).Encryption, sealedEvidence) == "Fictional sealed review evidence", "explicit evidence2 keeps derived1");
        Expect(ContactHistoryPolicy.Decrypt(new ContactHistoryKeys(explicitNext, founderWithExplicit).Encryption, sealedContact) == "fictional.previous@example.test", "explicit contact2 keeps derived1");
        Expect(VerifiedIdentityPolicy.Open(new IdentityKeys(explicitNext, founderWithExplicit).Encryption, record.Sealed) == fields, "explicit identity2 keeps derived1");
        Expect(new PushKeys(explicitNext, new DevelopmentEnvironment(), founderWithExplicit).Protector!.Unprotect(device, account, "fcm", 1, protectedToken) == deviceToken, "explicit push2 keeps Development derived1");
        var missing = Config(false, true); var missingFounder = Founder(missing);
        Expect(new EvidenceKeys(missing, missingFounder).CurrentVersion is null && new ContactHistoryKeys(missing, missingFounder).CurrentVersion is null
            && !new IdentityKeys(missing, missingFounder).Configured, "missing founder1 never guesses an encryption1 key from founder2");
        Expect(new PhoneKeys(missing, missingFounder).Key is null && new MediaLinks(missing, missingFounder).Key is null
            && new PushKeys(missing, new DevelopmentEnvironment(), missingFounder).Protector is null, "missing founder1 disables unversioned fallbacks");
        var explicitOne = Config(true, true, ("Evidence:Keys:1", Convert.ToBase64String(second)));
        Expect(new EvidenceKeys(explicitOne, Founder(explicitOne)).Encryption[1].SequenceEqual(second), "explicit key1 is never overwritten");
        Console.WriteLine($"PASS: actual API fallback-key compatibility ({checks} checks; fictional keys, no provider calls).");
    }
    private sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Swoovr.Checks";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
