using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Evidence;

/// <summary>
/// The evidence keys (D189): `Evidence:Keys:{n}` (base64, 32 bytes, versioned), or else one derived from the current
/// founder secret with its own label, so no key is shared with contact history or the email holds. Key Vault when hosted.
/// </summary>
public sealed class EvidenceKeys(IConfiguration configuration, Founders founders)
{
    public IReadOnlyDictionary<byte, byte[]> Encryption { get; } = Read(configuration, founders);
    public byte? CurrentVersion => Encryption.Count == 0 ? null : Encryption.Keys.Max();

    private static Dictionary<byte, byte[]> Read(IConfiguration configuration, Founders founders)
    {
        var keys = new Dictionary<byte, byte[]>();
        foreach (var entry in configuration.GetSection("Evidence:Keys").GetChildren())
            if (byte.TryParse(entry.Key, out var version) && entry.Value is { Length: > 0 } value && Convert.FromBase64String(value) is { Length: 32 } key)
                keys[version] = key;
        // #89: the derived key stays version 1 (still readable) after newer versions are configured, until `keys rotate`.
        if (!keys.ContainsKey(1) && founders.CurrentVersion is { } v)
            keys[1] = HMACSHA256.HashData(founders.Secrets[v], Encoding.UTF8.GetBytes("swoovr:evidence:encryption:v1"));
        return keys;
    }
}

/// <summary>
/// Legal holds and evidence preservation (policy §3, §7, §13; D189). Captures what a hold covers into the restricted
/// evidence store, keeps the hash-chained custody log, tells the purge jobs what they must leave alone, and erases a
/// hold's evidence when it is released. Nothing here writes content to logs or the audit trail.
/// </summary>
public sealed class EvidencePreservation(IServiceProvider services, EvidenceKeys keys, ContactHistoryKeys contactKeys)
{
    private SwoovrDbContext db => services.GetRequiredService<SwoovrDbContext>();

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    public bool Configured => keys.CurrentVersion is not null;

    /// <summary>Live holds and their scopes, for the purge jobs and capture.</summary>
    public async Task<(List<LegalHold> Holds, List<LegalHoldScope> Scopes)> LiveAsync(CancellationToken cancellationToken)
    {
        var holds = await db.LegalHolds.Where(h => h.Status != LegalHoldStatus.Released).ToListAsync(cancellationToken);
        var ids = holds.Select(h => h.Id).ToList();
        return (holds, await db.LegalHoldScopes.Where(s => ids.Contains(s.HoldId)).ToListAsync(cancellationToken));
    }

    /// <summary>Accounts any live hold names: their tombstones, cases and contact history must not be purged.</summary>
    public async Task<HashSet<Guid>> HeldAccountsAsync(CancellationToken cancellationToken) =>
        (await (from s in db.LegalHoldScopes join h in db.LegalHolds on s.HoldId equals h.Id
                where h.Status != LegalHoldStatus.Released && s.Kind == HoldScopeKind.Account select s.TargetId).ToListAsync(cancellationToken)).ToHashSet();

    /// <summary>
    /// D195 (procedure §14): purges fail closed. These return null when hold status can't be read reliably, and every
    /// purge path then deletes nothing. `Evidence:HoldCheckUnavailable` simulates that in Development only (tests).
    /// </summary>
    public async Task<HashSet<Guid>?> TryHeldAccountsAsync(CancellationToken cancellationToken)
    {
        if (SimulatedUnavailable) return null;
        try { return await HeldAccountsAsync(cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<EvidencePreservation>>().LogError(exception, "Hold status unavailable; purging nothing.");
            return null;
        }
    }

    public async Task<(List<LegalHold> Holds, List<LegalHoldScope> Scopes)?> TryLiveAsync(CancellationToken cancellationToken)
    {
        if (SimulatedUnavailable) return null;
        try { return await LiveAsync(cancellationToken); }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            services.GetRequiredService<ILogger<EvidencePreservation>>().LogError(exception, "Hold status unavailable; purging nothing.");
            return null;
        }
    }

    private bool SimulatedUnavailable => services.GetRequiredService<IHostEnvironment>().IsDevelopment()
        && services.GetRequiredService<IConfiguration>().GetValue("Evidence:HoldCheckUnavailable", false);

    /// <summary>D195 (§7): whether a live hold names this account, for admin deletion routes. Null when unknown (refuse).</summary>
    public async Task<bool?> HeldAsync(Guid account, CancellationToken cancellationToken) =>
        await TryHeldAccountsAsync(cancellationToken) is { } held ? held.Contains(account) : null;

    /// <summary>Appends one entry to a hold's custody chain (ids and machine values only).</summary>
    public async Task CustodyAsync(Guid hold, Guid? actor, string action, string? detail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var previous = await db.CustodyEvents.Where(c => c.HoldId == hold).OrderByDescending(c => c.Sequence).Select(c => c.Hash).FirstOrDefaultAsync(cancellationToken) ?? [];
        // Pending entries for the same hold in this unit of work chain on from the newest of them.
        var pending = db.ChangeTracker.Entries<CustodyEvent>().Where(e => e.State == EntityState.Added && e.Entity.HoldId == hold).Select(e => e.Entity).LastOrDefault();
        if (pending is not null) previous = pending.Hash;
        var at = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());
        db.CustodyEvents.Add(new CustodyEvent { HoldId = hold, At = at, ActorAccountId = actor, Action = action, Detail = detail, PreviousHash = previous,
            Hash = EvidenceSeal.Chain(previous, hold, at, actor, action, detail) });
    }

    /// <summary>
    /// Captures everything the hold covers that isn't already preserved with the same content: accounts, profiles and
    /// per-mode presentations, identity choices, verified number and contact history, requests, conversations and their
    /// messages (deleted-for-both text while it is still kept, D167), reports, cases and their evidence, sanctions,
    /// verification outcomes and audit entries, within the hold's period. Returns how many items were added.
    /// </summary>
    public async Task<int> CaptureAsync(LegalHold hold, Guid? actor, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (keys.CurrentVersion is not { } version) throw new InvalidOperationException("Evidence keys are not configured.");
        var scopes = await db.LegalHoldScopes.Where(s => s.HoldId == hold.Id).ToListAsync(cancellationToken);
        var accounts = scopes.Where(s => s.Kind == HoldScopeKind.Account).Select(s => s.TargetId).ToList();
        var named = scopes.Where(s => s.Kind == HoldScopeKind.Conversation).Select(s => s.TargetId).ToList();
        var known = (await db.EvidenceItems.Where(i => i.HoldId == hold.Id).Select(i => new { i.Kind, i.RecordId, i.Sha256 }).ToListAsync(cancellationToken))
            .Select(i => (i.Kind, i.RecordId, Convert.ToHexString(i.Sha256))).ToHashSet();
        var capture = 1 + (await db.EvidenceItems.Where(i => i.HoldId == hold.Id).Select(i => (int?)i.Capture).MaxAsync(cancellationToken) ?? 0);
        var added = 0;
        void Add(string kind, string source, string recordId, Guid? account, object content)
        {
            var json = JsonSerializer.Serialize(content, Json);
            var hash = EvidenceSeal.Hash(json);
            if (!known.Add((kind, recordId, Convert.ToHexString(hash)))) return;
            db.EvidenceItems.Add(new EvidenceItem
            {
                Id = Guid.NewGuid(), HoldId = hold.Id, Capture = capture, Kind = kind, Source = source, RecordId = recordId, AccountId = account,
                CapturedAt = now, CapturedBy = actor, Sha256 = hash, Sealed = EvidenceSeal.Seal(version, keys.Encryption[version], json),
            });
            added++;
        }
        bool InPeriod(DateTimeOffset at) => LegalHoldPolicy.InPeriod(hold, at);

        foreach (var id in accounts)
        {
            if (await db.Accounts.FindAsync([id], cancellationToken) is { } account) Add("account", "accounts", id.ToString(), id, account);
            if (await db.MemberProfiles.FindAsync([id], cancellationToken) is { } profile) Add("profile", "member_profiles", id.ToString(), id, profile);
            // #91: what others could see in each mode over the period.
            foreach (var shown in await db.ProfileVersions.Where(v => v.AccountId == id).OrderBy(v => v.CapturedAt).ToListAsync(cancellationToken))
                if (InPeriod(shown.CapturedAt)) Add("profile-version", "profile_versions", shown.Id.ToString(), id, shown);
            foreach (var mode in await db.MemberModes.Where(m => m.AccountId == id).ToListAsync(cancellationToken))
                Add("mode", "member_modes", $"{id:N}:{mode.ModeId}", id, new
                {
                    Mode = mode,
                    Intentions = await db.MemberModeIntentions.Where(i => i.AccountId == id && i.ModeId == mode.ModeId).Select(i => i.IntentionId).ToListAsync(cancellationToken),
                });
            if (await db.MemberIdentities.FindAsync([id], cancellationToken) is { } identity) Add("identity", "member_identities", id.ToString(), id, identity);
            if (await db.MemberPhones.FindAsync([id], cancellationToken) is { } phone) Add("phone", "member_phones", id.ToString(), id, phone);
            foreach (var past in await db.ContactHistories.Where(h => h.AccountId == id).ToListAsync(cancellationToken))
                Add("contact-history", "contact_histories", past.Id.ToString(), id, new
                {
                    past.Id, past.AccountId, past.ContactType, past.ValidFrom, past.ValidUntil, past.ChangedBy, past.ChangeReason,
                    Value = contactKeys.Encryption.ContainsKey(past.EncryptedValue[0]) ? ContactHistoryPolicy.Decrypt(contactKeys.Encryption, past.EncryptedValue) : null,
                });
            foreach (var request in await db.ConnectionRequests.Where(r => r.FromAccountId == id || r.ToAccountId == id).ToListAsync(cancellationToken))
                if (InPeriod(request.CreatedAt)) Add("request", "connection_requests", request.Id.ToString(), id, request);
            foreach (var block in await db.Blocks.Where(b => b.BlockerAccountId == id || b.BlockedAccountId == id).ToListAsync(cancellationToken))
                if (InPeriod(block.CreatedAt)) Add("block", "blocks", $"{block.BlockerAccountId:N}{block.BlockedAccountId:N}", id, block);
            foreach (var conversation in await db.Conversations.Where(c => (c.PairLow == id || c.PairHigh == id) && (named.Count == 0 || named.Contains(c.Id))).ToListAsync(cancellationToken))
            {
                Add("conversation", "conversations", conversation.Id.ToString(), id, conversation);
                foreach (var message in await db.Messages.Where(m => m.ConversationId == conversation.Id).OrderBy(m => m.SentAt).ToListAsync(cancellationToken))
                    if (InPeriod(message.SentAt))
                        Add("message", "messages", message.Id.ToString(), message.SenderAccountId, new
                        {
                            Message = message,
                            HiddenBy = await db.MessageHides.Where(h => h.MessageId == message.Id).Select(h => new { h.AccountId, h.HiddenAt }).ToListAsync(cancellationToken),
                        });
            }
            foreach (var report in await db.Reports.Where(r => r.ReporterAccountId == id || r.SubjectAccountId == id).ToListAsync(cancellationToken))
                if (InPeriod(report.CreatedAt))
                    Add("report", "reports", report.Id.ToString(), id, new
                    {
                        report.Id, report.ReporterAccountId, report.SubjectAccountId, report.SubjectType, report.SubjectRef, report.CategoryId, report.Priority,
                        report.Text, report.CaseId, report.CreatedAt, report.Police,
                        // D190: the reporter's police reference, opened into the sealed evidence item.
                        PoliceReference = report.PoliceReference is { } sealedReference && keys.Encryption.ContainsKey(sealedReference[0]) ? EvidenceSeal.Open(keys.Encryption, sealedReference) : null,
                    });
            foreach (var item in await db.ModerationCases.Where(c => c.SubjectAccountId == id).ToListAsync(cancellationToken))
            {
                Add("case", "moderation_cases", item.Id.ToString(), id, item);
                foreach (var evidence in await db.CaseEvidence.Where(e => e.CaseId == item.Id).ToListAsync(cancellationToken))
                    Add("case-evidence", "case_evidence", evidence.Id.ToString(), id, evidence);
            }
            foreach (var sanction in await db.Sanctions.Where(s => s.AccountId == id).ToListAsync(cancellationToken))
                Add("sanction", "sanctions", sanction.Id.ToString(), id, sanction);
            foreach (var attempt in await db.IdVerificationAttempts.Where(a => a.AccountId == id).ToListAsync(cancellationToken))
                Add("verification", "id_verification_attempts", attempt.Id.ToString(), id, new { attempt.Id, attempt.SessionId, attempt.State, attempt.ProviderStatus, attempt.CreatedAt, attempt.UpdatedAt });
            var key = id.ToString();
            foreach (var entry in await db.AuditEntries.Where(e => e.ActorAccountId == id || e.TargetId == key).OrderBy(e => e.At).Take(2000).ToListAsync(cancellationToken))
                if (InPeriod(entry.At)) Add("audit", "audit_entries", entry.Id.ToString(), id, entry);
            await ExtraAsync(hold, id, Add, cancellationToken);
        }
        await CustodyAsync(hold.Id, actor, "captured", $"capture:{capture};items:{added}", now, cancellationToken);
        return added;
    }

    /// <summary>D191: the verified identity record, opened into the sealed evidence item (only `legal` can export it).</summary>
    private async Task ExtraAsync(LegalHold hold, Guid account, Action<string, string, string, Guid?, object> add, CancellationToken cancellationToken)
    {
        if (await db.VerifiedIdentities.FindAsync([account], cancellationToken) is not { } record) return;
        var fields = services.GetRequiredService<Verification.VerifiedIdentityStore>().OpenForEvidence(record);
        add("identity-record", "restricted.verified_identities", account.ToString(), account, new
        {
            record.AccountId, record.SessionId, record.AttemptId, record.VerifiedAt, record.Method, record.Decision, record.IssuingCountry, record.RetainUntil, Fields = fields,
        });
    }

    /// <summary>
    /// Before final deletion erases an account's records, every live hold over it captures them again, so deletion still
    /// completes for everything the member and others see while held records stay in the evidence store (D189).
    /// </summary>
    public async Task<int> PreserveBeforeDeletionAsync(Guid account, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var holds = await (from s in db.LegalHoldScopes join h in db.LegalHolds on s.HoldId equals h.Id
                           where h.Status != LegalHoldStatus.Released && s.Kind == HoldScopeKind.Account && s.TargetId == account select h).ToListAsync(cancellationToken);
        var added = 0;
        foreach (var hold in holds) added += await CaptureAsync(hold, null, now, cancellationToken);
        if (holds.Count > 0) await db.SaveChangesAsync(cancellationToken);
        return added;
    }

    /// <summary>
    /// D167 with a hold: deleted-for-both message text past its 30 days is kept while a live hold covers the conversation
    /// (either member named, the message inside the hold's period).
    /// </summary>
    public static bool MessageHeld(Message message, Conversation conversation, IReadOnlyList<LegalHold> holds, IReadOnlyCollection<LegalHoldScope> scopes) =>
        holds.Any(h => LegalHoldPolicy.Covers(h, scopes, conversation.PairLow, conversation.Id, message.SentAt)
            || LegalHoldPolicy.Covers(h, scopes, conversation.PairHigh, conversation.Id, message.SentAt));

    /// <summary>Releasing a hold erases its evidence items; the hold record and its custody chain stay as the record of what happened.</summary>
    /// <remarks>Overlapping holds keep their own items (§7: each operates independently), so only this hold's copies go.</remarks>
    public async Task<int> ReleaseAsync(LegalHold hold, Guid actor, string reason, DateTimeOffset now, CancellationToken cancellationToken, int overlaps = 0)
    {
        (hold.Status, hold.ReleasedAt, hold.ReleasedBy, hold.ReleaseReason) = (LegalHoldStatus.Released, now, actor, reason.Trim());
        var erased = await db.EvidenceItems.Where(i => i.HoldId == hold.Id).ExecuteDeleteAsync(cancellationToken);
        await CustodyAsync(hold.Id, actor, "released", $"items-erased:{erased};overlaps:{overlaps}", now, cancellationToken);
        return erased;
    }

    public string Open(EvidenceItem item) => EvidenceSeal.Open(keys.Encryption, item.Sealed);
}
