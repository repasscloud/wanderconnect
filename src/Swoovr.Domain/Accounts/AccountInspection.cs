namespace Swoovr.Domain;

/// <summary>D294: basic assurance for every staff role; public preview never grants Legal moderation authority.</summary>
public static class AccountInspectionPolicy
{
    public static bool MaySeeAssurance(AccountRole role)=>role is AccountRole.Support or AccountRole.Admin or AccountRole.Legal;
    public static bool MayPreview(AccountRole role)=>role is AccountRole.Support or AccountRole.Admin;
    public static bool PublicMode(MemberMode mode)=>mode.Enabled&&mode.Visible;
}
