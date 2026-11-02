using Swoovr.Domain;

static class AccountInspectionChecks
{
    public static void Run()
    {
        var checks=0;
        void Expect(bool ok,string label){if(!ok)throw new Exception("Account inspection: "+label);checks++;}
        Expect(AccountInspectionPolicy.MaySeeAssurance(AccountRole.Legal),"Legal can see basic age assurance");
        Expect(AccountInspectionPolicy.MayPreview(AccountRole.Support)&&AccountInspectionPolicy.MayPreview(AccountRole.Admin)&&!AccountInspectionPolicy.MayPreview(AccountRole.Legal),"Legal gains no implicit public profile inspection");
        Expect(AccountInspectionPolicy.PublicMode(new MemberMode{Enabled=true,Visible=true})&&!AccountInspectionPolicy.PublicMode(new MemberMode{Enabled=true,Visible=false}),"hidden modes do not enter public preview");
        foreach(var state in new[]{MediaStatus.Pending,MediaStatus.Passed,MediaStatus.Uncertain,MediaStatus.Rejected})
            Expect(MediaPolicy.ProfileInspectable(new MediaItem{Status=state}),"approved Admin profile inspection state: "+state);
        Expect(!MediaPolicy.ProfileInspectable(new MediaItem{Status=MediaStatus.Blocked})&&!MediaPolicy.ProfileInspectable(new MediaItem{Status=MediaStatus.Passed,Purpose="attachment"}),"blocked and chat media never enter profile inspection");
        Console.WriteLine($"PASS: account inspection ({checks}).");
    }
}
