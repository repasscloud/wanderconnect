namespace Swoovr.Domain;

/// <summary>D216: owner-approved-in-principle periods; legal review57 remains required. Null means no disposal clock.</summary>
public sealed record SafetyRetentionWindow(DateTimeOffset? ContentUntil, DateTimeOffset? RecordsUntil);

public static class SafetyRetentionPolicy
{
    public static SafetyRetentionWindow ForCase(ModerationCase item, bool serious, DateTimeOffset? referredAt)
    {
        if (item.Status != CaseStatus.Closed || item.ClosedAt is not { } closed) return new(null, null);
        var anchor = referredAt is { } referral && referral > closed ? referral : closed;
        anchor = anchor.ToUniversalTime();
        return new(anchor.AddMonths(serious ? 24 : 12), anchor.AddMonths(24));
    }

    public static bool MayDispose(DateTimeOffset? until, DateTimeOffset now, bool held) =>
        !held && until is { } expiry && expiry <= now;
}
