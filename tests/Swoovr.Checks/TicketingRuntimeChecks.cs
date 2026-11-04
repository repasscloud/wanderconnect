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
        Check(!TicketingPolicy.CanTransfer(AccountRole.Support,"Legal & Law Enforcement"),"support cannot create legal records");
        Check(TicketingPolicy.CanTransfer(AccountRole.Support,"Trust & Safety — Restricted"),"support can escalate without read");
        Check(TicketingPolicy.CanAccessGroup(AccountRole.TrustSafety,"General Support"),"T&S support inheritance");
        try{TicketingProjection.ValidateAttachment("passport.txt","text/plain",System.Text.Encoding.UTF8.GetBytes("identity document"));throw new Exception("sensitive upload accepted");}catch(ArgumentException){}
        try{TicketingProjection.ValidateAttachment("x.html","text/html",[60,62]);throw new Exception("active upload accepted");}catch(ArgumentException){}
        Console.WriteLine("PASS: ticket runtime untrusted content, filename, reference, escalation and attachment policy (8 checks).");
    }
}
