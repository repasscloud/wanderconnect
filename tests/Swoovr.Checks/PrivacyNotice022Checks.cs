/// <summary>
/// Privacy Notice 0.22 (D453), effective 28 November 2026: both paragraphs the owner approved on issue 609 must appear word
/// for word, in their sections: Photo Verified in "Your photos" (#530, D427) and sharing a location in chat in the messages
/// section (#544, D431).
/// </summary>
static class PrivacyNotice022Checks
{
    public static readonly (string Section, string Text)[] Approved =
    [
        ("photos", "If you choose to get Photo Verified, you send us one photo of yourself holding a piece of paper with a code and the date we give you. Authorised Swoovr staff compare it with your public profile photos to decide whether to award the Photo Verified badge. It isn't an identity or age check. We keep that photo privately and delete it 30 days after the decision, unless a legal hold or safety investigation requires us to keep it longer. We keep a record of the decision (who decided, when, the outcome and which profile photos were compared). Photo Verified lasts 12 months, and ends sooner if you change a verified profile photo."),
        ("messages", "If you choose to share a location in a chat, the other member sees the exact place you picked, as a place name or coordinates they can open in their own maps app. We don't show a third-party map preview in the chat. A shared location is kept and removed like any other message."),
    ];

    public static void Run()
    {
        var missing = Approved.Where(a => !Swoovr.Client.PrivacyNotice.Sections.Single(s => s.Id == a.Section).Paragraphs.Contains(a.Text)).ToList();
        if (missing.Count > 0) throw new Exception("Privacy notice 0.22 wording not word for word: " + missing[0].Text);
        if (Swoovr.Client.PrivacyNotice.Version != "0.22, 28 November 2026") throw new Exception("Privacy notice 0.22: version");
        Console.WriteLine($"PASS: privacy notice 0.22 wording ({Approved.Length} approved paragraphs, word for word).");
    }
}
