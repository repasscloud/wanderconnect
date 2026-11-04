namespace Swoovr.Domain;

/// <summary>DEFAULT group policy. Unknown names and roles never receive an implicit grant.</summary>
public static class TicketingPolicy
{
    public static readonly IReadOnlyList<string> Groups = Array.AsReadOnly(new[] {
        "General Support", "Billing & Subscriptions", "Account Recovery", "Appeals", "Trust & Safety",
        "Trust & Safety — Restricted", "Legal & Law Enforcement", "Privacy & Data Protection",
        "Public Relations & Media", "Partnerships & Business Development", "Marketing & Community",
        "Internal Operations", "Security" });

    public static string MemberLogin(Guid memberId) => $"swoovr-{memberId:D}";

    public static bool CanTransfer(AccountRole role,string group) => CanAccessGroup(role,group) || group == "Trust & Safety — Restricted" && role is AccountRole.Support or AccountRole.Admin;

    public static bool CanAccessGroup(AccountRole role, string group) => group switch
    {
        "Legal & Law Enforcement" => role == AccountRole.Legal,
        "Trust & Safety — Restricted" => role == AccountRole.TrustSafety,
        "Trust & Safety" => role is AccountRole.TrustSafety or AccountRole.Admin,
        "Privacy & Data Protection" => role is AccountRole.Legal or AccountRole.Admin,
        "Internal Operations" or "Security" => role == AccountRole.Admin,
        "General Support" or "Billing & Subscriptions" or "Account Recovery" or "Appeals" or
        "Public Relations & Media" or "Partnerships & Business Development" or "Marketing & Community"
            => role is AccountRole.Support or AccountRole.Admin or AccountRole.TrustSafety,
        _ => false,
    };
}
