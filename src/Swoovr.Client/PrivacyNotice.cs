namespace Swoovr.Client;

/// <summary>
/// The member-facing privacy notice (#99, #22; policy §1–§16, procedure §10), shared by the web page and the app so both say
/// the same thing. It is a DRAFT until legal review (#57): every rendering shows <see cref="DraftLabel"/>. Wording follows
/// the decisions it describes (D167, D170–D173, D186–D191, D195, D199); change them together.
/// </summary>
public static class PrivacyNotice
{
    public const string DraftLabel = "DRAFT — not yet reviewed by our lawyers and not in force. It describes how the Swoovr prototype handles information today.";
    public const string Version = "Draft 0.1, 27 October 2026";
    /// <summary>OPEN (Q80): the privacy contact address until the owner confirms it.</summary>
    public const string Contact = "privacy@swoovr.app";

    public sealed record Section(string Id, string Title, IReadOnlyList<string> Paragraphs);

    public static readonly IReadOnlyList<Section> Sections =
    [
        new("who", "Who we are", [
            "Swoovr is run by RePass Cloud Pty Ltd in Australia. This notice explains what we collect, why, who can see it, how long we keep it and the choices you have.",
        ]),
        new("collect", "What we collect", [
            "Your account: your email address, mobile number (verified with a code) and sign-in details, held by our sign-in service.",
            "Your profile: display name, area, home city, date of birth (never shown to others), gender and pronouns if you add them, your connection modes, intentions, filters and abouts.",
            "What you do on Swoovr: connection requests, connections, blocks, messages, reports, appeals, trips and notices.",
            "Location: your home city and trip destinations, which you choose. When you confirm arrival or we check your home area, we read your device location once and keep only the outcome, never the coordinates.",
        ]),
        new("identity", "ID verification and your identity record", [
            "ID verification is optional and done by our verification provider, Didit. Didit keeps the raw evidence (document images, selfie, liveness checks) for about 30 days; we ask Didit to delete your sessions when your account is finally deleted.",
            "If you complete ID verification, we keep a minimum identity record: your legal first and last name as on the document, the document's date of birth, the document type, the issuing country and the expiry date. We encrypt it and keep it separate from your profile.",
            "We never keep your document number, address, document images, selfie, liveness or biometric data.",
            "Only our security and legal officer can open an identity record, for a recorded reason, with time-limited access that is logged. Other staff see only that an identity is on file, when, and the issuing country.",
        ]),
        new("bans", "Keeping banned members off Swoovr", [
            "If an account is terminated for breaking our rules, we keep keyed fingerprints (one-way hashes) of its email address and mobile number, and of the legal name and date of birth on its identity record, for 2 years.",
            "If someone registers or verifies with a matching fingerprint, our team reviews it. A match is never treated as proof on its own and never blocks anyone automatically.",
        ]),
        new("contact-history", "Your previous mobile numbers and email addresses", [
            "When you change your verified mobile number or email address, we keep the previous one, encrypted, for 2 years from the change, so we can look into misuse and enforce bans. We tell the old number or address that it changed, never the new one.",
            "Staff can check which accounts used a number or address, but never see the stored values; every search is logged.",
        ]),
        new("messages", "Messages, including ones you unsend", [
            "Messages are stored so you and the person you're talking to can read them. When you delete a message for both of you, it shows as deleted straight away, but we keep the text for 30 days in case it's needed to investigate a report, then erase it.",
            "If you report a conversation, the messages you choose are kept as evidence with the report.",
        ]),
        new("modes", "Your modes and who sees what", [
            "Each connection mode shows only what belongs to it. Your main about appears in a mode only if you turn on \"Show my main about\" for that mode.",
            "Others see a rounded distance band between home cities (for example \"5–10 km\"), never your exact location, and searches are limited so they can't be used to pinpoint you.",
            "We keep versions of what others could see on your profile, so our team can review a report against what was visible at the time.",
        ]),
        new("holds", "Evidence we must preserve", [
            "Sometimes we must preserve information, for example after a serious safety report or a verified request from police. A legal hold keeps the records it covers in a restricted, encrypted evidence store, even if you delete your account, until the hold is released.",
            "Holds are reviewed regularly and released by someone other than the person who placed them. Preserving information is not the same as disclosing it.",
        ]),
        new("law-enforcement", "Requests from police and other authorities", [
            "We disclose information to authorities only when the law requires or permits it, after verifying the request, and as narrowly as possible. We tell affected members unless the law prohibits it or it would put someone at risk or prejudice an investigation, and we record why and when we will reconsider.",
            "Information for authorities about how to contact us is on our law-enforcement page.",
        ]),
        new("staff", "Who at Swoovr can see your information", [
            "Our support and moderation staff see what they need to handle reports and appeals. Sensitive records (identity records, evidence, contact history) are restricted to named staff, need time-limited access with a recorded reason, and every view, search and export is logged in a record that can't be edited.",
            "Staff with a personal connection to you are kept out of anything about you.",
        ]),
        new("retention", "How long we keep information", [
            "While your account is open, we keep your information for as long as you use Swoovr.",
            "When you ask to delete your account, it is hidden at once. You have 30 days to change your mind (or you can delete straight away); then we erase your profile, messages you sent, identity record and the rest of your personal information, and ask Didit to delete your verification sessions.",
            "We keep a record that the account existed, with moderation records and audit entries that mention it, for 2 years, then erase them.",
            "If your account was terminated, we keep the identity record, contact history and ban fingerprints described above for 2 years.",
            "Anything under a legal hold is kept until the hold is released.",
        ]),
        new("founders", "Founding members' recovery token", [
            "If you hold a founding place, we keep your founder recovery token after your account is deleted so your founding status can be restored if you come back. When you delete your account you can tick \"Also delete my founder recovery token\", or ask support to delete it later.",
        ]),
        new("choices", "Your choices and rights", [
            "You can download a copy of your data from Your data and account, correct your profile at any time, choose what each mode shows, and delete your account.",
            "You can ask us about your information or make a complaint at the address below. If you're not satisfied with our response, you can complain to the Office of the Australian Information Commissioner (oaic.gov.au).",
            "If we have a data breach likely to cause you serious harm, we will tell you and the OAIC.",
        ]),
        new("contact", "Contact", [
            $"Privacy questions and complaints: {Contact}.",
        ]),
    ];
}
