namespace Swoovr.Client;

/// <summary>DEFAULT D264 draft content for review, never activated terms or a consent record.</summary>
public static class PublicPolicyDrafts
{
    public const string Version = "draft-2026-10-30-0.1";
    public const string Status = "draft";
    public const string Source = "docs/delivery/TERMS-AND-COMMUNITY-GUIDELINES-DRAFT.md";
    public const string SourceSha256 = "afb3dbb4f090f83a37a232abc982318c95e2f341b3d928a0e2ee661944187c2d";
    public const bool IsApproved = false;
    public const string DraftLabel = "DRAFT — proposed text, pending legal review. Not in force. Reading this page does not accept or activate these terms.";
    public sealed record Document(string Id, string Title, IReadOnlyList<string> Paragraphs, IReadOnlyList<string> Rules);
    public static readonly Document Terms = new("terms", "Terms of use", [
        "Swoovr is an adult connection service operated by RePass Cloud Pty Ltd. You must be at least 18 to use it. Give accurate account information, use only accounts you are authorised to control, protect your sign-in details and do not evade a restriction or ban. Tell support if your account may be compromised.",
        "Use Swoovr lawfully and follow the community guidelines below in every mode, profile, message, image and interaction connected with the service. Only share material you have the right and permission to share. Do not upload another person's private information or intimate images without their consent.",
        "You control whether to connect, continue a conversation or meet. Respect another person's refusal, withdrawal of consent or decision to stop contact. Verification badges describe particular checks; they do not guarantee a person's intentions, conduct or safety. Swoovr cannot guarantee a connection, meeting, relationship or protection from harm.",
        "You can report conduct or content and block an account through the available in-app controls. You can report a relevant interaction after blocking or ending a conversation. For help, use Swoovr support (https://swoovr.com/support) or support@swoovr.com and include your reference if one is shown. Intake availability does not mean continuous human monitoring or a promised response time. Swoovr is not an emergency dispatch service; if you face immediate danger, contact your local emergency service directly.",
        "Swoovr may assess reports, preserve relevant evidence and apply proportionate measures such as warnings, content restrictions, suspension or termination. Serious violations may lead to termination. We assess available evidence fairly; a report is an allegation, not proof. Where safe and lawful, we explain relevant account actions and available appeal routes without exposing another person's private information or protected investigations.",
        "You can request account deletion using the account controls. Deletion, unsending and blocking do not necessarily erase evidence already preserved for a lawful safety review or legal hold. The separately reviewed privacy notice and retention schedule must explain what is retained, why and for how long. Swoovr cannot promise recovery of information already lawfully destroyed."
    ], []);
    public static readonly Document Community = new("community", "Community guidelines", [
        "Report concerns using Swoovr's controls; you do not need to confront the person first. Give the information you reasonably have, distinguish facts from uncertainty, and do not knowingly fabricate reports. A report made in good faith is not misconduct merely because evidence is inconclusive."
    ], [
        "No illegal content or conduct, sexual assault, sexual harassment, sexual coercion, exploitation, stalking, threats, violence or encouragement of harm.",
        "No child sexual abuse or exploitation material, sexual content involving minors, grooming, or accounts operated by people under 18. Report suspected underage use; do not investigate it yourself.",
        "No non-consensual intimate images, threats to share them, image-based abuse, sexual extortion, or manipulated intimate material shared without consent.",
        "No unsolicited sexual content. Consent must be freely given, specific and ongoing; a profile preference, match or earlier conversation does not establish consent to another act or image.",
        "No impersonation, deceptive synthetic profile imagery, fraud, scams, coercive requests for money, spam, bulk unsolicited outreach, malware or malicious links.",
        "No discriminatory abuse, targeted harassment, doxxing, retaliation against reporters, or attempts to bypass a block, ban or another person's privacy choices.",
        "No public nudity or explicit sexual profile content, paid sexual services or exploitative solicitation. Do not infer that a private-content feature authorises prohibited material; separately approved feature and recipient-consent rules apply.",
        "Respect privacy across modes. Do not reveal another person's dating activity to their professional contacts or redistribute private messages or images to harass or exploit them."
    ]);
    public static Document? Find(string? id) => id switch { "terms" => Terms, "community" => Community, _ => null };
}
