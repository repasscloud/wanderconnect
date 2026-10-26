using Swoovr.Domain;

/// <summary>
/// Messaging rules (Q42, D143, D165): who may send, text limits, bots only replying, travellers before arrival only
/// replying, the five-minute delete for both, hiding for oneself, report evidence (the message and up to twenty before
/// it) and the 30-day purge of deleted text.
/// </summary>
static class MessagingChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Messaging check failed: " + what); checks++; }
        var now = DateTimeOffset.Parse("2026-10-26T04:00:00Z");
        SendFacts F(bool bot = false, bool connected = true, bool participates = true, bool otherWrote = false, bool traveller = false, int sent = 0) =>
            new(bot, connected, participates, otherWrote, traveller, sent);

        // Text (rule 2): 1–2,000 characters after trimming; new lines and tabs allowed, other control characters not.
        Expect(MessagingPolicy.CleanText("  hello  ") == "hello", "trimmed");
        Expect(MessagingPolicy.CleanText(null) is null && MessagingPolicy.CleanText("   ") is null && MessagingPolicy.CleanText("") is null, "empty");
        Expect(MessagingPolicy.CleanText(new string('a', 2000)) is not null && MessagingPolicy.CleanText(new string('a', 2001)) is null, "2,000 characters at most");
        Expect(MessagingPolicy.CleanText("line one\nline two\ttab") is not null, "new lines and tabs");
        Expect(MessagingPolicy.CleanText("bell\u0007") is null && MessagingPolicy.CleanText("nul\u0000") is null, "other control characters");

        // Who may send (rules 4, 11, 15, 16; D60).
        Expect(MessagingPolicy.CanSend(F(), "hi") == SendRefusal.None, "connected members");
        Expect(MessagingPolicy.CanSend(F(connected: false), "hi") == SendRefusal.NotAvailable, "not connected (or blocked, or closed)");
        Expect(MessagingPolicy.CanSend(F(participates: false), "hi") == SendRefusal.NotAvailable, "the other member is restricted, suspended or terminated");
        Expect(MessagingPolicy.CanSend(F(bot: true), "hi") == SendRefusal.BotMayOnlyReply, "a bot never writes first");
        Expect(MessagingPolicy.CanSend(F(bot: true, otherWrote: true), "hi") == SendRefusal.None, "a bot replies in a member-started conversation");
        Expect(MessagingPolicy.CanSend(F(traveller: true), "hi") == SendRefusal.ArrivalRequired, "a traveller before arrival doesn't write first");
        Expect(MessagingPolicy.CanSend(F(traveller: true, otherWrote: true), "hi") == SendRefusal.None, "but may reply (D60)");
        Expect(MessagingPolicy.CanSend(F(), " ") == SendRefusal.InvalidText && MessagingPolicy.CanSend(F(connected: false), " ") == SendRefusal.NotAvailable, "availability is checked before text");
        Expect(MessagingPolicy.CanSend(F(sent: 29), "hi") == SendRefusal.None && MessagingPolicy.CanSend(F(sent: 30), "hi") == SendRefusal.RateLimited, "30 a minute");

        // Delete for both (rule 8): the sender, within five minutes, once.
        var me = Guid.NewGuid(); var other = Guid.NewGuid(); var conversation = Guid.NewGuid();
        Message M(int minutesAgo, Guid? sender = null, int secondOffset = 0) => new() { Id = Guid.NewGuid(), ConversationId = conversation, SenderAccountId = sender ?? me,
            Body = "text", SentAt = now.AddMinutes(-minutesAgo).AddSeconds(secondOffset) };
        Expect(MessagingPolicy.MayDeleteForBoth(M(5), me, now) && !MessagingPolicy.MayDeleteForBoth(M(5, secondOffset: -1), me, now), "five minutes");
        Expect(!MessagingPolicy.MayDeleteForBoth(M(1, other), me, now), "not someone else's message");
        var deleted = M(1); deleted.DeletedAt = now;
        Expect(!MessagingPolicy.MayDeleteForBoth(deleted, me, now), "only once");

        // What a member sees (rule 9): not deleted, not hidden by them, not before they hid the conversation.
        Expect(MessagingPolicy.Shown(M(1), false, null) && !MessagingPolicy.Shown(deleted, false, null), "deleted for both");
        Expect(!MessagingPolicy.Shown(M(1), true, null), "hidden for me");
        Expect(!MessagingPolicy.Shown(M(10), false, now.AddMinutes(-5)) && MessagingPolicy.Shown(M(1), false, now.AddMinutes(-5)), "a hidden conversation shows only newer messages");

        // Evidence (rules 18–19): the reported message and up to twenty before it, oldest first, never later ones.
        var thread = Enumerable.Range(0, 30).Select(i => M(30 - i, i % 2 == 0 ? me : other)).ToList();
        var reported = thread[25];
        var evidence = MessagingPolicy.Evidence(thread, reported);
        Expect(evidence.Count == 21 && evidence[^1].Id == reported.Id && evidence[0].Id == thread[5].Id, "twenty before and the message");
        Expect(evidence.Zip(evidence.Skip(1)).All(p => p.First.SentAt <= p.Second.SentAt), "oldest first");
        Expect(!evidence.Any(m => m.SentAt > reported.SentAt), "nothing after it");
        Expect(MessagingPolicy.Evidence(thread, thread[3]).Count == 4, "fewer when the conversation is short");
        Expect(!MessagingPolicy.Evidence(thread.Append(new Message { Id = Guid.NewGuid(), ConversationId = Guid.NewGuid(), SentAt = reported.SentAt.AddSeconds(-1) }), reported)
            .Any(m => m.ConversationId != conversation), "only the same conversation");

        // D165: deleted text kept for 30 days as evidence, then purged.
        var old = M(1); old.DeletedAt = now.AddDays(-31);
        var recent = M(1); recent.DeletedAt = now.AddDays(-29);
        Expect(MessagingPolicy.Purge(old, now) && !MessagingPolicy.Purge(recent, now) && !MessagingPolicy.Purge(M(1), now), "purge after 30 days, only deleted ones");
        old.Body = "";
        Expect(!MessagingPolicy.Purge(old, now), "nothing left to purge");

        // A block ends connections (rule 14): Ended is its own status.
        Expect(Enum.IsDefined(ConnectionRequestStatus.Ended) && ConnectionRequestStatus.Ended != ConnectionRequestStatus.Accepted, "an ended connection isn't accepted");
        Expect(ReportPolicy.Supported(ReportSubject.Message) && ReportPolicy.Supported(ReportSubject.Conversation) && !ReportPolicy.Supported(ReportSubject.Photo), "messages and conversations can be reported");

        Console.WriteLine($"PASS: messaging rules ({checks}).");
    }
}
