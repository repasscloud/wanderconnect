namespace Swoovr.Domain;

public sealed class MediaSafetyAcknowledgement
{
    public Guid StaffAccountId { get; set; }
    public string Version { get; set; } = "";
    public DateTimeOffset AcknowledgedAt { get; set; }
}

/// <summary>Immutable decision metadata; member wording is derived from codes, never private notes or screening signals.</summary>
public sealed class MediaDecisionRecord
{
    public Guid Id { get; set; }
    public Guid MediaId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? ActorId { get; set; }
    public string Decision { get; set; } = "";
    public string Code { get; set; } = "";
    public string? Notes { get; set; }
    public DateTimeOffset At { get; set; }
}

public sealed class MediaAppeal
{
    public Guid Id { get; set; }
    public Guid DecisionId { get; set; }
    public Guid MediaId { get; set; }
    public Guid AccountId { get; set; }
    public string Text { get; set; } = "";
    public DateTimeOffset SubmittedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }
    public Guid? DecidedBy { get; set; }
    public string? Outcome { get; set; }
}

public static class PhotoReviewPolicy
{
    public const string NoticeVersion = "2026-11-02";
    public const string Notice = "Images may be distressing. If you suspect child sexual abuse material, stop viewing, do not download or share it, and escalate through the restricted safety workflow.";
    public static readonly TimeSpan AcknowledgementLifetime = TimeSpan.FromHours(4), AppealWindow = TimeSpan.FromDays(30);
    public static bool Acknowledged(DateTimeOffset? at, DateTimeOffset now) => at is { } value && value <= now && now < value + AcknowledgementLifetime;
    public static bool Appealable(string decision, string code, DateTimeOffset at, DateTimeOffset now) => decision == "reject"
        && (code.StartsWith("REJECT_", StringComparison.Ordinal) || code == "LEGACY_REJECT") && at <= now && now < at + AppealWindow;
    public static string MemberReason(string code) => code switch
    {
        "REJECT_EXPLICIT_BETA" => "This photo does not meet the current non-explicit photo rules.",
        "REJECT_IMPERSONATION" => "Use only photos you have the right to share, without impersonating someone else.",
        "REJECT_PRIVACY" => "This photo contains personal or location information that cannot be shown.",
        "REJECT_VIOLENCE" => "Graphic or disturbing images cannot be shown.",
        "REJECT_HATE" => "Hateful or prohibited extremist images cannot be shown.",
        "REJECT_SPAM" => "Spam or prohibited promotional images cannot be shown.",
        "REJECT_IMAGE_QUALITY" => "This photo does not meet the image-quality requirements.",
        _ => "This photo cannot be used on Swoovr.",
    };
}
