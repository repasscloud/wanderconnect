using Swoovr.Domain;

static class MeetingPlanChecks
{
    public static void Run()
    {
        var count=0;
        var a=Guid.Parse("11111111-1111-1111-1111-111111111111");
        var b=Guid.Parse("22222222-2222-2222-2222-222222222222");
        var stranger=Guid.Parse("99999999-9999-9999-9999-999999999999");
        var area=Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now=new DateTimeOffset(2026,10,17,1,0,0,TimeSpan.Zero);
        var session=new AccessSession(AccountRole.Member,SignInMethod.EmailPassword,false,true,new HashSet<Guid>());
        var first=new DiscoveryMember(a,true,true,true,true,new HashSet<DiscoveryContext>{DiscoveryContext.Friends},new HashSet<ConnectionIntention>{ConnectionIntention.Friendship});
        var second=first with {Id=b};
        var rules=new DiscoveryRules(true,true,true,false,new IntentionRule(new HashSet<ConnectionIntention>(),RequiredIntentionMatch.Any,new HashSet<ConnectionIntention>()));
        var request=new DiscoveryAccessRequest(session,a,first,second,rules,rules,DiscoveryContext.Friends,DiscoveryResource.Contact,false,false,true,true,false,false,area,null);
        var empty=ConnectionThread.Empty(a,b)!;
        var pending=empty.TryRequest(request,0,"Coffee?",now)!;
        var connection=pending.TryAccept(request with {AuthenticatedAccountId=b,Viewer=second,Subject=first},1,now)!;
        var access=new ConnectionAccess(session,a,first,second,false,false);
        var recipient=access with {ActorId=b};
        var terms=new MeetingPlanTerms(DiscoveryContext.Friends,ConnectionIntention.Friendship,"Coffee and conversation","Fictional central public cafe",now.AddDays(1),"Australia/Adelaide","Each pays their own");
        void Check(string name,bool condition){if(!condition)throw new Exception("Meeting plan: "+name);count++;}
        var plan=MeetingPlan.TryCreate(connection,access,terms,now)!;
        Check("private proposal starts unaccepted",plan is {State:MeetingPlanState.Proposed,Revision:0,ProposalVersion:1,Agreed:null}&&plan.Confirmations.Count==0);
        Check("pending connection cannot plan",MeetingPlan.TryCreate(pending,access,terms,now) is null);
        Check("outsider cannot plan",MeetingPlan.TryCreate(connection,access with {ActorId=stranger},terms,now) is null);
        var one=plan.TryConfirm(connection,access,0,1,now)!;
        Check("first explicit consent insufficient",one.State==MeetingPlanState.Proposed&&one.Agreed is null&&one.Confirmations.Count==1);
        Check("duplicate consent rejected",one.TryConfirm(connection,access,1,1,now) is null);
        Check("stale revision rejected",one.TryConfirm(connection,recipient,0,1,now) is null);
        Check("wrong version rejected",one.TryConfirm(connection,recipient,1,2,now) is null);
        var agreed=one.TryConfirm(connection,recipient,1,1,now)!;
        Check("both explicit consent agree terms",agreed.State==MeetingPlanState.Accepted&&agreed.Agreed==terms&&agreed.AgreedVersion==1);
        Check("original unchanged",plan.Confirmations.Count==0&&plan.Agreed is null);
        Check("completion before scheduled time denied",agreed.TryComplete(connection,access,2,1,now) is null);
        var changed=terms with {StartsAt=terms.StartsAt.AddHours(1),Activity="Walk and coffee"};
        var proposed=agreed.TryPropose(connection,access,2,changed,now)!;
        Check("unilateral proposal preserves agreement",proposed.State==MeetingPlanState.Accepted&&proposed.Agreed==terms&&proposed.AgreedVersion==1&&proposed.ProposalVersion==2&&proposed.Confirmations.Count==0);
        Check("old version consent cannot accept replacement",proposed.TryConfirm(connection,recipient,3,1,now) is null);
        var reproposed=proposed.TryConfirm(connection,recipient,3,2,now)!;
        Check("one replacement consent retains old agreement",reproposed.Agreed==terms);
        var rescheduled=reproposed.TryConfirm(connection,access,4,2,now)!;
        Check("both replacement consent updates agreement",rescheduled.Agreed==changed&&rescheduled.AgreedVersion==2&&rescheduled.CompletionConfirmations.Count==0);
        Check("old agreed version cannot complete",rescheduled.TryComplete(connection,access,5,1,changed.StartsAt) is null);
        var completedOne=rescheduled.TryComplete(connection,access,5,2,changed.StartsAt)!;
        Check("one completion does not complete both",completedOne.State==MeetingPlanState.Accepted&&completedOne.CompletionConfirmations.Count==1);
        Check("duplicate completion denied",completedOne.TryComplete(connection,access,6,2,changed.StartsAt) is null);
        var completed=completedOne.TryComplete(connection,recipient,6,2,changed.StartsAt)!;
        Check("both completion acknowledgements complete",completed.State==MeetingPlanState.Completed&&completed.CompletionConfirmations.Count==2);
        Check("completed immutable",completed.TryPropose(connection,access,7,terms with {StartsAt=now.AddDays(2)},now) is null&&completed.TryCancel(connection,recipient,7) is null);
        Check("unaccepted cannot complete",plan.TryComplete(connection,access,0,1,terms.StartsAt) is null);
        var closed=connection.TryClose(access,2)!;
        Check("closed connection cannot confirm",plan.TryConfirm(closed,recipient,0,1,now) is null);
        Check("closed connection can cancel",agreed.TryCancel(closed,access,2) is {State:MeetingPlanState.Cancelled});
        Check("blocked participant can cancel",agreed.TryCancel(connection,access with {BlockedEitherDirection=true},2) is {State:MeetingPlanState.Cancelled});
        var cancelled=proposed.TryCancel(connection,recipient,3)!;
        Check("cancel ends pending revision",cancelled.State==MeetingPlanState.Cancelled&&cancelled.TryConfirm(connection,access,4,2,now) is null);
        foreach(var invalid in new[]{access with {Session=null},access with {ActorId=stranger},access with {BlockedEitherDirection=true},access with {ConversationPrivacyRevoked=true},access with {Second=second with {IsActive=false}},access with {First=first with {Id=stranger}}})
        {
            Check("unsafe create denied",MeetingPlan.TryCreate(connection,invalid,terms,now) is null);
            Check("unsafe edit denied",agreed.TryPropose(connection,invalid,2,changed,now) is null);
            Check("unsafe confirm denied",plan.TryConfirm(connection,invalid,0,1,now) is null);
            Check("unsafe complete denied",agreed.TryComplete(connection,invalid,2,1,terms.StartsAt) is null);
        }
        foreach(var invalid in new[]{terms with {Activity=" "},terms with {Activity=new string('x',201)},terms with {PublicPlaceOrArea=""},terms with {CostResponsibility=""},terms with {TimeZoneId="Unknown/NotAZone"},terms with {StartsAt=now},terms with {Context=(DiscoveryContext)99},terms with {Intention=(ConnectionIntention)99}})
        {
            Check("invalid terms creation denied",MeetingPlan.TryCreate(connection,access,invalid,now) is null);
            Check("invalid terms proposal denied",agreed.TryPropose(connection,access,2,invalid,now) is null);
        }
        Check("null terms denied",MeetingPlan.TryCreate(connection,access,null,now) is null);
        Check("same proposal not revision",agreed.TryPropose(connection,access,2,terms,now) is null);
        Check("consent after scheduled instant denied",plan.TryConfirm(connection,access,0,1,terms.StartsAt) is null);
        var offset=terms with {StartsAt=terms.StartsAt.ToOffset(TimeSpan.FromHours(9.5))};
        Check("stored instant normalized UTC",MeetingPlan.TryCreate(connection,access,offset,now)!.Proposal.StartsAt.Offset==TimeSpan.Zero);
        var pendingCompletion=proposed.TryComplete(connection,access,3,1,terms.StartsAt)!.TryComplete(connection,recipient,4,1,terms.StartsAt)!;
        Check("pending edit cannot replace completed agreement",pendingCompletion.State==MeetingPlanState.Completed&&pendingCompletion.Agreed==terms&&pendingCompletion.TryConfirm(connection,access,5,2,now) is null);
        Check("replacement clears old completion acknowledgements",completedOne.TryPropose(connection,access,6,changed with {StartsAt=changed.StartsAt.AddDays(1)},changed.StartsAt)!.TryConfirm(connection,access,7,3,changed.StartsAt)!.TryConfirm(connection,recipient,8,3,changed.StartsAt)!.CompletionConfirmations.Count==0);
        Console.WriteLine($"PASS: {count} meeting plan cases (two-party domain consent only; no attendance/commerce evidence).");
    }
}
