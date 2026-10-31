using Swoovr.Domain;
using Swoovr.Contracts;
using Swoovr.Client;

// Regression: an unsafe field, conflict or stale hash must refuse.
static class ContentRemovalChecks
{
    public static void Run()
    {
        var count=0;
        void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
        var hash=ContentRemovalPolicy.Hash("Fictional original text");
        Check(ContentRemovalPolicy.SameSource("Fictional original text",hash),"exact observed source accepted");
        Check(!ContentRemovalPolicy.SameSource("Fictional later edit",hash),"later edit cannot be removed with stale observation");
        foreach(var bad in new[]{"",new string('z',64),new string('0',63)})
            Check(!ContentRemovalPolicy.SameSource("Fictional original text",bad),"invalid fingerprint refused");
        Check(ContentRemovalPolicy.ValidTarget("about",null,null),"core about allowlisted");
        Check(ContentRemovalPolicy.ValidTarget("mode-about","friends",null),"per-mode about allowlisted");
        Check(ContentRemovalPolicy.ValidTarget("message",null,Guid.NewGuid()),"single message allowlisted");
        foreach(var field in new[]{"email","date-of-birth","displayName","About","profile","messages"})
            Check(!ContentRemovalPolicy.ValidTarget(field,null,null),"unapproved source field refused");
        Check(!ContentRemovalPolicy.ValidTarget("about","friends",null),"ambiguous field scope refused");
        Check(!ContentRemovalPolicy.ValidTarget("mode-about","unknown",null),"unknown mode refused");
        Check(!ContentRemovalPolicy.ValidTarget("message",null,Guid.Empty),"empty message identity refused");
        Check(ContentRemovalPolicy.Allows(AccountRole.Admin,CaseQueue.Admin,true,false),"admin may handle affiliated case");
        Check(ContentRemovalPolicy.Allows(AccountRole.Support,CaseQueue.Support,false,false),"support may remove ordinary reported content");
        Check(!ContentRemovalPolicy.Allows(AccountRole.Support,CaseQueue.Admin,false,false),"support cannot handle admin queue");
        Check(!ContentRemovalPolicy.Allows(AccountRole.Support,CaseQueue.Support,true,false),"support cannot handle affiliated subject");
        Check(!ContentRemovalPolicy.Allows(AccountRole.Member,CaseQueue.Support,false,false),"member never gains staff removal authority");
        Check(!ContentRemovalPolicy.Allows(AccountRole.Admin,CaseQueue.Admin,false,true),"personal or reporter conflict refuses admin too");
        var message=Guid.NewGuid();
        Check(ContentRemovalPolicy.ReportContainsMessage("conversation",$"{{\"messages\":[{{\"id\":\"{message}\"}}]}}",message),"reported message membership accepted");
        Check(!ContentRemovalPolicy.ReportContainsMessage("conversation","{}",message),"missing provenance refused");
        Check(!ContentRemovalPolicy.ReportContainsMessage("profile",$"{{\"messages\":[{{\"id\":\"{message}\"}}]}}",message),"profile evidence cannot authorize message removal");
        var removed=new HashSet<Guid>();var original=new MessageView(Guid.NewGuid(),Guid.NewGuid(),true,"text","Fictional original",DateTimeOffset.UtcNow,true);
        var mark=original with{Kind=MessageText.RemovedKind,Text=MessageText.RemovedBody,CanDelete=false};
        Check(MessageText.ApplyKnownRemoval(mark,removed).Text==MessageText.RemovedBody,"typed removal is recognised");
        var delayed=MessageText.ApplyKnownRemoval(original,removed);
        Check(delayed.Kind==MessageText.RemovedKind&&delayed.Text==MessageText.RemovedBody&&!delayed.CanDelete&&!delayed.Deleted,"delayed response cannot revive removed text or relabel it as an unsend");
        Console.WriteLine($"PASS: {count} content-removal policy cases.");
    }
}
