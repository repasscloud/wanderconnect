using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Swoovr.Domain;

public enum BannedIdentifierType { Email, Mobile }
public enum BanStatus { Active, Revoked, Expired }

/// <summary>D217: restricted anti-abuse fingerprint, never the plaintext identifier or the HMAC key.</summary>
public sealed class BannedIdentifier
{
    public Guid Id { get; set; }
    public BannedIdentifierType IdentifierType { get; set; }
    public byte[] IdentifierHash { get; set; } = [];
    public int KeyVersion { get; set; }
    public Guid CaseId { get; set; }
    public string BanReasonCode { get; set; } = "";
    public BanStatus Status { get; set; }
    public DateTimeOffset BannedAt { get; set; }
    public DateTimeOffset? BanExpiresAt { get; set; }
    public DateTimeOffset? LastReviewedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public DateTimeOffset? LastMatchedAt { get; set; }
    public long MatchCount { get; set; }
}

/// <summary>Restricted immutable transition evidence; never copied into generic logs or member responses.</summary>
public sealed class BanTransition
{
    public Guid Id { get; set; }
    public Guid BannedIdentifierId { get; set; }
    public Guid CaseId { get; set; }
    public Guid? ActorAccountId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Action { get; set; } = "";
    public BanStatus? OldStatus { get; set; }
    public BanStatus NewStatus { get; set; }
    public DateTimeOffset? OldExpiresAt { get; set; }
    public DateTimeOffset? NewExpiresAt { get; set; }
    public string Reason { get; set; } = "";
    public string OldValues { get; set; } = "{}";
    public string NewValues { get; set; } = "{}";
}

/// <summary>Associated case note committed with its immutable registry transition.</summary>
public sealed class BanCaseNote
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid TransitionId { get; set; }
    public Guid? AuthorAccountId { get; set; }
    public DateTimeOffset At { get; set; }
    public string Text { get; set; } = "";
}

public static class BannedIdentifierPolicy
{
    private static readonly JsonSerializerOptions SnapshotOptions = new() { Converters = { new JsonStringEnumConverter() } };
    public const int ReasonMax = 1000;
    public const int HistoryMonths = 24;
    public const int NecessityReviewMonths = 6;

    public static string? Normalise(BannedIdentifierType type, string? value) => type switch
    {
        BannedIdentifierType.Email => EmailAddress.Normalise(value),
        BannedIdentifierType.Mobile => PhoneVerificationPolicy.Normalise(value),
        _ => null,
    };

    public static byte[] Fingerprint(BannedIdentifierType type, string value, ReadOnlySpan<byte> key) =>
        HashCanonical(type, Normalise(type, value) ?? throw new ArgumentException("Invalid identifier.", nameof(value)), key);

    /// <summary>Type-separated primitive; application callers use Fingerprint to normalise before hashing.</summary>
    public static byte[] HashCanonical(BannedIdentifierType type, string canonical, ReadOnlySpan<byte> key)
    {
        var label = type switch
        {
            BannedIdentifierType.Email => "email", BannedIdentifierType.Mobile => "mobile",
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };
        if (key.Length < 32) throw new ArgumentException("HMAC keys require at least32 bytes.", nameof(key));
        return HMACSHA256.HashData(key, Encoding.UTF8.GetBytes("swoovr/banned-identifier/" + label + "\0" + canonical));
    }

    public static bool Blocks(BannedIdentifier row, DateTimeOffset now) =>
        row.Status == BanStatus.Active && (row.BanExpiresAt is null || row.BanExpiresAt > now);

    public static (BannedIdentifier Entry, BanTransition Transition, BanCaseNote Note) Issue(BannedIdentifierType type,byte[] fingerprint,int version,Guid caseId,
        Guid? actor,DateTimeOffset bannedAt,string reason)
    {
        if (!Enum.IsDefined(type) || fingerprint.Length != 32 || version < 1 || caseId == Guid.Empty || actor == Guid.Empty)
            throw new ArgumentException("Documented typed fingerprint provenance is required.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > ReasonMax) throw new ArgumentException("A bounded explanation is required.",nameof(reason));
        var at = bannedAt.ToUniversalTime();
        var entry = new BannedIdentifier { Id = Guid.NewGuid(),IdentifierType = type,IdentifierHash = [..fingerprint],KeyVersion = version,
            CaseId = caseId,BanReasonCode = "permanent-account-ban",Status = BanStatus.Active,BannedAt = at };
        var transition = new BanTransition { Id = Guid.NewGuid(),BannedIdentifierId = entry.Id,CaseId = caseId,ActorAccountId = actor,
            At = at,Action = "created",NewStatus = BanStatus.Active,Reason = reason.Trim(),NewValues = JsonSerializer.Serialize(entry,SnapshotOptions) };
        return (entry,transition,new BanCaseNote { Id = Guid.NewGuid(),CaseId = caseId,TransitionId = transition.Id,AuthorAccountId = actor,At = at,Text = reason.Trim() });
    }

    /// <summary>Creates one auditable identifier-only transition; callers commit row, event and note atomically.</summary>
    public static (BanTransition Transition, BanCaseNote Note) Revoke(BannedIdentifier row, Guid actor, string reason, DateTimeOffset now)
    {
        if (actor == Guid.Empty) throw new ArgumentException("A staff actor is required.", nameof(actor));
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > ReasonMax) throw new ArgumentException("A bounded explanation is required.", nameof(reason));
        if (row.Status != BanStatus.Active || row.Id == Guid.Empty || row.CaseId == Guid.Empty)
            throw new InvalidOperationException("Only a documented active restriction can be revoked.");
        var at = now.ToUniversalTime();
        var transition = new BanTransition { Id = Guid.NewGuid(), BannedIdentifierId = row.Id, CaseId = row.CaseId,
            ActorAccountId = actor, At = at, Action = "revoked", Reason = reason.Trim(), OldStatus = row.Status,
            OldExpiresAt = row.BanExpiresAt, OldValues = JsonSerializer.Serialize(row, SnapshotOptions) };
        row.Status = BanStatus.Revoked;row.BanExpiresAt = at;row.RevokedAt = at;row.RevokedBy = actor;row.LastReviewedAt = at;
        transition.NewStatus = row.Status;transition.NewExpiresAt = row.BanExpiresAt;transition.NewValues = JsonSerializer.Serialize(row, SnapshotOptions);
        return (transition, new BanCaseNote { Id = Guid.NewGuid(), CaseId = row.CaseId, TransitionId = transition.Id,
            AuthorAccountId = actor, At = at, Text = reason.Trim() });
    }

    public static (BanTransition Transition,BanCaseNote Note) ReviewNecessity(BannedIdentifier row,Guid actor,string reason,DateTimeOffset now)
    {
        if(row.Status!=BanStatus.Active||row.BanExpiresAt<=now)throw new InvalidOperationException("Only an active unexpired restriction can be reviewed as necessary.");
        var result=RecordReinstatement(row,actor,reason,now);
        result.Transition.Action="necessity-reviewed";
        return result;
    }

    public static (BanTransition Transition,BanCaseNote Note) RecordReinstatement(BannedIdentifier row,Guid actor,string reason,DateTimeOffset now)
    {
        if(actor==Guid.Empty||string.IsNullOrWhiteSpace(reason)||reason.Trim().Length>ReasonMax)throw new ArgumentException("A staff actor and bounded explanation are required.");
        var at=now.ToUniversalTime();var before=JsonSerializer.Serialize(row,SnapshotOptions);row.LastReviewedAt=at;
        var transition=new BanTransition { Id=Guid.NewGuid(),BannedIdentifierId=row.Id,CaseId=row.CaseId,ActorAccountId=actor,At=at,Action="account-reinstated",
            OldStatus=row.Status,NewStatus=row.Status,OldExpiresAt=row.BanExpiresAt,NewExpiresAt=row.BanExpiresAt,Reason=reason.Trim(),OldValues=before,NewValues=JsonSerializer.Serialize(row,SnapshotOptions) };
        return(transition,new BanCaseNote { Id=Guid.NewGuid(),CaseId=row.CaseId,TransitionId=transition.Id,AuthorAccountId=actor,At=at,Text=reason.Trim() });
    }

    /// <summary>Append truthful system processing provenance without restarting the original expiry/history clock.</summary>
    public static (BanTransition Transition,BanCaseNote Note) Expire(BannedIdentifier row,DateTimeOffset now)
    {
        if(row.Id==Guid.Empty||row.CaseId==Guid.Empty||row.Status!=BanStatus.Active||row.BanExpiresAt is null||row.BanExpiresAt>now)
            throw new InvalidOperationException("Only a documented due active restriction can expire.");
        var before=JsonSerializer.Serialize(row,SnapshotOptions);row.Status=BanStatus.Expired;
        const string reason="Scheduled identifier restriction expired. Original expiry and account enforcement remain unchanged.";
        var transition=new BanTransition {Id=Guid.NewGuid(),BannedIdentifierId=row.Id,CaseId=row.CaseId,At=now.ToUniversalTime(),Action="expired",
            OldStatus=BanStatus.Active,NewStatus=BanStatus.Expired,OldExpiresAt=row.BanExpiresAt,NewExpiresAt=row.BanExpiresAt,
            Reason=reason,OldValues=before,NewValues=JsonSerializer.Serialize(row,SnapshotOptions)};
        return(transition,new BanCaseNote{Id=Guid.NewGuid(),CaseId=row.CaseId,TransitionId=transition.Id,At=transition.At,Text=reason});
    }

    /// <summary>DEFAULT inactive history minimum; active restrictions and missing transition provenance cannot expire.</summary>
    public static DateTimeOffset? HistoryUntil(BannedIdentifier row)
    {
        var ended = row.Status switch { BanStatus.Revoked => row.RevokedAt, BanStatus.Expired => row.BanExpiresAt, _ => null };
        if (ended is null) return null;
        var latest = new[] { ended.Value, row.BannedAt, row.LastReviewedAt ?? ended.Value }.Max();
        return latest.ToUniversalTime().AddMonths(HistoryMonths);
    }
}

/// <summary>Short-lived, content-free authenticated check quota; hashes never include the queried email/mobile.</summary>
public sealed class BanCheckAttempt
{
    public long Id { get; set; }
    public byte[] CallerHash { get; set; } = [];
    public byte[]? AddressHash { get; set; }
    public int KeyVersion { get; set; }
    public BannedIdentifierType IdentifierType { get; set; }
    public Guid? AccountId { get; set; }
    public Guid[] MatchedBanIds { get; set; } = [];
    public DateTimeOffset At { get; set; }
}
