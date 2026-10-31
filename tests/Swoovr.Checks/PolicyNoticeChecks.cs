using Swoovr.Client;
using Swoovr.Domain;
static class PolicyNoticeChecks
{
    public static void Run()
    {
        var n=0; void Check(bool value){if(!value)throw new Exception("Policy notice rules failed.");n++;}
        var now=new DateTimeOffset(2026,10,30,0,0,0,TimeSpan.Zero);
        var publication=new PolicyNoticePublication{Id=Guid.NewGuid(),Version="2026-10-30.1",Title="Meeting safety advice updated",Link="/safety/advice",StagedAt=now};
        Check(PolicyNoticePolicy.ValidVersion(publication.Version));
        foreach(var invalid in new[]{"","../x","a?x=1","a#x","a/x","a b","ä","a\n"})Check(!PolicyNoticePolicy.ValidVersion(invalid));
        Check(!PolicyNoticePolicy.ValidTitle("\nPrivate title"));
        foreach(var link in new[]{"https://outside.example.test/policy","//outside.example.test","/privacy","/terms","/community-guidelines","/safety?contact=private","/safety/advice#private","/safety/meeting/secret","/safety/../privacy"})
            Check(!PolicyNoticePolicy.AllowedLink(link));
        Check(!PolicyNoticePolicy.CanPublish(publication,false));
        Check(!PolicyNoticePolicy.CanDisplay(publication,now));
        Check(PolicyNoticePolicy.CanPublish(publication,true));
        publication.PublishedAt=now;publication.PurgeAfter=now.AddDays(60);
        Check(!PolicyNoticePolicy.CanPublish(publication,true));
        Check(PolicyNoticePolicy.CanDisplay(publication,now));
        Check(!PolicyNoticePolicy.CanDisplay(publication,now.AddDays(60)));
        Check(PolicyNoticePolicy.Eligible(true,now,now));
        Check(!PolicyNoticePolicy.Eligible(true,now.AddTicks(1),now));
        Check(!PolicyNoticePolicy.Eligible(false,now.AddDays(-1),now));
        Check(PolicyNoticePolicy.Dedupe(publication.Id)==PolicyNoticePolicy.Dedupe(publication.Id));
        Check(PolicyNoticePolicy.Dedupe(publication.Id)!=PolicyNoticePolicy.Dedupe(Guid.NewGuid()));
        Check(PolicyNoticePolicy.Dedupe(publication.Id).Length<=64);
        var centreOnly=NotificationPolicy.Decide(new(NotificationKind.PolicyChange,true,false,false,true,true,false,false,true,true));
        Check(centreOnly.Centre&&centreOnly.Push==NotificationPolicy.NotForKind&&centreOnly.Email==NotificationPolicy.NotForKind);
        var channelsOff=NotificationPolicy.Decide(new(NotificationKind.PolicyChange,true,false,false,true,true,false,false,false,false));
        Check(channelsOff.Centre&&channelsOff.Push==NotificationPolicy.NotForKind&&channelsOff.Email==NotificationPolicy.NotForKind);
        var privatePush=NotificationPolicy.Push(NotificationKind.PolicyChange,true,"Private member","Private mode","Private text","Private place");
        Check(!privatePush.Body.Contains("Private")&&privatePush.Title=="Swoovr");
        Check(!NotificationPolicy.Decide(new(NotificationKind.PolicyChange,true,true,false,true,true,false,false,true,true)).Centre);
        Check(SafetyAdvice.BeforeConnecting.Count>=3&&SafetyAdvice.BeforeMeeting.Count>=3);
        Check(SafetyAdvice.ShowDuringSetup(null)&&!SafetyAdvice.ShowDuringSetup("Fictional"));
        Check(!SafetyAdvice.BeforeMeeting.Any(s=>s.Contains("guarantee your safety",StringComparison.OrdinalIgnoreCase)));
        Console.WriteLine($"PASS: policy notice and safety advice rules ({n} checks; no persistence/publication proof).");
    }
}
