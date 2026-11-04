using Swoovr.Contracts;
using Swoovr.Domain;

/// <summary>The approved, in-force terms of use and community guidelines (D288/D290/D310).</summary>
static class PublicPolicyChecks
{
    public static void Run()
    {
        static void Need(bool ok,string name){if(!ok)throw new Exception("Public policy check failed: "+name);}
        Need(PublicPolicies.Status=="approved"&&PublicPolicies.IsApproved,"policies approved and in force");
        Need(PublicPolicies.Version=="2026-11-02-0.2"&&PublicPolicies.EffectiveAt<=DateTimeOffset.UtcNow,"approved version identity and effective date");
        Need(PublicPolicies.Source=="docs/delivery/TERMS-AND-COMMUNITY-GUIDELINES.md"&&PublicPolicies.SourceSha256.Length==64,"source provenance");
        var sourcePath=PublicPolicies.Source;
        Need(File.Exists(sourcePath),"run source-fidelity check from repository root");
        var source=File.ReadAllText(sourcePath);
        Need(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourcePath)))==PublicPolicies.SourceSha256,"source has not drifted from the approved text");
        Need(!source.Contains("draft",StringComparison.OrdinalIgnoreCase),"approved source is not labelled draft");
        Need(!PublicPolicies.ReadingNote.Contains("draft",StringComparison.OrdinalIgnoreCase)&&PublicPolicies.ReadingNote.Contains("does not record acceptance"),"reader is not acceptance");
        Need(PublicPolicies.Terms.Paragraphs.Count==14&&PublicPolicies.Community.Rules.Count==12&&PublicPolicies.Community.Paragraphs.Count==2,"complete supplied sections");
        Need(PublicPolicies.Terms.Paragraphs.Any(p=>p.Contains("Australian Consumer Law")&&p.Contains("cannot be excluded")),"consumer guarantees preserved");
        Need(PublicPolicies.Terms.Paragraphs.Any(p=>p.Contains("appeal an action once within 30 days")),"appeal route (D135)");
        Need(PublicPolicies.Community.Rules.Any(p=>p.Contains("non-consensual intimate images")),"image abuse prohibition retained");
        Need(PublicPolicies.Find("unknown")==null,"no arbitrary policy lookup");
        Need(File.ReadAllText("containers/keycloak/themes/swoovr/login/register-commons.ftl").Contains("sw-terms-link")&&File.ReadAllText("containers/keycloak/themes/swoovr/login/register-commons.ftl").Contains("sw-community-link"),"registration source links both policies");
        // issue 179/D312 (owner wording): creating an account agrees to the Terms of Use and acknowledges the Privacy Notice and
        // Community Guidelines; the 18+ checkbox stays a separate blocking attestation.
        var messages=File.ReadAllText("containers/keycloak/themes/swoovr/login/messages/messages_en.properties");
        Need(messages.Contains("swoovrAgreementBefore=By creating an account, you agree to our")&&messages.Contains("swoovrAgreementMiddle=and acknowledge our")&&messages.Contains("acceptTerms=I''m 18 or over")&&messages.Contains("swoovrAdultsOnly=Swoovr is exclusively for adults aged 18 and over."),"owner sign-up agreement wording and separate 18+ attestation");
        var now=DateTimeOffset.UtcNow;
        PublicPolicyVersion[] approved=[new(PublicPolicies.Version,PublicPolicyStatus.Approved,PublicPolicies.EffectiveAt,PublicPolicies.SourceSha256)];
        Need(PolicyAcceptancePolicy.Resolve(null,approved,now).State==PolicyRequirementState.ExistingAccessUnchanged,"no configured requirement preserves existing access");
        var required=PolicyAcceptancePolicy.Resolve(PublicPolicies.Version,approved,now);
        Need(required.State==PolicyRequirementState.Required&&required.Version==PublicPolicies.Version,"approved version can be required");
        Need(PolicyAcceptancePolicy.CanRecord(required,PublicPolicies.Version,true),"explicit acceptance of the approved version");
        Need(!PolicyAcceptancePolicy.CanRecord(required,"other",true)&&!PolicyAcceptancePolicy.CanRecord(required,PublicPolicies.Version,false),"no substituted version or implicit consent");
        Need(!PolicyAcceptancePolicy.CanRecord(new(PolicyRequirementState.ExistingAccessUnchanged,null),PublicPolicies.Version,true),"nothing recorded without a configured requirement");
        Need(PolicyAcceptancePolicy.Resolve("unknown-version",approved,now).State==PolicyRequirementState.ConfigurationInvalid,"unknown selection rejected");
        Console.WriteLine("PASS: approved public policy checks (terms of use and community guidelines 2026-11-02-0.2 in force).");
    }
}
