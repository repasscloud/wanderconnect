namespace Swoovr.Domain;

/// <summary>
/// Photo attachments in chat (#41; policy §5, §14; D203). Only between connected members, through the same quarantine and
/// screening as profile photos; the recipient sees one only once it passed, the sender sees its state. Uncertain ones go
/// to the restricted admin review queue; a known-content match is a Serious Safety Incident (sealed, held, escalated
/// with ids only, never viewable). A recipient can turn photos in chat off (Q84 default on). No explicit content (D178).
/// </summary>
public static class AttachmentPolicy
{
    public const string Purpose = "attachment";
    /// <summary>DEFAULT (Q84): photos a member may send in a day, across all conversations.</summary>
    public const int PerDay = 20;
    public static readonly TimeSpan Day = TimeSpan.FromDays(1);

    /// <summary>Whether the recipient may see a photo message: it passed and wasn't removed (a deleted message shows as deleted to both).</summary>
    public static bool RecipientSees(MediaStatus status, bool removed) => status == MediaStatus.Passed && !removed;

    /// <summary>A photo message is listed for the sender always, and for the recipient once passed, or as deleted.</summary>
    public static bool Listed(Message message, Guid viewer, MediaStatus? status, bool removed) =>
        message.Kind != MessageKind.Photo || message.SenderAccountId == viewer || message.DeletedAt is not null || status is { } s && RecipientSees(s, removed);
}

/// <summary>
/// Ending a conversation politely without blocking (#41, Q85 default): an optional closing message from a fixed, kind
/// set, then every mode connecting the pair is disconnected (D176) and the conversation closes read-only for both.
/// </summary>
public static class ConversationEnding
{
    public static readonly IReadOnlyDictionary<string, string> Closings = new Dictionary<string, string>
    {
        ["thanks"] = "Thanks for chatting. I'm going to end our conversation here. All the best.",
        ["not-a-match"] = "I don't think we're a match, so I'm going to end our conversation here. Take care.",
        ["take-care"] = "I'm stepping back from this conversation. Take care of yourself.",
    };

    public static bool Valid(string? closing) => closing is null || Closings.ContainsKey(closing);
}

/// <summary>
/// Conversation starters from intention chips both members picked in a mode they're connected in (#41): never chips
/// from another mode (D175), never more than three, worded so neither person has to explain more than they chose.
/// </summary>
public static class ConversationStarters
{
    public const int Max = 3;
    private static readonly string[] Templates =
    [
        "You both picked “{0}”. What does that look like for you?",
        "“{0}” is on both your profiles. What got you interested?",
        "You share “{0}”. Any plans along those lines coming up?",
    ];

    public static IReadOnlyList<string> For(IEnumerable<string> sharedLabels) =>
        sharedLabels.Distinct().Take(Max).Select((label, i) => string.Format(Templates[i % Templates.Length], label)).ToList();
}
