using Swoovr.Client;
using Swoovr.Domain;

/// <summary>
/// Photo attachments in chat, polite endings and starters (#41; policy §5, §14; D203): the recipient sees a photo only
/// once it passed; deleted messages read as deleted to both; closing messages are a fixed set; starters come only from
/// shared chips and never more than three; case links are signed like the others.
/// </summary>
static class ChatAttachmentChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Chat attachment check failed: " + what); checks++; }
        Guid sender = Guid.NewGuid(), recipient = Guid.NewGuid();
        Message Photo(DateTimeOffset? deleted = null) => new() { Id = Guid.NewGuid(), SenderAccountId = sender, Kind = MessageKind.Photo, MediaId = Guid.NewGuid(), DeletedAt = deleted };

        // Who sees a photo message.
        foreach (var status in Enum.GetValues<MediaStatus>())
        {
            Expect(AttachmentPolicy.Listed(Photo(), sender, status, false), $"the sender always sees their photo ({status})");
            Expect(AttachmentPolicy.Listed(Photo(), recipient, status, false) == (status == MediaStatus.Passed), $"the recipient sees it only once passed ({status})");
        }
        Expect(!AttachmentPolicy.Listed(Photo(), recipient, MediaStatus.Passed, true), "not once removed");
        Expect(!AttachmentPolicy.Listed(Photo(), recipient, null, false), "not without its media item");
        Expect(AttachmentPolicy.Listed(Photo(DateTimeOffset.UtcNow), recipient, MediaStatus.Pending, false), "a deleted message shows as deleted to both, whatever its photo");
        Expect(AttachmentPolicy.Listed(new Message { SenderAccountId = sender, Kind = MessageKind.Text }, recipient, null, false), "text is unaffected");
        Expect(AttachmentPolicy.PerDay == 20 && AttachmentPolicy.Purpose == "attachment", "the Q84 default limit and the purpose");
        Expect(MediaPolicy.MemberState(MediaStatus.Blocked) == MediaPolicy.MemberState(MediaStatus.Rejected), "a blocked photo reads like a rejected one to its sender");

        // Signed links: the case variant, and nothing else new.
        var key = new byte[32];
        var now = DateTimeOffset.UtcNow;
        var media = Guid.NewGuid();
        Expect(MediaPolicy.ReadLink(key, MediaPolicy.Link(key, media, "case", now.AddMinutes(10)), now) == (media, "case"), "case links read back");
        Expect(MediaPolicy.ReadLink(key, MediaPolicy.Link(key, media, "evidence", now.AddMinutes(10)), now) is null, "unknown variants are refused");
        var link = MediaPolicy.Link(key, media, "member", now.AddMinutes(10));
        Expect(MediaPolicy.ReadLink(key, link.Replace(".member.", ".case."), now) is null, "a member link can't be turned into a case link");

        // Ending politely.
        Expect(ConversationEnding.Closings["thanks"] == "Thanks for chatting. I don't think we're the right match, but I wish you all the best.", "Q85 first exact farewell");
        Expect(ConversationEnding.Closings["not-a-match"] == "I've enjoyed our conversation, but I've decided not to continue. Take care.", "Q85 second exact farewell");
        Expect(ConversationEnding.Closings["take-care"] == "I'm moving in a different direction, but thank you for your time. Wishing you well.", "Q85 third exact farewell");
        Expect(ConversationEnding.Valid(null) && ConversationEnding.Valid("thanks") && ConversationEnding.Valid("not-a-match") && ConversationEnding.Valid("take-care"), "the three closings or none");
        Expect(!ConversationEnding.Valid("") && !ConversationEnding.Valid("THANKS") && !ConversationEnding.Valid("you are awful"), "no free text");
        Expect(ConversationEnding.Closings.Values.All(t => t.Length <= MessagingPolicy.TextMax && MessagingPolicy.CleanText(t) == t), "closings are valid messages");
        Expect(PhotoText.Closings.Select(c => c.Id).SequenceEqual(ConversationEnding.Closings.Keys) && PhotoText.Closings.All(c => c.Label.Contains(ConversationEnding.Closings[c.Id])),
            "the clients offer exactly the API's closings, word for word");

        Expect(ConversationEnding.SystemText(true) == ConversationEnding.ByMe && ConversationEnding.SystemText(false) == ConversationEnding.ByOther, "participant-specific neutral system text");
        Expect(!MessagingPolicy.MayDeleteForBoth(new Message { Kind = MessageKind.System, SenderAccountId = sender, SentAt = now }, sender, now)
            && !MessagingPolicy.MayDeleteForBoth(new Message { Kind = MessageKind.Farewell, SenderAccountId = sender, SentAt = now }, sender, now), "system and farewell events are preserved");
        foreach (var previews in new[] { true, false })
            Expect(NotificationPolicy.Push(NotificationKind.ConversationEnded, previews, "private name", "Dating", "private farewell", null) == ("Swoovr", ConversationEnding.Notification), "closure pushes never contain private previews");
        Expect(StandingText.SupportUrl == "https://swoovr.com/support" && StandingText.SupportEmail == "support@swoovr.com", "confirmed support channels");
        Expect(StandingText.SupportLink("SW-ABC234") == "https://swoovr.com/support?reference=SW-ABC234", "reference passed to support without report content");

        // Starters.
        Expect(ConversationStarters.For([]).Count == 0, "no shared chips, no starters");
        var four = ConversationStarters.For(["Hiking", "Coffee", "Hiking", "Art", "Music"]);
        Expect(four.Count == 3 && four[0].Contains("“Hiking”") && four[1].Contains("“Coffee”") && four[2].Contains("“Art”"), "three at most, distinct, in order");
        Expect(four.Distinct().Count() == 3, "each starter is worded differently");

        // Wording.
        Expect(PhotoText.ChatState("checking").StartsWith("Checking") && PhotoText.ChatState("not-approved").StartsWith("Not approved"), "chat photo states");
        Expect(PhotoText.Refusal("photos-off") == "This person isn't accepting photos in chat." && PhotoText.Refusal("attachment-limit").Contains("20"), "chat refusals");

        Console.WriteLine($"PASS: chat attachments, endings and starters ({checks} checks).");
    }
}
