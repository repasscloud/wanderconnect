using Swoovr.Client;

/// <summary>
/// The privacy notice draft (#99, #22): it is labelled DRAFT and covers every disclosure the owner asked for — the identity
/// record and what's never kept, ban matching, 2 years after termination, contact history, unsent-message retention,
/// evidence holds and the founder token — with numbers that match the decisions they describe.
/// </summary>
static class PrivacyNoticeChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Privacy notice check failed: " + what); checks++; }
        string Text(string id) => string.Join(" ", PrivacyNotice.Sections.Single(s => s.Id == id).Paragraphs);
        var all = string.Join(" ", PrivacyNotice.Sections.SelectMany(s => s.Paragraphs));

        Expect(PrivacyNotice.DraftLabel.StartsWith("DRAFT") && PrivacyNotice.DraftLabel.Contains("not in force"), "labelled DRAFT, not in force");
        Expect(PrivacyNotice.Sections.Select(s => s.Id).Distinct().Count() == PrivacyNotice.Sections.Count, "unique section ids");
        Expect(Text("identity").Contains("legal first and last name") && Text("identity").Contains("issuing country") && Text("identity").Contains("expiry date"), "the identity record's fields (D191)");
        Expect(Text("identity").Contains("never keep your document number, address, document images, selfie, liveness or biometric data"), "what's never kept");
        Expect(Text("identity").Contains("30 days"), "Didit's 30 days (D186)");
        Expect(Text("bans").Contains("2 years") && Text("bans").Contains("never treated as proof"), "ban matching, reviewed, never proof (D171, D191)");
        Expect(Text("contact-history").Contains("2 years") && Text("contact-history").Contains("never the new one"), "contact history (D187, #87)");
        Expect(Text("messages").Contains("30 days"), "unsent messages kept 30 days (D167)");
        Expect(Text("holds").Contains("legal hold") && Text("holds").Contains("even if you delete your account"), "evidence holds (D189, D195)");
        Expect(Text("founders").Contains("founder recovery token") && Text("founders").Contains("Also delete my founder recovery token"), "the founder token (D173, #22)");
        Expect(Text("retention").Contains("30 days") && Text("retention").Contains("2 years"), "deletion and retention (D170)");
        Expect(Text("modes").Contains("Show my main about") && Text("modes").Contains("never your exact location"), "mode consent and distance bands (D199)");
        Expect(Text("choices").Contains("oaic.gov.au") && all.Contains(PrivacyNotice.Contact), "complaints and the contact (Q80)");
        Expect(!all.Contains("24/7"), "no round-the-clock promise");

        Console.WriteLine($"PASS: privacy notice draft ({checks}).");
    }
}
