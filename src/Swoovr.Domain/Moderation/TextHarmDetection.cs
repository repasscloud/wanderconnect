using System.Text;

namespace Swoovr.Domain;

public enum TextHarmSource { ProfileAbout, ModeAbout, Message }
/// <summary>Actual server history only, excluding the current write. Missing history must not imply zero.</summary>
public sealed record TextHarmHistory(bool Complete, int SameTextMessagesLastHour, int DistinctRecipientsLastHour);
public sealed record TextHarmInput(Guid AccountId, TextHarmSource Source, string Text, TextHarmHistory History);
public sealed record TextHarmSignal(string Rule, string Category);
public sealed record TextHarmAssessment(string Detector, string Version, IReadOnlyList<TextHarmSignal> Signals);

/// <summary>Local classification hook. Output is a human-review candidate, never proof or an enforcement decision.</summary>
public interface ITextHarmDetector
{
    ValueTask<TextHarmAssessment> DetectAsync(TextHarmInput input, CancellationToken cancellationToken);
}

/// <summary>Reversible DEFAULT limits, not measured accuracy or an exhaustive abuse classifier.</summary>
public sealed record TextHarmRules(bool Enabled = false, int RepeatedMessages = 5, int BulkRecipients = 10)
{
    public bool Valid => RepeatedMessages is >= 3 and <= 100 && BulkRecipients is >= 5 and <= 100;
}

public sealed class BoundedTextHarmDetector(TextHarmRules rules) : ITextHarmDetector
{
    public const string Detector = "local-bounded-rules";
    public const string Version = "swoovr-text-harm-2026-10-30-v1";
    public static string Normalise(string text)
    {
        // Product limits remain enforced at the write boundary; no truncation silently skips a suffix here.
        if (text.Length > 4000) throw new ArgumentOutOfRangeException(nameof(text));
        var result = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Normalize(NormalizationForm.FormKC))
        {
            if (char.IsWhiteSpace(c)) { if (result.Length > 0) space = true; continue; }
            if (space) { result.Append(' '); space = false; }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
    public ValueTask<TextHarmAssessment> DetectAsync(TextHarmInput input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!rules.Valid) throw new InvalidOperationException("Invalid text-harm thresholds.");
        var signals = new List<TextHarmSignal>();
        if (!rules.Enabled) return ValueTask.FromResult(new TextHarmAssessment(Detector, Version, signals));
        var text = Normalise(input.Text);
        // Narrow combinations reduce trivial single-word matches; legitimate text may still match and needs review.
        if ((text.Contains("send me ", StringComparison.Ordinal) || text.Contains("transfer me ", StringComparison.Ordinal))
            && (text.Contains("money", StringComparison.Ordinal) || text.Contains("gift card", StringComparison.Ordinal)
                || text.Contains("crypto", StringComparison.Ordinal)))
            signals.Add(new("payment-request-combination", "scams"));
        // Deliberately narrow direct phrases. Quotes, jokes and reclaimed language require human context.
        if (text.Contains("i will kill you", StringComparison.Ordinal) || text.Contains("i'll kill you", StringComparison.Ordinal))
            signals.Add(new("direct-threat-phrase", "threats"));
        if (input.Source == TextHarmSource.Message && input.History.Complete)
        {
            if (text.Length >= 20 && input.History.SameTextMessagesLastHour + 1 >= rules.RepeatedMessages)
                signals.Add(new("repetitive-message-threshold", "spam"));
            if (input.History.DistinctRecipientsLastHour >= rules.BulkRecipients)
                signals.Add(new("bulk-recipient-threshold", "spam"));
        }
        return ValueTask.FromResult(new TextHarmAssessment(Detector, Version, signals));
    }
}

/// <summary>Durable restricted evidence, separate from member Report: no fabricated reporter account.</summary>
public sealed class TextHarmReview
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public Guid IntentId { get; set; }
    public Guid[] RelatedAccountIds { get; set; } = [];
    public string? ModeId { get; set; }
    public Guid SubjectAccountId { get; set; }
    public TextHarmSource Source { get; set; }
    public Guid SourceId { get; set; }
    public string SourceRevision { get; set; } = "";
    public string DetectorVersion { get; set; } = "";
    public string[] Rules { get; set; } = [];
    public string[] Categories { get; set; } = [];
    public byte[]? SealedEvidence { get; set; }
    public DateTimeOffset? EvidenceDisposedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? Outcome { get; set; } // reviewed-no-action, actioned; never an automatic verdict
}

/// <summary>Encrypted inspection intent committed atomically with the exact source revision.</summary>
public sealed class TextHarmIntent
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public TextHarmSource Source { get; set; }
    public Guid SourceId { get; set; }
    public string? ModeId { get; set; }
    public Guid Revision { get; set; }
    public byte[]? SealedText { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string? Result { get; set; }
}
