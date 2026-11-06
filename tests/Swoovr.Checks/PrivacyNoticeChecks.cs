using Swoovr.Client;

/// <summary>
/// The approved privacy notice (#99, #22; D288/D290/D310): it carries its approval label and covers every disclosure the owner asked for — the identity
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

        Expect(!PrivacyNotice.StatusLabel.Contains("draft", StringComparison.OrdinalIgnoreCase) && PrivacyNotice.StatusLabel.Contains("approved 6 November 2026") && PrivacyNotice.Version == "0.19, 6 November 2026", "0.19 approved 6 November 2026 (issue 208), never draft");
        // Issue 208 / D333–D334: helpdesk collection, self-hosted Zammad, support retention, customer retirement, logs and backups.
        Expect(Text("collect").Contains("Customer support: when you contact us") && Text("collect").Contains("internal case notes"), "support information collected (issue 208 §2)");
        Expect(Text("sharing").Contains("self-hosted customer support system using Zammad") && Text("sharing").Contains("not an independent recipient") && Text("sharing").Contains("email service provider"), "self-hosted Zammad, not a recipient (issue 208 §3)");
        Expect(Text("retention").Contains("General Support tickets, including guest and email-only enquiries, are normally retained for up to 24 months after final closure")
            && Text("retention").Contains("at least 7 years where legally required") && Text("retention").Contains("up to 5 years after closure, legal and law-enforcement cases for up to 7 years")
            && Text("retention").Contains("privacy requests for up to 3 years") && Text("retention").Contains("security incidents for up to 5 years"), "approved helpdesk retention periods (D333)");
        Expect(!all.Contains("30-day General Support") && !Text("retention").Contains("serious-incident schedule uses 24 months"), "no withdrawn 30-day purge, no 24-month serious-incident period");
        Expect(Text("retention").Contains("retire your associated customer support account") && Text("retention").Contains("retired identifier") && !Text("retention").Contains("anonymous"), "customer retirement after final deletion, never called anonymous (issue 208 §5)");
        Expect(Text("retention").Contains("no more than 35 days") && Text("retention").Contains("daily 30-day, weekly 4-week and monthly 12-month") && Text("retention").Contains("up to 90 days"), "helpdesk backups distinguished, logs 90 days (issue 208 §6, §8)");
        Expect(Text("retention").Contains("Trust & Safety cases, including serious incidents, normally follow a five-year retention schedule"), "serious incidents follow the five-year Trust & Safety schedule (issue 208 §7)");
        // Issue 186 (Q130 A): the persistent Safety & Support country is disclosed with its limits.
        var device = string.Join(" ", PrivacyNotice.Sections.Single(s => s.Id == "device").Paragraphs);
        Expect(device.Contains("up to 12 months") && device.Contains("not used to determine your physical location") && device.Contains("personalise advertising") && device.Contains("change your country selection"), "country preference disclosed with its limits");
        Expect(string.Join(" ", PrivacyNotice.Sections.Single(s => s.Id == "collect").Paragraphs).Contains("manually selected Safety & Support country"), "country preference listed under what we collect");
        Expect(PrivacyNotice.Sections.Select(s => s.Id).Distinct().Count() == PrivacyNotice.Sections.Count, "unique section ids");
        Expect(Text("identity").Contains("legal first and last name") && Text("identity").Contains("issuing country") && Text("identity").Contains("expiry date"), "the identity record's fields (D191)");
        Expect(Text("identity").Contains("never keep your document number, address, document images, selfie, liveness or biometric data"), "what's never kept");
        Expect(Text("identity").Contains("30 days"), "Didit's 30 days (D186)");
        Expect(Text("bans").Contains("2 years") && Text("bans").Contains("does not prove who owns") && Text("bans").Contains("identity match alone does not automatically block"), "identity review remains distinct from contact blocking (D191, D217)");
        Expect(Text("bans").Contains("active, unexpired") && Text("bans").Contains("independently of the original account ban") && Text("bans").Contains("subject to legal review"), "restricted contact blocking, correction and legal gate (D217)");
        Expect(Text("contact-history").Contains("2 years") && Text("contact-history").Contains("never the new one"), "contact history (D187, #87)");
        Expect(Text("messages").Contains("30 days"), "unsent messages kept 30 days (D167)");
        Expect(Text("holds").Contains("legal hold") && Text("holds").Contains("even if you delete your account"), "evidence holds (D189, D195)");
        Expect(Text("founders").Contains("founder recovery token") && Text("founders").Contains("Also delete my founder recovery token"), "the founder token (D173, #22)");
        // D231's review exceptions as reworded by issue 208 §7 (owner text): 12 months minimum, holds extend, reassess before disposal.
        Expect(Text("retention").Contains("12 months") && Text("retention").Contains("24 months")
            && Text("retention").Contains("applicable legal holds may extend retention") && Text("retention").Contains("records are reassessed"), "review retention exceptions and hold extension (D231, issue 208)");
        Expect(Text("moderation-reporting").Contains("36 months") && Text("moderation-reporting").Contains("not verified residence"), "reporting receipt purpose and independent retention (D268)");
        Expect(Text("retention").Contains("30 days") && Text("retention").Contains("2 years"), "deletion and retention (D170)");
        Expect(Text("modes").Contains("Show my main about") && Text("modes").Contains("never your exact location"), "mode consent and distance bands (D199)");
        Expect(Text("choices").Contains("oaic.gov.au") && all.Contains(PrivacyNotice.Contact), "complaints and the contact (Q80)");
        Expect(PrivacyNotice.Contact == "privacy@swoovr.com" && !all.Contains("privacy@swoovr.app"), "the confirmed contact (D202)");
        Expect(!all.Contains("24/7"), "no round-the-clock promise");
        // D203: photos — private until checked, EXIF removed, fingerprint-only known-content matching, deletion unless held.
        Expect(Text("photos").Contains("private until they have been checked") && Text("photos").Contains("EXIF") && Text("photos").Contains("fingerprint")
            && Text("photos").Contains("safety-review retention period or legal hold") && Text("photos").Contains("stay with Didit"), "the photos section (D203)");

        // APP 1.4 contents, APP 3.3 sensitive-information consent and APP 1.7–1.9 automated decisions (from 10 December 2026).
        Expect(Text("why").Contains("don't sell") && Text("why").Contains("required"), "purposes and the consequences of not providing information (APP 1.4(c), APP 5.2(f))");
        Expect(Text("sensitive").Contains("sexual orientation") && Text("sensitive").Contains("consent"), "sensitive information collected with consent (APP 3.3, D138)");
        Expect(Text("automated").Contains("under 18") && Text("automated").Contains("safety registry") && Text("automated").Contains("A person, not a program"), "automated decisions disclosed (APP 1.7)");
        Expect(Text("overseas").Contains("United States") && Text("overseas").Contains("Canada"), "likely overseas recipients and countries (APP 1.4(f)–(g))");
        Expect(Text("overseas").Contains("Sydney, Australia") && Text("overseas").Contains("not processed in those regions") && Text("overseas").Contains("European Union"), "Sydney processing with storage-only regional replicas");
        Expect(Text("who").Contains("ABN 74 642 243 801") && Text("international").Contains("directly to us in Australia") && Text("international").Contains("standard contractual clauses") && Text("international").Contains("California"), "operator particulars and GDPR/CCPA rights");
        Expect(Text("international").Contains("45 days") && Text("international").Contains("have not sold or shared") && Text("international").Contains("appeal")
            && Text("international").Contains("Biometric Information Privacy Act"), "CCPA disclosures, US state appeals and biometric retention policy");
        Expect(Text("choices").Contains("access") && Text("choices").Contains("correct") && Text("choices").Contains("30 days"), "access, correction and complaint handling (APP 1.4(d)–(e))");
        Expect(Text("sharing").Contains("Didit") && Text("sharing").Contains("Twilio") && Text("sharing").Contains("MailerSend"), "service providers named");
        Console.WriteLine($"PASS: approved privacy notice 0.19 ({checks}).");
    }
}
