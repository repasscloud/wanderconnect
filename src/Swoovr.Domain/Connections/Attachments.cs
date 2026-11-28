namespace Swoovr.Domain;

/// <summary>
/// Photo attachments in chat (#41; policy §5, §14; D203). Only between connected members, through the same quarantine and
/// screening as profile photos; the recipient sees one only once it passed, the sender sees its state. Uncertain ones go
/// to the restricted admin review queue; a known-content match is a Serious Safety Incident (sealed, held, escalated
/// with ids only, never viewable). A recipient can turn photos in chat off (Q84 default on).
/// D423/D428 (#546, supersede D178 and D389 rule 1): an explicit photo reaches only an eligible recipient
/// (<see cref="ExplicitChatPolicy"/>); otherwise it never reaches them at all and only the sender is told.
/// </summary>
public static class AttachmentPolicy
{
    public const string Purpose = "attachment";
    /// <summary>DEFAULT (Q84): photos a member may send in a day, across all conversations.</summary>
    public const int PerDay = 20;
    public static readonly TimeSpan Day = TimeSpan.FromDays(1);

    /// <summary>
    /// Issue 543 (D456): a library photo the member may send in a chat: their own approved profile-library photo, live, not
    /// uploaded into an album, and standard (an explicit or albums-only photo is shared from its album, #431).
    /// </summary>
    public static bool FromLibrary(MediaItem item) =>
        item is { Purpose: null, AlbumId: null, AlbumOnly: false, ContentRating: ContentRating.Standard, Status: MediaStatus.Passed, RemovedAt: null, ObjectDeletedAt: null };

    /// <summary>Whether the recipient may see a photo message: it passed and wasn't removed (a deleted message shows as deleted to both).</summary>
    public static bool RecipientSees(MediaStatus status, bool removed) => status == MediaStatus.Passed && !removed;

    /// <summary>A photo message is listed for the sender always, and for the recipient once passed, or as deleted. An explicit
    /// photo refused at delivery (D423) is never listed for the recipient.</summary>
    public static bool Listed(Message message, Guid viewer, MediaStatus? status, bool removed) =>
        message.Kind != MessageKind.Photo || message.SenderAccountId == viewer
        || message.ExplicitRefusedAt is null && (message.DeletedAt is not null || status is { } s && RecipientSees(s, removed));
}

/// <summary>
/// D423 and D428 (#546, owner 26 November 2026): explicit photos sent straight to chat. Delivered only when, now, the sender
/// is ID Verified (D389) and the recipient is ID Verified with Receive explicit media on for at least one mode the pair is
/// connected in (D428). Checked when the photo would be delivered and again on every view (182.4). Screening is never
/// skipped: malware, known-content matching and the nudity check run first; only routine manual nudity review is.
/// </summary>
public static class ExplicitChatPolicy
{
    /// <summary>The automatic check's nudity flag (MediaScreeningPolicy); for an eligible pair it rates the photo explicit (D428).</summary>
    public const string NuditySignal = "nudity-signal";
    /// <summary>The sender's state when an explicit photo couldn't be delivered; neutral, never why (D423).</summary>
    public const string NotAccepted = "not-accepted";

    public static bool Allowed(bool senderIdVerified, bool recipientIdVerified, bool recipientReceivesInConnectedMode) =>
        senderIdVerified && recipientIdVerified && recipientReceivesInConnectedMode;
}

/// <summary>
/// Ending a conversation politely without blocking (#41, Q85 default): an optional closing message from a fixed, kind
/// set, then every mode connecting the pair is disconnected (D176) and the conversation closes read-only for both.
/// </summary>
public static class ConversationEnding
{
    public static readonly IReadOnlyDictionary<string, string> Closings = new Dictionary<string, string>
    {
        ["thanks"] = "Thanks for chatting. I don't think we're the right match, but I wish you all the best.",
        ["not-a-match"] = "I've enjoyed our conversation, but I've decided not to continue. Take care.",
        ["take-care"] = "I'm moving in a different direction, but thank you for your time. Wishing you well.",
    };

    public const string ByMe = "You've ended this conversation. Your connections with this member have been closed, and neither of you can send further messages here.";
    public const string ByOther = "This conversation has ended. You can still view your previous messages, but replies are no longer available.";
    public const string Notification = "A conversation has ended on Swoovr.";
    public static string SystemText(bool initiatedByViewer) => initiatedByViewer ? ByMe : ByOther;

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
