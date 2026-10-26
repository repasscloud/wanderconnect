namespace Swoovr.Domain;

/// <summary>Ended: an accepted connection a block ended (Q42 rule 14, D143); unblocking doesn't bring it back.</summary>
public enum ConnectionRequestStatus { Pending, Accepted, Declined, Withdrawn, Cancelled, Ended }

/// <summary>
/// A request from one member to another in one connection mode (D112, Q17 rules 29–31). Nothing connects on its own:
/// only the recipient's accept makes a connection. PairLow/PairHigh hold the two account ids in order so the database
/// can keep at most one pending request per pair and mode, whichever way it was sent.
/// </summary>
public sealed class ConnectionRequest
{
    public Guid Id { get; set; }
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public Guid PairLow { get; set; }
    public Guid PairHigh { get; set; }
    public string ModeId { get; set; } = "";
    public ConnectionRequestStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AnsweredAt { get; set; }
    /// <summary>The traveller's trip when a local sent this from the Travel feed (slice M); null from Explore.</summary>
    public Guid? TripId { get; set; }

    public static (Guid Low, Guid High) Pair(Guid a, Guid b) => a.CompareTo(b) <= 0 ? (a, b) : (b, a);
}

/// <summary>
/// One member blocking another (D112). A block works both ways: neither sees the other anywhere (Explore, profiles,
/// requests, Travel), and every answer about the other reads as not found, never "blocked".
/// </summary>
public sealed class Block
{
    public Guid BlockerAccountId { get; set; }
    public Guid BlockedAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public static class BlockPolicy
{
    /// <summary>True when either person blocked the other.</summary>
    public static bool Separated(IEnumerable<Block> blocks, Guid a, Guid b) =>
        blocks.Any(x => x.BlockerAccountId == a && x.BlockedAccountId == b || x.BlockerAccountId == b && x.BlockedAccountId == a);
}

public enum RequestRefusal { None, SenderIsBot, NotAvailable, AlreadyConnected, AlreadyPending, RecentlyDeclined, RateLimited }

/// <summary>Trusted facts about a would-be request, gathered by the API from application records.</summary>
public sealed record RequestFacts(
    bool SenderIsBot,
    bool SameAccount,
    bool Separated,
    bool RecipientShownToSender,
    bool Connected,
    bool Pending,
    DateTimeOffset? DeclinedAt,
    int SentInWindow);

/// <summary>
/// Who may send a connection request (D112, D117). DEFAULT limits: 20 requests per sender in a rolling 24 hours, and
/// after a decline the same sender waits 30 days before asking again in that mode. A refusal for self, a block or an
/// unseen person is the same NotAvailable, so the answer never says why.
/// </summary>
public static class ConnectionRequestPolicy
{
    public const int PerDay = 20;
    public static readonly TimeSpan Window = TimeSpan.FromHours(24);
    public static readonly TimeSpan AfterDecline = TimeSpan.FromDays(30);

    public static RequestRefusal CanSend(RequestFacts facts, DateTimeOffset now)
    {
        // Q11 rules 50–52: official bots never initiate.
        if (facts.SenderIsBot) return RequestRefusal.SenderIsBot;
        if (facts.SameAccount || facts.Separated || !facts.RecipientShownToSender) return RequestRefusal.NotAvailable;
        if (facts.Connected) return RequestRefusal.AlreadyConnected;
        if (facts.Pending) return RequestRefusal.AlreadyPending;
        if (facts.DeclinedAt is { } declined && now - declined < AfterDecline) return RequestRefusal.RecentlyDeclined;
        return facts.SentInWindow >= PerDay ? RequestRefusal.RateLimited : RequestRefusal.None;
    }

    public enum Answer { Accept, Decline, Withdraw }

    /// <summary>
    /// The recipient accepts or declines a pending request; the sender withdraws it. Anyone else, or any request that is
    /// no longer pending, gets nothing (the API answers 404 for another member's ids, 409 for a settled request).
    /// </summary>
    public static bool MayAnswer(ConnectionRequest request, Guid caller, Answer answer) =>
        answer == Answer.Withdraw ? request.FromAccountId == caller : request.ToAccountId == caller;

    public static ConnectionRequestStatus Outcome(Answer answer) => answer switch
    {
        Answer.Accept => ConnectionRequestStatus.Accepted,
        Answer.Decline => ConnectionRequestStatus.Declined,
        _ => ConnectionRequestStatus.Withdrawn,
    };
}
