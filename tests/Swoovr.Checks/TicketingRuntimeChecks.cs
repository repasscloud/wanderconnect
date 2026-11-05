using Swoovr.Api.Ticketing;
using Swoovr.Domain;

internal static class TicketingRuntimeChecks
{
    public static void Run()
    {
        void Check(bool ok,string name){if(!ok)throw new Exception(name);}
        Check(TicketingProjection.Plain("<script>alert(1)</script><p>Hello &amp; goodbye<img src='https://evil.test/x'></p>","text/html")=="Hello & goodbye", "HTML projects without script or remote image");
        Check(TicketingProjection.Filename("../../bad\r\nname.txt")=="badname.txt","filename cannot inject path or headers");
        Check(TicketingProjection.Reference(42)=="TK-42","ticket references never collide with SW reports");
        Check(TicketingPolicy.CanTransfer(AccountRole.Support,"Legal & Law Enforcement")&&!TicketingPolicy.CanAccessGroup(AccountRole.Support,"Legal & Law Enforcement"),"support hands off to Legal without read");
        Check(TicketingPolicy.CanTransfer(AccountRole.Support,"Trust & Safety — Restricted"),"support can escalate without read");
        Check(TicketingPolicy.CanAccessGroup(AccountRole.TrustSafety,"General Support"),"T&S support inheritance");
        StaffMatrix(Check);
        try{TicketingProjection.ValidateAttachment("passport.txt","text/plain",System.Text.Encoding.UTF8.GetBytes("identity document"));throw new Exception("sensitive upload accepted");}catch(ArgumentException){}
        try{TicketingProjection.ValidateAttachment("x.html","text/html",[60,62]);throw new Exception("active upload accepted");}catch(ArgumentException){}
        Console.WriteLine("PASS: ticket runtime untrusted content, filename, reference, escalation, attachment and staff queue-matrix policy (16 checks).");
    }

    /// <summary>The owner's final matrix (#204, 5 November 2026), cell by cell, and what is mirrored into Zammad.</summary>
    private static void StaffMatrix(Action<bool,string> Check)
    {
        // F = works the queue, H = hand off only, - = none. Columns: Support, Trust & Safety, Legal, Admin.
        var matrix=new Dictionary<string,string>
        {
            ["General Support"]="FF-F",["Billing & Subscriptions"]="FF-F",["Account Recovery"]="FF-F",["Appeals"]="-F-F",
            ["Public Relations & Media"]="FF-F",["Partnerships & Business Development"]="FF-F",["Marketing & Community"]="FF-F",
            ["Trust & Safety"]="-F-F",["Trust & Safety — Restricted"]="HF-H",["Legal & Law Enforcement"]="HHFH",
            ["Privacy & Data Protection"]="--FF",["Internal Operations"]="---F",["Security"]="---F",
        };
        AccountRole[] columns=[AccountRole.Support,AccountRole.TrustSafety,AccountRole.Legal,AccountRole.Admin];
        Check(matrix.Count==TicketingPolicy.Groups.Count&&matrix.Keys.All(TicketingPolicy.Groups.Contains),"matrix covers exactly the 13 queues");
        var mismatches=matrix.SelectMany(row=>columns.Select((role,i)=>(row.Key,role,Expected:row.Value[i],
            Actual:TicketingPolicy.CanAccessGroup(role,row.Key)?'F':TicketingPolicy.CanHandOff(role,row.Key)?'H':'-'))).Where(c=>c.Expected!=c.Actual).ToList();
        Check(mismatches.Count==0,"role-to-queue matrix matches the owner's decision: "+string.Join(", ",mismatches.Select(m=>m.Key+"/"+m.role)));
        var support=TicketingPolicy.EffectiveRoles([AccountRole.Support],false);
        var trust=TicketingPolicy.EffectiveRoles([AccountRole.TrustSafety],false);
        var legal=TicketingPolicy.EffectiveRoles([AccountRole.Legal],false);
        var root=TicketingPolicy.EffectiveRoles([],true);
        Check(trust.SetEquals([AccountRole.TrustSafety,AccountRole.Support])&&root.SetEquals([AccountRole.Admin]),"root admin acts as Admin and Trust & Safety also as Support, as the API applies them");
        Check(TicketingPolicy.StaffGrants(support,false).GetValueOrDefault("Appeals")is null&&TicketingPolicy.StaffGrants(support,false)["Legal & Law Enforcement"]=="create","Support: no Appeals, hand-off into Legal");
        var trustWeb=TicketingPolicy.StaffGrants(trust,true);
        Check(TicketingPolicy.StaffGrants(trust,false)["Trust & Safety — Restricted"]=="full"&&trustWeb["Trust & Safety — Restricted"]=="create","Restricted works through Swoovr Admin only; the web sign-in can only hand off");
        Check(TicketingPolicy.StaffGrants(legal,true).Values.All(v=>v=="create"),"Legal's web sign-in works no queue (both its queues are sensitive)");
        Check(TicketingPolicy.Groups.All(g=>!TicketingPolicy.Sensitive.Contains(g)||TicketingPolicy.StaffGrants(TicketingPolicy.EffectiveRoles([AccountRole.Support,AccountRole.TrustSafety,AccountRole.Legal,AccountRole.Admin],true),true).GetValueOrDefault(g)!="full"),"no combination of roles gives a web sign-in a sensitive queue");
        Check(TicketingPolicy.StaffLogin(Guid.Empty)!=TicketingPolicy.StaffWebLogin(Guid.Empty)&&TicketingPolicy.StaffWebLogin(Guid.Empty)!=TicketingPolicy.MemberLogin(Guid.Empty),"API, web and member helpdesk logins never coincide");
    }
}
