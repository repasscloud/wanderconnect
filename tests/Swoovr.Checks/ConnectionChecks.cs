using Swoovr.Domain;

static class ConnectionChecks
{
    public static void Run()
    {
        var count = 0;
        var a = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var b = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var outsider = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var area = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now = new DateTimeOffset(2026,10,17,1,0,0,TimeSpan.Zero);
        var session = new AccessSession(AccountRole.Member, SignInMethod.EmailPassword, false, true, new HashSet<Guid>());
        var member = new DiscoveryMember(a,true,true,true,true,new HashSet<DiscoveryContext>(Enum.GetValues<DiscoveryContext>()),new HashSet<ConnectionIntention>{ConnectionIntention.Friendship});
        var other = member with {Id=b};
        var rule = new DiscoveryRules(true,true,true,false,new IntentionRule(new HashSet<ConnectionIntention>(),RequiredIntentionMatch.Any,new HashSet<ConnectionIntention>()));
        var request = new DiscoveryAccessRequest(session,a,member,other,rule,rule,DiscoveryContext.Connect,DiscoveryResource.Contact,false,false,true,true,false,false,area,null);
        var access = new ConnectionAccess(session,a,member,other,false,false);
        var recipient = access with {ActorId=b};
        var reverse = request with {AuthenticatedAccountId=b,Viewer=other,Subject=member};
        var empty = ConnectionThread.Empty(a,b)!;
        void Check(string name,bool condition){if(!condition)throw new Exception("Connection: "+name);count++;}
        Check("empty rejects same participant",ConnectionThread.Empty(a,a) is null);
        Check("empty rejects missing participant",ConnectionThread.Empty(a,Guid.Empty) is null);
        var pending=empty.TryRequest(request,0,"Hello, would you like a coffee?",now)!;
        Check("eligible text request",pending is {State:ConnectionState.Pending,Revision:1}&&pending.Messages.Count==1);
        Check("original unchanged",empty.State==ConnectionState.None&&empty.Revision==0&&empty.Messages.Count==0);
        Check("pending visible to both",pending.CanRead(access)&&pending.CanRead(recipient));
        Check("pending cannot send second",pending.TrySend(access,1,"Again",now) is null);
        Check("pending recipient cannot reply before acceptance",pending.TrySend(recipient,1,"Reply",now) is null);
        Check("sender cannot accept",pending.TryAccept(request,1,now) is null);
        Check("sender cannot decline",pending.TryDecline(access,1) is null);
        Check("outsider cannot read",!pending.CanRead(access with {ActorId=outsider}));
        Check("outsider cannot accept",pending.TryAccept(reverse with {AuthenticatedAccountId=outsider},1,now) is null);
        Check("identity-swapped initial request denied",empty.TryRequest(request with {Viewer=member with {Id=outsider}},0,"Hello",now) is null);
        Check("other pair denied",empty.TryRequest(request with {Subject=other with {Id=outsider}},0,"Hello",now) is null);
        Check("profile grant cannot request",empty.TryRequest(request with {Resource=DiscoveryResource.Profile},0,"Hello",now) is null);
        Check("stale request denied",empty.TryRequest(request,1,"Hello",now) is null);
        Check("no repeat pending",pending.TryRequest(reverse,1,"Hello",now) is null);
        foreach(var text in new string?[]{null,"","  ",new string('x',1001)})
            Check("invalid request text",empty.TryRequest(request,0,text,now) is null);
        Check("maximum request accepted",empty.TryRequest(request,0,new string('x',1000),now) is not null);
        Check("request retains server timestamp",pending.Messages[0].SentAt==now);
        var declined=pending.TryDecline(recipient,1)!;
        Check("recipient declines",declined is {State:ConnectionState.Declined,Revision:2});
        Check("declined no repeated request either direction",declined.TryRequest(request,2,"Hello",now) is null&&declined.TryRequest(reverse with {Context=DiscoveryContext.Friends},2,"Hello",now) is null);
        Check("decline no accepted resurrection",declined.TryAccept(reverse,2,now) is null);
        Check("declined live chat denied",!declined.CanRead(recipient)&&declined.TrySend(access,2,"Hello",now) is null);
        Check("stale accept denied",pending.TryAccept(reverse,0,now) is null);
        Check("accept requires current contact grant",pending.TryAccept(reverse with {SubjectRules=rule with {AllowContact=false}},1,now) is null);
        Check("pre-arrival traveller initiation denied",empty.TryRequest(request with {Context=DiscoveryContext.Travel},0,"Hello",now) is null);
        Check("Q05 pre-arrival acceptance not approved",pending.TryAccept(reverse with {Context=DiscoveryContext.Travel},1,now) is null);
        var presence=new DestinationPresence(a,area,now.AddMinutes(-1),now.AddMinutes(1),true,true);
        Check("arrived traveller can request",empty.TryRequest(request with {Context=DiscoveryContext.Travel,Presence=presence},0,"Hello",now) is not null);
        Check("arrived traveller can accept",pending.TryAccept(reverse with {Context=DiscoveryContext.Travel,Presence=presence with {AccountId=b}},1,now) is not null);
        Check("pending read denied after block",!pending.CanRead(access with {BlockedEitherDirection=true}));
        Check("accept denies changed block",pending.TryAccept(reverse with {BlockedEitherDirection=true},1,now) is null);
        var accepted=pending.TryAccept(reverse,1,now)!;
        Check("recipient accepts",accepted is {State:ConnectionState.Accepted,Revision:2});
        Check("accepted both may read",accepted.CanRead(access)&&accepted.CanRead(recipient));
        Check("ordinary discovery mismatch exists",!new DiscoveryAccessPolicy().Allows(request with {AreaPreferencesSatisfied=false},now));
        Check("accepted continuity independent of new-discovery preferences",accepted.TrySend(access,2,"We can keep our conversation",now) is {Revision:3});
        var sent=accepted.TrySend(recipient,2,"Coffee sounds good",now)!;
        Check("accepted recipient sends",sent.Revision==3&&sent.Messages.Count==2&&sent.Messages[1].SenderId==b);
        Check("send is nondestructive",accepted.Messages.Count==1&&accepted.Revision==2);
        Check("stale send denied",sent.TrySend(access,2,"Outdated",now) is null);
        Check("backdated send denied",sent.TrySend(access,3,"Earlier",now.AddSeconds(-1)) is null);
        Check("backdated acceptance denied",pending.TryAccept(reverse,1,now.AddSeconds(-1)) is null);
        Check("swapped pair order permits valid member",accepted.CanRead(access with {First=other,Second=member}));
        foreach(var text in new string?[]{null,"","\t",new string('x',4001)})
            Check("invalid chat text",accepted.TrySend(access,2,text,now) is null);
        Check("maximum chat accepted",accepted.TrySend(access,2,new string('x',4000),now) is not null);
        foreach(var invalid in new[]{access with {Session=null},access with {Session=session with {IsActive=false}},access with {Session=session with {Role=AccountRole.Admin}},access with {ActorId=Guid.Empty},access with {ActorId=outsider},access with {First=member with {Id=outsider}},access with {Second=member},access with {BlockedEitherDirection=true},access with {ConversationPrivacyRevoked=true},access with {First=member with {IsActive=false}},access with {Second=other with {IsActive=false}},access with {First=member with {IsAdult=false}},access with {Second=other with {IsVerified=false}}})
        {
            Check("current unsafe read denied",!accepted.CanRead(invalid));
            Check("current unsafe send denied",accepted.TrySend(invalid,2,"Suppressed",now) is null);
        }
        var closed=accepted.TryClose(access,2)!;
        Check("either participant closes",closed is {State:ConnectionState.Closed,Revision:3}&&accepted.TryClose(recipient,2) is not null);
        Check("closed no live access",!closed.CanRead(access)&&closed.TrySend(access,3,"No",now) is null);
        Check("closed no repeated request",closed.TryRequest(request,3,"No",now) is null);
        Check("cannot close twice",closed.TryClose(access,3) is null);
        Check("recipient can decline after block",pending.TryDecline(recipient with {BlockedEitherDirection=true},1) is not null);
        Check("can close after privacy revocation",accepted.TryClose(access with {ConversationPrivacyRevoked=true},2) is not null);
        Check("outsider cannot close",accepted.TryClose(access with {ActorId=outsider},2) is null);
        Check("historical report after decline",declined.CanReport(access));
        Check("historical report after closure and block",closed.CanReport(recipient with {BlockedEitherDirection=true,ConversationPrivacyRevoked=true,First=member with {IsActive=false}}));
        Check("empty has no interaction to report",!empty.CanReport(access));
        Check("outsider no historical access",!closed.CanReport(access with {ActorId=outsider}));
        Check("anonymous no historical access",!closed.CanReport(access with {Session=null}));
        Check("null facts deny",!accepted.CanRead(null)&&!closed.CanReport(null)&&accepted.TrySend(null,2,"Hello",now) is null);
        Console.WriteLine($"PASS: {count} connection lifecycle cases (trusted hypothetical snapshots; no messaging service/persistence).");
    }
}
