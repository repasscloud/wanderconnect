using Swoovr.Client;
using Swoovr.Domain;

static class PublicPolicyDraftChecks
{
    public static void Run()
    {
        static void Need(bool ok,string name){if(!ok)throw new Exception("Public policy check failed: "+name);}
        Need(PublicPolicyDrafts.Status=="draft"&&!PublicPolicyDrafts.IsApproved,"draft never approved");
        Need(PublicPolicyDrafts.Version=="draft-2026-10-30-0.1","stable draft version identity");
        Need(PublicPolicyDrafts.Source=="docs/delivery/TERMS-AND-COMMUNITY-GUIDELINES-DRAFT.md"&&PublicPolicyDrafts.SourceSha256.Length==64,"source provenance");
        var sourcePath=PublicPolicyDrafts.Source;
        Need(File.Exists(sourcePath),"run source-fidelity check from repository root");
        Need(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(sourcePath)))==PublicPolicyDrafts.SourceSha256,"source has not drifted from prepared draft");
        Need(PublicPolicyDrafts.DraftLabel.Contains("Not in force")&&PublicPolicyDrafts.DraftLabel.Contains("does not accept"),"reader is not acceptance");
        Need(PublicPolicyDrafts.Terms.Paragraphs.Count==6&&PublicPolicyDrafts.Community.Rules.Count==8,"complete supplied sections");
        Need(PublicPolicyDrafts.Community.Rules.Any(p=>p.Contains("non-consensual intimate images")),"image abuse prohibition retained");
        Need(PublicPolicyDrafts.Find("unknown")==null,"no arbitrary policy lookup");
        Need(File.ReadAllText("containers/keycloak/themes/swoovr/login/register-commons.ftl").Contains("sw-terms-link")&&File.ReadAllText("containers/keycloak/themes/swoovr/login/register-commons.ftl").Contains("sw-community-link"),"registration source links both drafts");
        Need(!File.ReadAllText("containers/keycloak/themes/swoovr/login/messages/messages_en.properties").Contains("By continuing you agree"),"adult attestation is not inferred legal assent");
        var now=DateTimeOffset.UtcNow;
        PublicPolicyVersion[] drafts=[new(PublicPolicyDrafts.Version,PublicPolicyStatus.Draft,null,PublicPolicyDrafts.SourceSha256)];
        Need(PolicyAcceptancePolicy.Resolve(null,drafts,now).State==PolicyRequirementState.ExistingAccessUnchanged,"no configured version preserves existing access");
        Need(PolicyAcceptancePolicy.Resolve(PublicPolicyDrafts.Version,drafts,now).State==PolicyRequirementState.ConfigurationInvalid,"explicit draft configuration rejected");
        Need(!PolicyAcceptancePolicy.CanRecord(new(PolicyRequirementState.ExistingAccessUnchanged,null),PublicPolicyDrafts.Version,true),"draft never silently recorded");
        // Synthetic future-approved entry verifies the rule only; it is not part of a deployed catalogue.
        PublicPolicyVersion[] fixture=[new("fictional-approved",PublicPolicyStatus.Approved,now.AddDays(-1),new string('a',64))];
        var required=PolicyAcceptancePolicy.Resolve("fictional-approved",fixture,now);
        Need(PolicyAcceptancePolicy.CanRecord(required,"fictional-approved",true),"exact explicitly selected approved fixture");
        Need(!PolicyAcceptancePolicy.CanRecord(required,"other",true)&&!PolicyAcceptancePolicy.CanRecord(required,"fictional-approved",false),"no substituted version or implicit consent");
        Console.WriteLine("PASS: public draft policy checks; synthetic approval is not legal or integration evidence.");
    }
}
