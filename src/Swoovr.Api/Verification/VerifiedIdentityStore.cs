using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Verification;

/// <summary>
/// The identity store keys (D191): `IdentityStore:Keys:{n}` (base64, 32 bytes, versioned) and `IdentityStore:HmacKey`,
/// or else keys derived from founder secret1 with their own labels, so none is shared with contact history,
/// evidence or the email holds. Azure Key Vault when hosted (not built).
/// </summary>
public sealed class IdentityKeys(IConfiguration configuration, Founders founders)
{
    public IReadOnlyDictionary<byte, byte[]> Encryption { get; } = Read(configuration, founders);
    public byte? CurrentVersion => Encryption.Count == 0 ? null : Encryption.Keys.Max();
    public byte[]? Hmac => configuration["IdentityStore:HmacKey"] is { Length: > 0 } configured ? Convert.FromBase64String(configured)
        : founders.DeriveLegacyKey("swoovr:identity-store:match:v1");
    public bool Configured => CurrentVersion is not null && Hmac is not null;

    private static Dictionary<byte, byte[]> Read(IConfiguration configuration, Founders founders)
    {
        var keys = new Dictionary<byte, byte[]>();
        foreach (var entry in configuration.GetSection("IdentityStore:Keys").GetChildren())
            if (byte.TryParse(entry.Key, out var version) && entry.Value is { Length: > 0 } value && Convert.FromBase64String(value) is { Length: 32 } key)
                keys[version] = key;
        // #89: the derived key stays version 1 (still readable) after newer versions are configured, until `keys rotate`.
        if (!keys.ContainsKey(1) && founders.DeriveLegacyKey("swoovr:identity-store:encryption:v1") is { } legacy)
            keys[1] = legacy;
        return keys;
    }
}

/// <summary>
/// The verified identity store (policy §1 principle 2, §2.1–2.3; owner Q78, D191). Records the minimum verified fields
/// when an ID check is approved (re-reading Didit when a webhook carried no document data), flags a match with a
/// terminated member's record to the admin queue (never blocks, never proof), and opens a record only for the
/// `legal` role with a recorded purpose. Nothing here is logged or audited with the fields themselves.
/// </summary>
public sealed class VerifiedIdentityStore(IServiceProvider services, IdentityKeys keys, IIdVerificationProvider provider, ILogger<VerifiedIdentityStore> log)
{
    private SwoovrDbContext db => services.GetRequiredService<SwoovrDbContext>();

    public bool Configured => keys.Configured;

    /// <summary>Records (or replaces) the member's verified identity from an approved session; unsaved. Returns whether one was kept.</summary>
    public async Task<bool> RecordAsync(Guid account, IdVerificationAttempt attempt, ProviderSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (keys.CurrentVersion is not { } version || keys.Hmac is not { } hmac)
        {
            log.LogWarning("Identity store keys are not configured; no verified identity kept.");
            return false;
        }
        var fields = session.Identity;
        if (fields is null && provider.Configured && await provider.GetDecisionAsync(attempt.SessionId, cancellationToken) is { } decision && decision.SessionId == attempt.SessionId)
            fields = decision.Identity;
        if (fields is null) return false;
        var record = VerifiedIdentityPolicy.Record(account, attempt, fields, now, version, keys.Encryption[version], hmac);
        var existing = await db.VerifiedIdentities.FindAsync([account], cancellationToken);
        if (existing is not null) db.VerifiedIdentities.Remove(existing);
        db.VerifiedIdentities.Add(record);
        Audit.Record(db, now, null, "system", "identity.recorded", "account", account.ToString(), record.MatchHmac is null ? "no-match-key" : null);
        await FlagIfTerminatedMatchAsync(account, record.MatchHmac, now, cancellationToken);
        return true;
    }

    /// <summary>
    /// D191 (policy §8): a newly verified identity matching a terminated member's record (kept after their deletion, or
    /// on an account still terminated) opens a case in the admin queue. Nothing is blocked; a match is a lead, not proof.
    /// </summary>
    private async Task FlagIfTerminatedMatchAsync(Guid account, byte[]? match, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (match is null) return;
        var terminated = await (from i in db.VerifiedIdentities
                                where i.MatchHmac == match && i.AccountId != account
                                      && (i.RetainUntil != null || db.Sanctions.Any(s => s.AccountId == i.AccountId && s.Kind == SanctionKind.Termination && s.LiftedAt == null))
                                select i.AccountId).AnyAsync(cancellationToken);
        if (!terminated) return;
        if (await db.ModerationCases.AnyAsync(c => c.SubjectAccountId == account && (c.Status == CaseStatus.Open || c.Status == CaseStatus.UnderReview), cancellationToken)) return;
        var item = new ModerationCase
        {
            Id = Guid.NewGuid(), SubjectAccountId = account, Status = CaseStatus.Open, Queue = CaseQueue.Admin, Priority = ReportPriority.High,
            CreatedAt = now, UpdatedAt = now, SystemFlag = "returning-terminated-identity",
        };
        db.ModerationCases.Add(item);
        Audit.Record(db, now, null, "system", "account.returning-terminated", "moderation-case", item.Id.ToString(), "identity");
    }

    /// <summary>Opens a record for the `legal` role, recording who, whose and why. Null when there is none.</summary>
    public async Task<VerifiedIdentityFields?> OpenForAsync(Guid account, Guid actor, string purpose, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var record = await db.VerifiedIdentities.FindAsync([account], cancellationToken);
        if (record is null) return null;
        db.IdentityAccesses.Add(new IdentityAccess { AccountId = account, ActorAccountId = actor, Purpose = purpose.Trim(), At = now });
        Audit.Record(db, now, actor, "account", "identity.decrypted", "account", account.ToString());
        await db.SaveChangesAsync(cancellationToken);
        return VerifiedIdentityPolicy.Open(keys.Encryption, record.Sealed);
    }

    /// <summary>The member's own record, for their data export (D172, D191). Null when there is none or the key is gone.</summary>
    public async Task<VerifiedIdentityFields?> OwnAsync(Guid account, CancellationToken cancellationToken) =>
        await db.VerifiedIdentities.FindAsync([account], cancellationToken) is { } record && keys.Encryption.ContainsKey(record.Sealed[0])
            ? VerifiedIdentityPolicy.Open(keys.Encryption, record.Sealed) : null;

    /// <summary>Opens a record for preservation under a legal hold (D189); never for display.</summary>
    public VerifiedIdentityFields? OpenForEvidence(VerifiedIdentity record) =>
        keys.Encryption.ContainsKey(record.Sealed[0]) ? VerifiedIdentityPolicy.Open(keys.Encryption, record.Sealed) : null;
}
