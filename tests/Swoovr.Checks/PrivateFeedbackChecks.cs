using Swoovr.Domain;

static class PrivateFeedbackChecks
{
    public static void Run()
    {
        var count=0;
        var a=Guid.Parse("11111111-1111-1111-1111-111111111111");
        var b=Guid.Parse("22222222-2222-2222-2222-222222222222");
        var stranger=Guid.Parse("99999999-9999-9999-9999-999999999999");
        var interaction=Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now=new DateTimeOffset(2026,10,17,2,0,0,TimeSpan.Zero);
        var session=new AccessSession(AccountRole.Member,SignInMethod.EmailPassword,false,true,new HashSet<Guid>());
        var access=new FeedbackAccess(session,a,interaction,a,b,true,false);
        var recipient=access with {ActorId=b};
        var answers=new FeedbackAnswers(FeedbackContext.ChatOnly,PrivateAnswer.Yes,PrivateAnswer.Yes,PrivateAnswer.Uncertain,PrivateAnswer.Yes,ConnectionIntention.Friendship,null);
        void Check(string name,bool condition){if(!condition)throw new Exception("Private feedback: "+name);count++;}
        var first=PrivateFeedback.TrySubmit(access,answers,now)!;
        Check("genuine chat permits independent feedback",first is not null&&first.Revision==1);
        var history=first!.ReadOwnHistory(access)!;
        Check("author reads own labelled answers",history.Count==1&&history[0].Answers==answers&&history[0].SubmittedAt==now);
        Check("counterpart cannot read first answers",first.ReadOwnHistory(recipient) is null);
        var theirs=PrivateFeedback.TrySubmit(recipient,answers with {Context=FeedbackContext.MetInPerson,BoundariesRespected=PrivateAnswer.No},now)!;
        Check("counterpart submits independently",theirs.ReadOwnHistory(recipient)!.Count==1);
        Check("author cannot read counterpart answers",theirs.ReadOwnHistory(access) is null);
        Check("one met claim never changes other label",first.ReadOwnHistory(access)![0].Answers.Context==FeedbackContext.ChatOnly);
        Check("mere discovery not feedback evidence",PrivateFeedback.TrySubmit(access with {HadGenuineConnection=false},answers,now) is null);
        Check("agreed meeting permits feedback",PrivateFeedback.TrySubmit(access with {HadGenuineConnection=false,HadAgreedMeeting=true},answers,now) is not null);
        Check("missing historical evidence denies reads",first.ReadOwnHistory(access with {HadGenuineConnection=false}) is null);
        foreach(var invalid in new[]{access with {Session=null},access with {Session=session with {IsActive=false}},access with {Session=session with {Role=AccountRole.Admin}},access with {ActorId=stranger},access with {ActorId=Guid.Empty},access with {InteractionId=Guid.Empty},access with {FirstParticipant=Guid.Empty},access with {SecondParticipant=a}})
        {
            Check("invalid context cannot submit",PrivateFeedback.TrySubmit(invalid,answers,now) is null);
            Check("invalid context cannot read",first.ReadOwnHistory(invalid) is null);
            Check("invalid context cannot amend",first.TryAmend(invalid,1,answers,now) is null);
        }
        Check("other interaction no read",first.ReadOwnHistory(access with {InteractionId=stranger}) is null);
        Check("other pair no read",first.ReadOwnHistory(access with {SecondParticipant=stranger}) is null);
        Check("other interaction cannot amend",first.TryAmend(access with {InteractionId=stranger},1,answers,now) is null);
        Check("pair order stable",first.ReadOwnHistory(access with {FirstParticipant=b,SecondParticipant=a}) is not null);
        Check("no stale amendment",first.TryAmend(access,0,answers,now) is null);
        Check("no counterpart amendment",first.TryAmend(recipient,1,answers,now) is null);
        Check("no backwards timestamp",first.TryAmend(access,1,answers,now.AddSeconds(-1)) is null);
        var amended=first.TryAmend(access,1,answers with {Reconnect=PrivateAnswer.No,ReconnectCapacity=null,Chemistry=PrivateAnswer.Uncertain},now.AddMinutes(1))!;
        Check("amend appends audit revision",amended.Revision==2&&amended.ReadOwnHistory(access)!.Count==2&&amended.ReadOwnHistory(access)![1].Version==2);
        Check("prior answers retained",amended.ReadOwnHistory(access)![0].Answers==answers);
        Check("immutable original remains",first.Revision==1&&first.ReadOwnHistory(access)!.Count==1);
        Check("new answers private",amended.ReadOwnHistory(recipient) is null);
        Check("stale amendment after edit denied",amended.TryAmend(access,1,answers,now.AddMinutes(2)) is null);
        foreach(var invalid in new[]{answers with {Context=(FeedbackContext)99},answers with {ProfileRepresentedThem=(PrivateAnswer)99},answers with {IntentionsAligned=(PrivateAnswer)99},answers with {BoundariesRespected=(PrivateAnswer)99},answers with {Reconnect=(PrivateAnswer)99},answers with {Chemistry=(PrivateAnswer)99},answers with {ReconnectCapacity=null},answers with {ReconnectCapacity=(ConnectionIntention)99},answers with {Reconnect=PrivateAnswer.No},answers with {Reconnect=PrivateAnswer.Uncertain}})
        {
            Check("invalid answers cannot submit",PrivateFeedback.TrySubmit(access,invalid,now) is null);
            Check("invalid answers cannot amend",first.TryAmend(access,1,invalid,now) is null);
        }
        Check("null answers denied",PrivateFeedback.TrySubmit(access,null,now) is null&&first.TryAmend(access,1,null,now) is null);
        Check("null facts denied",PrivateFeedback.TrySubmit(null,answers,now) is null&&first.ReadOwnHistory(null) is null&&first.TryAmend(null,1,answers,now) is null);
        Check("no reconnect capacity when undecided",PrivateFeedback.TrySubmit(access,answers with {Reconnect=PrivateAnswer.Uncertain,ReconnectCapacity=null},now) is not null);
        Check("no reconnect capacity when declined",PrivateFeedback.TrySubmit(access,answers with {Reconnect=PrivateAnswer.No,ReconnectCapacity=null},now) is not null);
        Check("negative boundaries remains private feedback only",theirs.ReadOwnHistory(recipient)![0].Answers.BoundariesRespected==PrivateAnswer.No);
        Console.WriteLine($"PASS: {count} private feedback cases (fictional owner-only answers; no reports/ranking/enforcement).");
    }
}
