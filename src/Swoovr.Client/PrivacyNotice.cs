namespace Swoovr.Client;

/// <summary>
/// The member-facing privacy notice (#99, #22; policy §1–§16, procedure §10), shared by the web page and the app so both say
/// the same thing. It is a DRAFT until legal review (#57): every rendering shows <see cref="DraftLabel"/>. Wording follows
/// the decisions it describes (D167, D170–D173, D186–D191, D195, D199); change them together.
/// </summary>
public static class PrivacyNotice
{
    public const string DraftLabel = "DRAFT — not yet reviewed by our lawyers and not in force. It describes how the Swoovr prototype handles information today.";
    public const string Version = "Draft 0.16, 31 October 2026";
    /// <summary>CONFIRMED (D202, Q80): privacy@swoovr.com; mail to privacy@swoovr.app forwards to it.</summary>
    public const string Contact = "privacy@swoovr.com";

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
            "If an account is permanently terminated, we keep keyed fingerprints of its current verified email address and mobile number in a restricted safety registry. The registry stores no plaintext contact values. Active restrictions can have no automatic expiry; their continued necessity is reviewed, and the proposed longer retention remains subject to legal review.",
            "An active, unexpired email or mobile restriction prevents that identifier from being registered or attached to an account. It does not prove who owns the identifier now, and a refused identifier change does not automatically ban an otherwise legitimate account. Contact Swoovr Support if a number or address was reassigned or a restriction may be wrong. Authorized staff can review and release the identifier independently of the original account ban, with a recorded explanation.",
            "We separately keep a keyed fingerprint of a terminated member’s verified legal name and date of birth for 2 years. A matching identity goes to staff review; that identity match alone does not automatically block an account.",
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
        new("photos", "Your photos", [
            "Photos you upload are kept private until they have been checked. We remove location and other hidden details (EXIF data) and keep a re-made copy, never the original file. Our own screening service checks each photo for nudity and against databases of known child sexual abuse material (through the Canadian Centre for Child Protection's Project Arachnid, which receives only a digital fingerprint of the photo, not the photo itself).",
            "If the checks aren't sure, a trained member of our team may look at the photo, for a recorded reason that is logged. Photos are stored in our private cloud storage (Cloudflare R2) and shown to others only through short-lived links, only in the modes you choose.",
            "When you delete a photo, or your account, we delete it unless a safety-review retention period or legal hold requires us to keep it. If a photo matches known child abuse material, we keep it sealed as evidence, never show it to anyone, and follow the law on reporting it.",
            "ID verification selfies and documents never go through this process; they stay with Didit.",
        ]),
        new("meeting-safety", "Voluntary meeting safety and trusted contacts", [
            "Meeting safety is optional and free. You can save up to three trusted contacts, including people without Swoovr accounts. We keep their contact details encrypted and use them for invitations and safety updates, never marketing. Invitations expire after seven days; contacts must agree before receiving meeting updates and can decline or withdraw.",
            "Incoming contact messages contain your preferred first name, a general status and links, without the meeting time or place. Your selected contacts must verify a short-lived code sent to their confirmed contact details before viewing the read-only meeting summary. It shows your approved, member-provided date, time zone, public venue or general area and check-in status, never the other participant's identity, profile, photos, messages or live location.",
            "We keep hashes of access links, codes and read grants, and minimal consent and notification-attempt records. A protected browser cookie keeps verified read access for at most four hours, within the link's lifetime. This does not sign the contact in as a Swoovr member. Only you can mark yourself OK, home safe, end or cancel the session.",
            "Shared links normally expire at the final expected end plus 24 hours, with a maximum of 30 days from their creation; after home safe, end or cancel, final-status access lasts up to 24 hours unless the link expires or is revoked sooner. Expired links never resume. Ordinary meeting-safety records are scheduled for deletion 30 days after final closure, unless a linked incident, safety review or legal hold requires preservation. Withdrawn or expired contact records have a separate deletion schedule and may remain while required by those records.",
            "Optional after-meeting feedback appears only in your own member features and does not automatically change another person's reputation. It may still be accessed or preserved through lawful restricted safety or legal processes. An incident report can include meeting context without automatically notifying the person reported.",
            "Swoovr does not monitor emergencies or dispatch responders. Missed check-ins are unconfirmed, not proof of an emergency; alerts are not guaranteed to arrive. Calling emergency services opens your device's dialler, and alerting a contact by SMS opens its composer. Opening either does not prove a call or message was sent. An optional location link is explicitly chosen and generated on your device; Swoovr receives no exact coordinates.",
        ]),
        new("text-review", "Text safety review", [
            "When enabled, local automated rules inspect newly submitted profile about text and text messages for limited threat, payment-request, repetitive-message and bulk-outreach signals. These rules can miss harm or flag legitimate conversations and quotations. They do not establish misconduct or automatically restrict an account.",
            "A rule match creates restricted encrypted evidence for authorised human review. Staff decide whether any action is needed; the ordinary account-notice and review pathways apply when a person takes an action. Inspection work is encrypted, and completed processing receipts contain no message text and expire after 30 days unless preservation requires longer. Candidate evidence follows the separate safety-case retention and legal-hold rules.",
        ]),
        new("policy-records", "Policy versions and safety updates", [
            "The proposed terms and community guidelines are drafts and are not in force. Reading them or signing in does not record acceptance. When an approved version is explicitly required, we record your deliberate acceptance with its version, content digest and server time. Ordinary acceptance records are erased during final account deletion; independently preserved evidence follows its own review and legal-hold rules.",
            "Explicitly published safety updates can appear in your notification centre. The ordinary delivery queue and minimal per-member receipt expire after 60 days, unless preservation requires longer, including when the profile has already been deleted. We keep a version disposition without member or operator identifiers to prevent accidental reuse. These notices do not send email or push. Reading educational safety advice does not create a new acknowledgement or consent record.",
        ]),
        new("appearance", "Your appearance preferences", [
            "The browser or app also keeps an account-scoped copy and unsynced choices on your device so appearance can work offline. Signing out clears that account copy and restores the separate guest choice. Conflicting account changes require a choice before replacing either version. Device storage can be unavailable; in that case a choice may last only for the current visit.",
            "We can keep your colour scheme and requested System, Light or Dark display mode with your account, together with a revision, last-change identifier and update time so changes do not silently overwrite one another. This changes only your own interface and is not shown on your profile or to other members.",
            "Appearance is free. We do not use a scheme to infer identity, gender, sexuality or intentions, or to change matching, visibility, permissions or benefits. Account preferences are included in your own data export and erased at final account deletion; required independent encrypted evidence can remain under the separate preservation rules.",
        ]),
        new("beta-access", "Private beta access records", [
            "We keep beta pre-authorisation, activation and revocation records to manage invitation-only access. When enabled, we use your pre-authorised email for an invitation and keep minimal queue, attempt and outcome records; provider acceptance is separate from delivery. Unactivated unlinked records ordinarily expire after 90 days, and revoked records after 90 days from revocation; legal holds or retained evidence may postpone this. Staff may record the status of private iOS distribution work managed outside Swoovr. This is a staff report, not verification from Apple or proof of current access through every other account requirement.",
            "Enrolments linked to your account are included in your own data export and erased from the ordinary account at final deletion. Required independent encrypted evidence can remain under the separate preservation rules. An unlinked or historical access record may need a privacy request to identify and review it safely.",
        ]),
        new("beta-analytics", "Understanding the beta", [
            "When first-party beta observations are enabled, we keep one record per account and UTC day showing authenticated member API activity, whether Explore successfully loaded and whether it returned at least one eligible profile. Activity can include background requests; it does not prove that you read a screen, were online to another member or wanted to contact anyone.",
            "These records contain no filters, mode, intention, location, device or IP information, counterpart identifiers or message content. Authorised administrators can read a bounded internal scorecard using aggregated observations and retained product records, with small numbers suppressed. We do not use it for advertising, member ranking or automatic safety actions, and do not analyse private feedback for it.",
            "Ordinary daily observation records expire after 90 days or with final account deletion. Evidence preservation can retain encrypted copies separately. Missing observations and deleted source records limit what the scorecard can establish; it is not a complete history or a measure of relationship success.",
        ]),
        new("moderation-reporting", "Moderation reporting records", [
            "We keep minimal restricted records of moderation decisions for annual reporting, including the action, its reviewed policy basis and how it was detected and enforced. For account terminations, we record whether your declared home country at the time was Australia, another country or unknown. This is not verified residence. These reporting records contain no message text, contact values or staff free-text explanation.",
            "The draft reporting schedule keeps these linked records for 36 months, with longer preservation where a hold or retained evidence requires it. We also keep anonymous daily processing totals for 36 months and internal annual aggregates afterwards. Aggregates hide small numeric groups, disclose unknown coverage and do not establish classifier accuracy or completed staff training.",
        ]),
        new("moderated-content", "Text removed after moderation", [
            "Authorised staff can remove an individual profile-text field or a reported text message. Account standing records the action; a removed message is shown as removed by Swoovr rather than as a message the sender unsent.",
            "The exact removed text and the review explanation are kept as restricted encrypted case evidence, including after the ordinary source is deleted. The applicable case-content retention, review and legal-hold rules decide when that evidence can be erased; a separate minimal decision record follows its own retention period. The reporter and internal review explanation are not disclosed through the account notice.",
        ]),
        new("holds", "Evidence we must preserve", [
            "Sometimes we must preserve information, for example after a serious safety report or a verified request from police. A legal hold keeps the records it covers in a restricted, encrypted evidence store, even if you delete your account, while the hold applies and afterwards for any applicable safety-review or police-record retention period.",
            "Holds are reviewed regularly and released by someone other than the person who placed them. Preserving information is not the same as disclosing it.",
        ]),
        new("law-enforcement", "Requests from police and other authorities", [
            "We disclose information to authorities only when the law requires or permits it, after verifying the request, and as narrowly as possible. We tell affected members unless the law prohibits it or it would put someone at risk or prejudice an investigation, and we record why and when we will reconsider.",
            "Information for authorities about how to contact us is on our law-enforcement page.",
        ]),
        new("staff", "Who at Swoovr can see your information", [
            "Our support and moderation staff see what they need to handle reports and appeals. Sensitive records (identity records, evidence, contact history) are restricted to named staff, need time-limited access with a recorded reason, and every view, search and export is logged in a record that can't be edited.",
            "Staff with a known personal conflict are excluded from related restricted reviews. Other administrative access follows separate role permissions.",
        ]),
        new("retention", "How long we keep information", [
            "While your account is open, we keep your information for as long as you use Swoovr.",
            "When you ask to delete your account, it is hidden at once. You can cancel within 30 days of the request until final deletion starts (or ask for deletion straight away); then we erase your profile, messages you sent, identity record and the rest of your personal information, and ask Didit to delete your verification sessions.",
            "We normally keep a minimal record that the account existed for 2 years. If the other person keeps their own messages, a minimal reference remains so their history still works; your old sign-in mapping is removed.",
            "Closed police-case records follow a seven-year retention period. Backups are planned to expire on daily 30-day, weekly 4-week and monthly 12-month schedules; production backup and restore readiness is still being prepared.",
            "If your account was terminated, we normally keep its identity record and contact history for 2 years. Active email/mobile restrictions follow the separate review process above. Our draft schedule proposes keeping revoked or expired identifier history for at least 24 months after its latest ending or review, with longer preservation where legally required. This draft schedule still needs legal review.",
            "After identifier fingerprints and their history are lawfully disposed, we may keep a minimal record of the decision while the original account ban or preservation duties continue. That record still links to its case and account; it contains no contact fingerprint and is not used to match an address or number. New staff reviews have their own retention period.",
            "Safety-review evidence is retained for at least 12 months after the latest completed review or referral; our draft serious-incident schedule uses 24 months. Minimal review and compliance records normally remain for 24 months. Open reviews and active or overlapping legal holds postpone disposal. Released copies follow the applicable review or police-record schedule; unknown provenance requires restricted review before disposal.",
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
            $"General support: {StandingText.SupportUrl} or {StandingText.SupportEmail}. Quote your SW-XXXXXX reference if you have one. {StandingText.SupportHelp}",
        ]),
    ];
}
