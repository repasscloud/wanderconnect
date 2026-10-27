using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Domain;

public enum LegalHoldStatus { Provisional, Active, Released }

/// <summary>
/// A legal hold (internal trust and safety policy §3, §13; owner Q76, D189): a narrow instruction to preserve the records
/// of named accounts (optionally only some of their conversations) within a date range. It records its reason,
/// authoriser, scope, review date and release conditions; it never expires on its own and only the `legal` role releases
/// it. A provisional hold (opened by an admin from a case, or by Swoovr itself for a serious-incident report) protects
/// the same way until `legal` confirms or releases it. The member is never told.
/// </summary>
public sealed class LegalHold
{
    public Guid Id { get; set; }
    public LegalHoldStatus Status { get; set; }
    /// <summary>Staff-written reason; shown only to `legal` (never in the audit log).</summary>
    public string Reason { get; set; } = "";
    public string? ReleaseConditions { get; set; }
    /// <summary>"staff" or "serious-incident".</summary>
    public string Source { get; set; } = "staff";
    public Guid? CaseId { get; set; }
    /// <summary>Null when Swoovr opened it (a serious-incident report).</summary>
    public Guid? PlacedBy { get; set; }
    public AccountRole PlacedByRole { get; set; }
    public DateTimeOffset PlacedAt { get; set; }
    public Guid? ConfirmedBy { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset ReviewDue { get; set; }
    /// <summary>The preserved period; open ends mean "from the start" or "until released".</summary>
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? Until { get; set; }
    public DateTimeOffset? ReleasedAt { get; set; }
    public Guid? ReleasedBy { get; set; }
    public string? ReleaseReason { get; set; }
    /// <summary>D195 (procedure §7): the law-enforcement case it serves, its lawful justification and the requesting body.</summary>
    public Guid? LeCaseId { get; set; }
    public string? LawfulBasis { get; set; }
    public string? RequestingBody { get; set; }
    /// <summary>Null for an ordinary hold; "police-preservation" for a temporary one (reviewed within 30 days, §7).</summary>
    public string? Kind { get; set; }
}

public enum HoldScopeKind { Account, Conversation }

/// <summary>One account or conversation a hold covers. A hold naming conversations covers only those for its accounts.</summary>
public sealed class LegalHoldScope
{
    public Guid HoldId { get; set; }
    public HoldScopeKind Kind { get; set; }
    public Guid TargetId { get; set; }
}

/// <summary>
/// One preserved record (§3: integrity hashes, timestamps, provenance). Content is canonical JSON, sealed with a
/// versioned evidence key held outside the database; the SHA-256 is of the plain JSON so an export can be checked. Items
/// are immutable: a later capture adds items, never edits them. Erased when the last hold over them is released.
/// </summary>
public sealed class EvidenceItem
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public int Capture { get; set; }
    /// <summary>account, profile, mode, identity, phone, contact-history, request, conversation, message, report, case, case-evidence, sanction, verification, audit.</summary>
    public string Kind { get; set; } = "";
    /// <summary>Where it came from: the table and the record's id.</summary>
    public string Source { get; set; } = "";
    public string RecordId { get; set; } = "";
    public Guid? AccountId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public Guid? CapturedBy { get; set; }
    public byte[] Sha256 { get; set; } = [];
    /// <summary>Key version (1 byte), nonce (12), tag (16), ciphertext.</summary>
    public byte[] Sealed { get; set; } = [];
}

/// <summary>
/// The chain of custody (§3): every placement, confirmation, capture, review, view, export, disclosure and release, as
/// a hash chain (each entry's hash covers the previous one), so a removed or edited entry shows. Ids and machine values only.
/// </summary>
public sealed class CustodyEvent
{
    public long Sequence { get; set; }
    public Guid HoldId { get; set; }
    public DateTimeOffset At { get; set; }
    public Guid? ActorAccountId { get; set; }
    public string Action { get; set; } = "";
    public string? Detail { get; set; }
    public byte[] PreviousHash { get; set; } = [];
    public byte[] Hash { get; set; } = [];
}

/// <summary>A one-time evidence export link (§3, D189): 15 minutes, `legal` only, only a hash of the secret stored.</summary>
public sealed class EvidenceExport
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DownloadedAt { get; set; }
    public string Purpose { get; set; } = "";
    public Guid? DisclosureId { get; set; }
    /// <summary>D195 (§8): the case it was made for; a correction names the package it supplements and why.</summary>
    public Guid? LeCaseId { get; set; }
    public Guid? SupplementsExportId { get; set; }
    public string? CorrectionReason { get; set; }
    /// <summary>Recorded when downloaded: the package hash of the copy actually handed over (redactions applied).</summary>
    public byte[]? PackageSha256 { get; set; }
}

/// <summary>
/// The law-enforcement disclosure register (policy §3, APP 6.5 records; D189): who asked, on what legal basis, how the
/// request was verified, and what was given. Kept with the hold; `legal` only.
/// </summary>
public sealed class Disclosure
{
    public Guid Id { get; set; }
    public Guid HoldId { get; set; }
    public string Agency { get; set; } = "";
    public string Officer { get; set; } = "";
    public string Reference { get; set; } = "";
    /// <summary>One of <see cref="LegalHoldPolicy.LegalBases"/>.</summary>
    public string LegalBasis { get; set; } = "";
    public string VerifiedHow { get; set; } = "";
    public Guid DisclosedBy { get; set; }
    public DateTimeOffset DisclosedAt { get; set; }
}

public enum HoldRefusal { None, NotPermitted, ReasonRequired, ScopeRequired, BadDates, ReviewTooFar, CaseRequired, Released, NotProvisional, NotIndependent, OverlapsUnchecked }

/// <summary>Who may do what with holds and what a hold covers (D189). Facts come from application records only.</summary>
public static class LegalHoldPolicy
{
    public static readonly TimeSpan DefaultReview = TimeSpan.FromDays(90);
    public static readonly TimeSpan MaxReview = TimeSpan.FromDays(365);
    /// <summary>A provisional hold's review falls due after 30 days; it stays in force until confirmed or released.</summary>
    public static readonly TimeSpan ProvisionalReview = TimeSpan.FromDays(30);
    public static readonly TimeSpan ExportLifetime = TimeSpan.FromMinutes(15);
    public const int TextMax = 1000;
    public const int ScopeMax = 20;
    public static readonly IReadOnlyList<string> LegalBases = ["app-6.2e", "warrant", "subpoena", "court-order", "serious-threat", "consent"];

    public static bool Live(LegalHold hold) => hold.Status != LegalHoldStatus.Released;

    public static bool Overdue(LegalHold hold, DateTimeOffset now) => Live(hold) && hold.ReviewDue <= now;

    /// <summary>
    /// Placing a hold: `legal` places a confirmed hold; an admin only a provisional one from a case. A reason and at least
    /// one account are needed; the review is at most a year away; the range runs forwards.
    /// </summary>
    public static HoldRefusal CheckPlace(AccountRole role, string? reason, int accounts, Guid? caseId, DateTimeOffset? from, DateTimeOffset? until,
        DateTimeOffset? reviewDue, DateTimeOffset now)
    {
        if (role is not (AccountRole.Legal or AccountRole.Admin)) return HoldRefusal.NotPermitted;
        if (role == AccountRole.Admin && caseId is null) return HoldRefusal.CaseRequired;
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > TextMax) return HoldRefusal.ReasonRequired;
        if (accounts is < 1 or > ScopeMax) return HoldRefusal.ScopeRequired;
        if (from is { } f && until is { } u && u <= f) return HoldRefusal.BadDates;
        if (reviewDue is { } r && (r <= now || r - now > MaxReview)) return HoldRefusal.ReviewTooFar;
        return HoldRefusal.None;
    }

    public static LegalHoldStatus StatusFor(AccountRole role) => role == AccountRole.Legal ? LegalHoldStatus.Active : LegalHoldStatus.Provisional;

    public static DateTimeOffset ReviewFor(LegalHoldStatus status, DateTimeOffset? requested, DateTimeOffset now) =>
        status == LegalHoldStatus.Provisional ? now + ProvisionalReview : requested ?? now + DefaultReview;

    /// <summary>Confirming, reviewing, capturing, releasing and exporting are `legal`'s alone.</summary>
    public static HoldRefusal CheckManage(AccountRole role, LegalHold hold) =>
        role != AccountRole.Legal ? HoldRefusal.NotPermitted : !Live(hold) ? HoldRefusal.Released : HoldRefusal.None;

    /// <summary>
    /// D195 (procedure §7) replaces D189 here: release is approved only by a `legal` officer who neither placed nor
    /// confirmed the hold, after checking every overlapping hold.
    /// </summary>
    public static HoldRefusal CheckRelease(AccountRole role, LegalHold hold, string? reason, Guid releaser, int overlaps, bool overlapsChecked) =>
        CheckManage(role, hold) is var refusal and not HoldRefusal.None ? refusal
        : string.IsNullOrWhiteSpace(reason) || reason.Length > TextMax ? HoldRefusal.ReasonRequired
        : !LawEnforcementPolicy.IndependentRelease(hold, releaser) ? HoldRefusal.NotIndependent
        : overlaps > 0 && !overlapsChecked ? HoldRefusal.OverlapsUnchecked : HoldRefusal.None;

    /// <summary>A temporary police preservation hold (D195, §7) is reviewed at most 30 days ahead; others within a year.</summary>
    public static HoldRefusal CheckReview(AccountRole role, LegalHold hold, DateTimeOffset reviewDue, DateTimeOffset now) =>
        CheckManage(role, hold) is var refusal and not HoldRefusal.None ? refusal
        : reviewDue <= now || reviewDue - now > (hold.Kind == LawEnforcementPolicy.PolicePreservation ? LawEnforcementPolicy.TemporaryHoldMax : MaxReview)
            ? HoldRefusal.ReviewTooFar : HoldRefusal.None;

    /// <summary>Whether two holds protect any of the same accounts (§7: overlapping holds operate independently).</summary>
    public static int Overlaps(LegalHold hold, IReadOnlyCollection<LegalHold> live, IReadOnlyCollection<LegalHoldScope> scopes)
    {
        var mine = scopes.Where(s => s.HoldId == hold.Id && s.Kind == HoldScopeKind.Account).Select(s => s.TargetId).ToHashSet();
        return live.Count(h => h.Id != hold.Id && Live(h) && scopes.Any(s => s.HoldId == h.Id && s.Kind == HoldScopeKind.Account && mine.Contains(s.TargetId)));
    }

    /// <summary>Whether a moment falls inside the hold's preserved period.</summary>
    public static bool InPeriod(LegalHold hold, DateTimeOffset at) => (hold.From is not { } f || at >= f) && (hold.Until is not { } u || at <= u);

    /// <summary>
    /// Whether a live hold covers a record of <paramref name="account"/> (optionally in a conversation) dated
    /// <paramref name="at"/>. A hold naming conversations covers only those conversations' messages.
    /// </summary>
    public static bool Covers(LegalHold hold, IReadOnlyCollection<LegalHoldScope> scopes, Guid account, Guid? conversation, DateTimeOffset at)
    {
        if (!Live(hold) || !InPeriod(hold, at)) return false;
        var mine = scopes.Where(s => s.HoldId == hold.Id).ToList();
        if (!mine.Any(s => s.Kind == HoldScopeKind.Account && s.TargetId == account)) return false;
        var conversations = mine.Where(s => s.Kind == HoldScopeKind.Conversation).Select(s => s.TargetId).ToList();
        return conversation is not { } c || conversations.Count == 0 || conversations.Contains(c);
    }

    public static bool ValidDisclosure(string? agency, string? officer, string? reference, string? basis, string? verifiedHow) =>
        new[] { agency, officer, reference, verifiedHow }.All(v => !string.IsNullOrWhiteSpace(v) && v.Length <= 200)
        && basis is not null && LegalBases.Contains(basis);

    /// <summary>A one-time export link works once, before it expires, for a hold that is still live.</summary>
    public static bool ExportUsable(EvidenceExport? export, LegalHold? hold, DateTimeOffset now) =>
        export is not null && hold is not null && export.DownloadedAt is null && export.ExpiresAt > now && Live(hold);
}

/// <summary>Sealing and hashing for evidence (D189). Keys come from configuration, never the database.</summary>
public static class EvidenceSeal
{
    public static byte[] Hash(string json) => SHA256.HashData(Encoding.UTF8.GetBytes(json));

    public static byte[] Seal(byte version, byte[] key, string json) => ContactHistoryPolicy.Encrypt(version, key, json);

    public static string Open(IReadOnlyDictionary<byte, byte[]> keys, byte[] sealedValue) => ContactHistoryPolicy.Decrypt(keys, sealedValue);

    /// <summary>The package hash: SHA-256 over the items' hashes in export order, so one changed item changes it.</summary>
    public static byte[] PackageHash(IEnumerable<byte[]> itemHashes)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var item in itemHashes) hash.AppendData(item);
        return hash.GetHashAndReset();
    }

    /// <summary>The next custody entry's hash: the previous hash, then the entry's fields.</summary>
    public static byte[] Chain(byte[] previous, Guid hold, DateTimeOffset at, Guid? actor, string action, string? detail) =>
        SHA256.HashData([.. previous, .. Encoding.UTF8.GetBytes($"{hold}|{at.ToUnixTimeMilliseconds()}|{actor}|{action}|{detail}")]);

    /// <summary>Whether a custody log (in order) is an unbroken chain.</summary>
    public static bool ChainIntact(IReadOnlyList<CustodyEvent> events)
    {
        var previous = Array.Empty<byte>();
        foreach (var e in events)
        {
            if (!e.PreviousHash.SequenceEqual(previous) || !e.Hash.SequenceEqual(Chain(previous, e.HoldId, e.At, e.ActorAccountId, e.Action, e.Detail))) return false;
            previous = e.Hash;
        }
        return true;
    }
}
