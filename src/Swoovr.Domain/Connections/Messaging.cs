namespace Swoovr.Domain;

/// <summary>
/// One conversation per connected pair, shared across modes (Q42 rule 3, D143). A block closes it (rules 12–14): no more
/// messages and no history through member screens, and unblocking doesn't reopen it; a later reconnection starts a new
/// conversation. Kind leaves room for system conversations later.
/// </summary>
public sealed class Conversation
{
    public Guid Id { get; set; }
    public Guid PairLow { get; set; }
    public Guid PairHigh { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    /// <summary>
    /// Why it closed: "blocked" (hidden from both for good, D143), "disconnected" (no mode connects the pair any more,
    /// D176) or "deleted" (the other account was deleted, D170). The last two stay readable, read-only, for both.
    /// Null for conversations closed before this was recorded (treated as blocked).
    /// </summary>
    public string? ClosedReason { get; set; }
}

/// <summary>Why a conversation closed (Conversation.ClosedReason).</summary>
public static class ClosedReasons
{
    public const string Blocked = "blocked", Disconnected = "disconnected", Deleted = "deleted";
}

/// <summary>What a message carries: text, or one photo through the media quarantine (#41, D203; policy §5).</summary>
public enum MessageKind { Text, Photo }

/// <summary>
/// A message. Deleting for both (within five minutes, rule 8) shows it to both members as deleted at once; its text
/// stays on the server for 30 days for investigations (harassment and the like) and is then erased by `messages purge`
/// (owner, Q59, D167).
/// </summary>
public sealed class Message
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderAccountId { get; set; }
    public MessageKind Kind { get; set; }
    public string Body { get; set; } = "";
    public DateTimeOffset SentAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    /// <summary>A photo message's media item (purpose "attachment"); the recipient sees it only once it passed screening.</summary>
    public Guid? MediaId { get; set; }
}

/// <summary>A message one member hid from their own view (rule 9); the other member still sees it.</summary>
public sealed class MessageHide
{
    public Guid MessageId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset HiddenAt { get; set; }
}

/// <summary>A conversation one member hid from their own list (rule 9): messages up to HiddenAt stay hidden for them; a new one brings it back.</summary>
public sealed class ConversationHide
{
    public Guid ConversationId { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset HiddenAt { get; set; }
}

public enum SendRefusal { None, NotAvailable, BotMayOnlyReply, ArrivalRequired, InvalidText, RateLimited }

/// <summary>What the API knows about one attempt to send.</summary>
/// <param name="Connected">An accepted connection in any mode, with no block either way and the conversation open.</param>
/// <param name="RecipientParticipates">The recipient's account is active and not restricted, suspended or terminated (rule 15, D160).</param>
/// <param name="OtherHasWritten">The other member has sent a message in this conversation.</param>
/// <param name="TravellerBeforeArrival">The sender's only connections with this person came from their own visit and that trip isn't arrived (D60).</param>
public sealed record SendFacts(bool SenderIsBot, bool Connected, bool RecipientParticipates, bool OtherHasWritten, bool TravellerBeforeArrival, int SentLastMinute);

/// <summary>
/// Messaging rules (Q42, D143), checked on every send (rule 16). Text only, 1–2,000 characters after trimming, no
/// control characters except new lines and tabs. An accepted connection is required; bots only reply in a conversation
/// the member started (rule 11); a traveller connected only through a visit may reply but not start before arrival
/// (D60, D149). The sender's own state (restricted, suspended, terminated, age check) is the API's member gate.
/// DEFAULT limits (D165): 30 messages a minute; delete for both within five minutes.
/// </summary>
public static class MessagingPolicy
{
    public const int TextMax = 2000, PerMinute = 30, PageSize = 50, EvidenceBefore = 20;
    public static readonly TimeSpan DeleteWindow = TimeSpan.FromMinutes(5), EvidenceRetention = TimeSpan.FromDays(30);

    public static string? CleanText(string? text)
    {
        var trimmed = text?.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > TextMax) return null;
        return trimmed.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) ? null : trimmed;
    }

    public static SendRefusal CanSend(SendFacts facts, string? text)
    {
        if (!facts.Connected || !facts.RecipientParticipates) return SendRefusal.NotAvailable;
        if (facts.SenderIsBot && !facts.OtherHasWritten) return SendRefusal.BotMayOnlyReply;
        if (facts.TravellerBeforeArrival && !facts.OtherHasWritten) return SendRefusal.ArrivalRequired;
        if (CleanText(text) is null) return SendRefusal.InvalidText;
        return facts.SentLastMinute >= PerMinute ? SendRefusal.RateLimited : SendRefusal.None;
    }

    /// <summary>Rule 8: only the sender, only within five minutes, only once.</summary>
    public static bool MayDeleteForBoth(Message message, Guid me, DateTimeOffset now) =>
        message.SenderAccountId == me && message.DeletedAt is null && now - message.SentAt <= DeleteWindow;

    /// <summary>
    /// Whether a message is listed for a member: not hidden by them, not before they hid the conversation. A message
    /// deleted for both is still listed, as "deleted" with no text (owner, Q59, D167); its text stays on the server.
    /// </summary>
    public static bool Shown(Message message, bool hiddenByMe, DateTimeOffset? conversationHiddenAt) =>
        !hiddenByMe && (conversationHiddenAt is null || message.SentAt > conversationHiddenAt);

    /// <summary>What a member reads of a message: its text, or nothing once it's deleted for both (Q59).</summary>
    public static string VisibleText(Message message) => message.DeletedAt is null ? message.Body : "";

    /// <summary>Rules 18–19: the reported message and up to twenty before it, oldest first.</summary>
    public static IReadOnlyList<Message> Evidence(IEnumerable<Message> conversation, Message reported) =>
        conversation.Where(m => m.ConversationId == reported.ConversationId && (m.SentAt < reported.SentAt || m.SentAt == reported.SentAt && m.Id == reported.Id))
            .OrderByDescending(m => m.SentAt).Take(EvidenceBefore + 1).OrderBy(m => m.SentAt).ToList();

    /// <summary>D167: a deleted message's text is kept for 30 days for investigations, then erased.</summary>
    public static bool Purge(Message message, DateTimeOffset now) => message.DeletedAt is { } at && now - at > EvidenceRetention && message.Body.Length > 0;
}
