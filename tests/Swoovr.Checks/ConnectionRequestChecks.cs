using Swoovr.Domain;

/// <summary>D112 request and block rules: who may send, who may answer, the limits, and blocks both ways.</summary>
static class ConnectionRequestChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Connection request check failed: " + what); checks++; }

        var now = DateTimeOffset.Parse("2026-10-24T00:00:00Z");
        var ok = new RequestFacts(SenderIsBot: false, SameAccount: false, Separated: false, RecipientShownToSender: true,
            Connected: false, Pending: false, DeclinedAt: null, SentInWindow: 0);
        Expect(ConnectionRequestPolicy.CanSend(ok, now) == RequestRefusal.None, "a member to someone shown to them");
        Expect(ConnectionRequestPolicy.CanSend(ok with { SenderIsBot = true }, now) == RequestRefusal.SenderIsBot, "a bot never initiates (Q11 rule 50)");
        Expect(ConnectionRequestPolicy.CanSend(ok with { SenderIsBot = true, RecipientShownToSender = false }, now) == RequestRefusal.SenderIsBot, "the bot rule comes first");
        foreach (var (what, facts) in new (string, RequestFacts)[] { ("self", ok with { SameAccount = true }), ("blocked either way", ok with { Separated = true }),
            ("not shown in this mode (off, hidden, unknown)", ok with { RecipientShownToSender = false }) })
            Expect(ConnectionRequestPolicy.CanSend(facts, now) == RequestRefusal.NotAvailable, what + " reads as not available");
        Expect(ConnectionRequestPolicy.CanSend(ok with { Separated = true, Connected = true, Pending = true }, now) == RequestRefusal.NotAvailable,
            "a block hides that they were connected or had a request");
        Expect(ConnectionRequestPolicy.CanSend(ok with { Connected = true }, now) == RequestRefusal.AlreadyConnected, "already connected");
        Expect(ConnectionRequestPolicy.CanSend(ok with { Pending = true }, now) == RequestRefusal.AlreadyPending, "one pending per pair and mode");
        Expect(ConnectionRequestPolicy.CanSend(ok with { DeclinedAt = now.AddDays(-29) }, now) == RequestRefusal.RecentlyDeclined, "declined 29 days ago");
        Expect(ConnectionRequestPolicy.CanSend(ok with { DeclinedAt = now.AddDays(-30) }, now) == RequestRefusal.None, "declined 30 days ago may ask again");
        Expect(ConnectionRequestPolicy.CanSend(ok with { SentInWindow = 19 }, now) == RequestRefusal.None, "the 20th request of the day");
        Expect(ConnectionRequestPolicy.CanSend(ok with { SentInWindow = 20 }, now) == RequestRefusal.RateLimited, "the 21st is rate limited");

        var (a, b, c) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Expect(ConnectionRequest.Pair(a, b) == ConnectionRequest.Pair(b, a), "the pair is the same whichever way the request goes");
        var request = new ConnectionRequest { FromAccountId = a, ToAccountId = b, Status = ConnectionRequestStatus.Pending };
        Expect(ConnectionRequestPolicy.MayAnswer(request, b, ConnectionRequestPolicy.Answer.Accept), "the recipient accepts");
        Expect(ConnectionRequestPolicy.MayAnswer(request, b, ConnectionRequestPolicy.Answer.Decline), "the recipient declines");
        Expect(!ConnectionRequestPolicy.MayAnswer(request, a, ConnectionRequestPolicy.Answer.Accept), "the sender can't accept their own request");
        Expect(ConnectionRequestPolicy.MayAnswer(request, a, ConnectionRequestPolicy.Answer.Withdraw), "the sender withdraws");
        Expect(!ConnectionRequestPolicy.MayAnswer(request, b, ConnectionRequestPolicy.Answer.Withdraw), "the recipient can't withdraw");
        foreach (var answer in Enum.GetValues<ConnectionRequestPolicy.Answer>())
            Expect(!ConnectionRequestPolicy.MayAnswer(request, c, answer), $"a third member can't {answer}");
        Expect(ConnectionRequestPolicy.Outcome(ConnectionRequestPolicy.Answer.Accept) == ConnectionRequestStatus.Accepted
            && ConnectionRequestPolicy.Outcome(ConnectionRequestPolicy.Answer.Decline) == ConnectionRequestStatus.Declined
            && ConnectionRequestPolicy.Outcome(ConnectionRequestPolicy.Answer.Withdraw) == ConnectionRequestStatus.Withdrawn, "outcomes");

        Block[] blocks = [new() { BlockerAccountId = a, BlockedAccountId = b }];
        Expect(BlockPolicy.Separated(blocks, a, b) && BlockPolicy.Separated(blocks, b, a), "a block works both ways");
        Expect(!BlockPolicy.Separated(blocks, a, c) && !BlockPolicy.Separated(blocks, c, b), "and only between those two");

        Console.WriteLine($"PASS: {checks} connection request cases (bots never initiate, one pending per pair and mode, 30-day wait after a decline, 20 a day, only the recipient answers and only the sender withdraws, blocks both ways and indistinguishable from not available).");
    }
}
