using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

// Real PostgreSQL, sealed synthetic evidence and the actual retention resolver. No source row is needed to resolve copies.
static class MediaCopyProvenanceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        var key = Enumerable.Repeat((byte)71, 32).ToArray();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Evidence:Keys:1"] = Convert.ToBase64String(key) }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IEmailSender>(new UnconfiguredEmailSender()); services.AddScoped<Founders>(); services.AddScoped<ContactHistoryKeys>(); services.AddScoped<EvidenceKeys>();
        services.AddScoped(_ => new SwoovrDbContext(options)); services.AddScoped<EvidencePreservation>(); services.AddScoped<SafetyRetention>();
        await using var provider = services.BuildServiceProvider(); await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SwoovrDbContext>(); var retention = scope.ServiceProvider.GetRequiredService<SafetyRetention>();
        // Roll back the intentionally unresolved negative holds so other shared-database checks stay independent.
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow; var owner = Guid.NewGuid(); var decisionId = Guid.NewGuid(); var appealId = Guid.NewGuid(); var mediaId = Guid.NewGuid();
        db.Accounts.Add(new Account { Id = owner, Issuer = "https://issuer.example.test", Subject = owner.ToString(), Realm = AccountRealm.Members, Status = AccountStatus.Active, CreatedAt = now, LastSeenAt = now });
        var decision = new MediaDecisionRecord { Id = decisionId, MediaId = mediaId, AccountId = owner, Decision = "reject", Code = "REJECT_PRIVACY", At = now };
        var appeal = new MediaAppeal { Id = appealId, DecisionId = decisionId, MediaId = mediaId, AccountId = owner, Text = "Fictional appeal", SubmittedAt = now };
        db.AddRange(decision, appeal); await db.SaveChangesAsync();
        var json = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        LegalHold Hold() => new() { Id = Guid.NewGuid(), Status = LegalHoldStatus.Active, Reason = "Fictional legacy copy provenance", Source = "staff", PlacedByRole = AccountRole.Legal, PlacedAt = now, ReviewDue = now.AddMonths(1), RelatedAccountIds = null, RetentionCaseIds = null };
        EvidenceItem Copy(LegalHold hold, string source, Guid record, object content, Guid? envelopeOwner = null)
        {
            var text = JsonSerializer.Serialize(content, json);
            return new() { Id = Guid.NewGuid(), HoldId = hold.Id, Capture = 1, Kind = "media", Source = source, RecordId = record.ToString(), AccountId = envelopeOwner ?? owner,
                CapturedAt = now, Sha256 = EvidenceSeal.Hash(text), Sealed = EvidenceSeal.Seal(1, key, text) };
        }
        var held = Hold(); db.LegalHolds.Add(held); db.EvidenceItems.AddRange(Copy(held, "media_decision_records", decisionId, decision), Copy(held, "media_appeals", appealId, appeal)); await db.SaveChangesAsync();
        await db.Set<MediaAppeal>().Where(a => a.Id == appealId).ExecuteDeleteAsync(); await db.Set<MediaDecisionRecord>().Where(d => d.Id == decisionId).ExecuteDeleteAsync();
        await db.Accounts.Where(a => a.Id == owner).ExecuteDeleteAsync(); db.ChangeTracker.Clear();
        check("media provenance fixture has no surviving source/account rows", !await db.Set<MediaAppeal>().AnyAsync(a => a.Id == appealId) && !await db.Set<MediaDecisionRecord>().AnyAsync(d => d.Id == decisionId) && !await db.Accounts.AnyAsync(a => a.Id == owner));
        held = await db.LegalHolds.SingleAsync(h => h.Id == held.Id); await retention.ResolveProvenanceAsync(held, default);
        check("sealed media decision and appeal copies reconstruct deleted account provenance", held.RetentionCaseIds is { Length: 0 } && held.RelatedAccountIds is { Length: 1 } && held.RelatedAccountIds[0] == owner);
        var original = await db.EvidenceItems.Where(e => e.HoldId == held.Id).Select(e => new { e.Id, e.Sealed, e.Sha256 }).ToListAsync();
        await retention.ResolveProvenanceAsync(held, default);
        check("provenance resolution preserves sealed copy bytes and hashes", (await db.EvidenceItems.Where(e => e.HoldId == held.Id).ToListAsync()).All(e => original.Any(o => o.Id == e.Id && o.Sealed.SequenceEqual(e.Sealed) && o.Sha256.SequenceEqual(e.Sha256))));
        var legacy = Hold(); db.LegalHolds.Add(legacy);
        var legacyCopy = Copy(legacy, "media_items", mediaId, new { id = mediaId, accountId = owner, status = "Rejected" }); legacyCopy.Kind = "media-item";
        db.EvidenceItems.Add(legacyCopy); await db.SaveChangesAsync(); await retention.ResolveProvenanceAsync(legacy, default);
        check("existing media-item copy kind retains its legacy provenance handling", legacy.RelatedAccountIds is { Length: 1 } && legacy.RelatedAccountIds[0] == owner && legacy.RetentionCaseIds is { Length: 0 });
        foreach (var variant in new[] { "missing-account", "malformed-account", "empty-account", "mismatched-account", "mismatched-record", "unknown-source", "legacy-source-under-wrong-kind", "missing-media", "missing-appeal-decision", "missing-envelope" })
        {
            var invalid = Hold(); db.LegalHolds.Add(invalid);
            var payload = new Dictionary<string, object?> { ["id"] = appealId, ["accountId"] = owner, ["mediaId"] = mediaId, ["decisionId"] = decisionId };
            switch (variant)
            {
                case "missing-account": payload.Remove("accountId"); break;
                case "malformed-account": payload["accountId"] = "not-a-guid"; break;
                case "empty-account": payload["accountId"] = Guid.Empty; break;
                case "mismatched-account": payload["accountId"] = Guid.NewGuid(); break;
                case "mismatched-record": payload["id"] = Guid.NewGuid(); break;
                case "missing-media": payload.Remove("mediaId"); break;
                case "missing-appeal-decision": payload.Remove("decisionId"); break;
            }
            var invalidCopy = Copy(invalid, variant == "unknown-source" ? "unknown_media_schema" : variant == "legacy-source-under-wrong-kind" ? "media_items" : "media_appeals", appealId, payload);
            if (variant == "missing-envelope") invalidCopy.AccountId = null;
            db.EvidenceItems.Add(invalidCopy); await db.SaveChangesAsync();
            await retention.ResolveProvenanceAsync(invalid, default);
            check("media provenance fails closed for " + variant, invalid.RelatedAccountIds is null && invalid.RetentionCaseIds is null);
        }
        // No disposal bypass: all positive and intentionally malformed synthetic fixtures leave through rollback.
        await transaction.RollbackAsync();
    }
}
