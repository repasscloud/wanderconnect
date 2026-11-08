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
        MemberAttachments(Check);
        Console.WriteLine("PASS: ticket runtime untrusted content, filename, reference, escalation, attachment and staff queue-matrix policy (16 checks), member attachments (issue 220, 14 checks).");
    }

    /// <summary>Issue 220 (D330): the owner's types and limits, with content that has to match the type.</summary>
    private static void MemberAttachments(Action<bool,string> Check)
    {
        static bool Refused(string name,byte[] data){try{TicketingProjection.MemberAttachment(name,data);return false;}catch(ArgumentException){return true;}}
        byte[] Pad(byte[] head,int size=64){var data=new byte[Math.Max(size,head.Length)];head.CopyTo(data,0);return data;}
        var png=Pad([0x89,0x50,0x4E,0x47,0x0D,0x0A,0x1A,0x0A]);
        var pdf=Pad(System.Text.Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var mp4=Pad([0,0,0,0x18,(byte)'f',(byte)'t',(byte)'y',(byte)'p',(byte)'i',(byte)'s',(byte)'o',(byte)'m']);
        var docx=Pad([0x50,0x4B,0x03,0x04,..System.Text.Encoding.ASCII.GetBytes("....[Content_Types].xml")]);
        Check(TicketingProjection.MemberAttachment("screen shot.PNG",png).ContentType=="image/png","an image keeps its type, whatever the case of its extension");
        Check(TicketingProjection.MemberAttachment("statement.pdf",pdf).ContentType=="application/pdf"&&TicketingProjection.MemberAttachment("clip.mp4",mp4).ContentType=="video/mp4","a PDF and a video");
        Check(TicketingProjection.MemberAttachment("notes.docx",docx).ContentType.Contains("wordprocessingml")&&TicketingProjection.MemberAttachment("app.log",System.Text.Encoding.UTF8.GetBytes("12:00 ok\n")).ContentType=="text/plain","Word and a log");
        Check(Refused("photo.png",pdf)&&Refused("report.pdf",png),"content must match the extension");
        Check(Refused("setup.exe",Pad([0x4D,0x5A]))&&Refused("page.html",System.Text.Encoding.UTF8.GetBytes("<p>x</p>"))&&Refused("image.svg",System.Text.Encoding.UTF8.GetBytes("<svg/>")),"programs, pages and SVG are refused");
        Check(Refused("evil.pdf.exe",pdf)&&Refused("empty.png",[]),"the last extension counts; empty files are refused");
        Check(Refused("passport.txt",System.Text.Encoding.UTF8.GetBytes("my passport number"))&&Refused("binary.txt",[0x00,0x01,0x02]),"text keeps the sensitive-evidence screen and must be text");
        Check(Refused("big.png",Pad(png,(int)Swoovr.Contracts.SupportAttachmentPolicy.MaxImageBytes+1)),"per-type size limit");
        try{TicketingProjection.MemberAttachments(Enumerable.Range(0,6).Select(i=>($"p{i}.png",png)));Check(false,"six files accepted");}catch(ArgumentException){}
        Check(Swoovr.Contracts.SupportAttachmentPolicy.Problem([("a.png",10),("b.mov",Swoovr.Contracts.SupportAttachmentPolicy.MaxVideoBytes),("c.mp4",Swoovr.Contracts.SupportAttachmentPolicy.MaxVideoBytes)])?.Contains("150 MB")==true,"the message total");
        Check(Swoovr.Contracts.SupportAttachmentPolicy.Problem([("a.exe",10)])?.Contains("can't be attached")==true&&Swoovr.Contracts.SupportAttachmentPolicy.Problem([("a.heic",10),("b.m4a",10)]) is null,"what a member reads before sending");
        Check(TicketingEndpoints.IsUpload("/api/me/tickets/upload")&&TicketingEndpoints.IsUpload("/api/me/tickets/12/replies/upload")&&!TicketingEndpoints.IsUpload("/api/me/tickets/12/replies")&&!TicketingEndpoints.IsUpload("/api/admin/tickets/upload")&&TicketingEndpoints.IsUpload("/api/admin/tickets/12/replies/upload")&&!TicketingEndpoints.IsUpload("/api/admin/tickets/12/replies"),"only the upload routes take large bodies (members; staff replies, issue 222)");
        Check(TicketingRuntime.Fingerprints([new("a.png","image/png",png)]).Single().StartsWith("a.png:64:"),"an operation's digest covers file names, sizes and hashes");
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
            // D349, #281: the scheduled Home City check's items.
            ["Home City checks"]="FF-F",
        };
        AccountRole[] columns=[AccountRole.Support,AccountRole.TrustSafety,AccountRole.Legal,AccountRole.Admin];
        Check(matrix.Count==TicketingPolicy.Groups.Count&&matrix.Keys.All(TicketingPolicy.Groups.Contains),"matrix covers exactly the 14 queues");
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
