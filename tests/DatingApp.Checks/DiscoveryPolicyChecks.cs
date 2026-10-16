using DatingApp.Domain;

static class DiscoveryPolicyChecks
{
    public static void Run()
    {
        var count = 0;
        var policy = new DiscoveryAccessPolicy();
        var ltr = ConnectionIntention.LongTermRelationship;
        var fwb = ConnectionIntention.FriendsWithBenefits;
        var friend = ConnectionIntention.Friendship;
        var casual = ConnectionIntention.CasualConnection;
        var all = new HashSet<DiscoveryContext> { DiscoveryContext.Connect, DiscoveryContext.Friends, DiscoveryContext.Travel };
        var viewerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var subjectId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var area = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var now = new DateTimeOffset(2026, 10, 16, 6, 0, 0, TimeSpan.Zero);
        var unrestricted = new IntentionRule(new HashSet<ConnectionIntention>(), RequiredIntentionMatch.Any, new HashSet<ConnectionIntention>());
        var rules = new DiscoveryRules(true, true, true, false, unrestricted);
        var viewer = new DiscoveryMember(viewerId, true, true, true, true, all, new HashSet<ConnectionIntention> { ltr, friend });
        var subject = viewer with { Id = subjectId };
        var session = new AccessSession(AccountRole.Member, SignInMethod.EmailPassword, false, true, new HashSet<Guid>());
        var request = new DiscoveryAccessRequest(session, viewerId, viewer, subject, rules, rules,
            DiscoveryContext.Connect, DiscoveryResource.Profile, false, false, true, true, false, false, area, null);
        void Check(string name, bool expected, DiscoveryAccessRequest value)
        {
            if (policy.Allows(value, now) != expected) throw new Exception($"Discovery policy: {name}");
            count++;
        }
        Check("eligible adults", true, request);
        foreach (var resource in Enum.GetValues<DiscoveryResource>())
        {
            var restricted = new IntentionRule(new HashSet<ConnectionIntention> { ltr }, RequiredIntentionMatch.Any, new HashSet<ConnectionIntention> { fwb, casual });
            var baseRequest = request with { Resource = resource, ViewerRules = rules with { Intentions = restricted } };
            Check($"{resource}: LTR plus hidden FWB excluded", false, baseRequest with { Subject = subject with { ApplicableIntentions = new HashSet<ConnectionIntention> { ltr, fwb } } });
            Check($"{resource}: LTR plus friendship allowed", true, baseRequest);
            Check($"{resource}: block cannot be bypassed", false, request with { Resource = resource, BlockedEitherDirection = true });
            Check($"{resource}: incognito refusal", false, request with { Resource = resource, IsIncognito = true, SubjectRules = rules with { AcceptIncognito = false } });
        }
        Check("anonymous", false, request with { Session = null });
        Check("unbound actor", false, request with { AuthenticatedAccountId = subjectId });
        Check("staff cannot use member discovery", false, request with { Session = session with { Role = AccountRole.Admin } });
        Check("revoked authentication", false, request with { Session = session with { IsActive = false } });
        foreach (var member in new[] { viewer with { IsActive = false }, viewer with { IsAdult = false }, viewer with { IsVerified = false } })
            Check("viewer safety gate", false, request with { Viewer = member });
        foreach (var member in new[] { subject with { IsActive = false }, subject with { IsAdult = false }, subject with { IsVerified = false } })
            Check("subject safety gate", false, request with { Subject = member });
        Check("self not a discovery grant", false, request with { Subject = viewer });
        Check("empty account", false, request with { Subject = subject with { Id = Guid.Empty } });
        Check("sparse hidden", false, request with { ViewerRules = rules with { HideSparse = true }, Subject = subject with { IsSubstantive = false } });
        Check("sparse allowed when not filtered", true, request with { Subject = subject with { IsSubstantive = false } });
        Check("reciprocal sparse filter", false, request with { SubjectRules = rules with { HideSparse = true }, Viewer = viewer with { IsSubstantive = false } });
        Check("no-photo not an input or denial", true, request);
        Check("subject refuses viewing", false, request with { SubjectRules = rules with { AllowViewing = false } });
        Check("viewer context unavailable", false, request with { Viewer = viewer with { EnabledContexts = new HashSet<DiscoveryContext>() } });
        Check("subject context unavailable", false, request with { Subject = subject with { EnabledContexts = new HashSet<DiscoveryContext>() } });
        Check("unmatched area", false, request with { AreaPreferencesSatisfied = false });
        Check("missing area", false, request with { DiscoveryAreaId = Guid.Empty });
        Check("unknown context", false, request with { Context = (DiscoveryContext)999 });
        Check("unknown resource", false, request with { Resource = (DiscoveryResource)999 });
        Check("subject reciprocal intention exclusion", false, request with { SubjectRules = rules with { Intentions = new IntentionRule(new HashSet<ConnectionIntention>(), RequiredIntentionMatch.Any, new HashSet<ConnectionIntention> { friend }) } });
        var required = new HashSet<ConnectionIntention> { ltr, fwb };
        Check("ANY required overlap", true, request with { ViewerRules = rules with { Intentions = unrestricted with { Required = required } } });
        Check("ALL requires full subset", false, request with { ViewerRules = rules with { Intentions = unrestricted with { Required = required, Match = RequiredIntentionMatch.All } } });
        Check("ALL fulfilled", true, request with { ViewerRules = rules with { Intentions = unrestricted with { Required = new HashSet<ConnectionIntention> { ltr, friend }, Match = RequiredIntentionMatch.All } } });
        Check("contradictory filters", false, request with { ViewerRules = rules with { Intentions = unrestricted with { Required = new HashSet<ConnectionIntention> { ltr }, Excluded = new HashSet<ConnectionIntention> { ltr } } } });
        Check("invalid rule mode", false, request with { ViewerRules = rules with { Intentions = unrestricted with { Match = (RequiredIntentionMatch)999 } } });
        Check("unknown candidate intention", false, request with { Subject = subject with { ApplicableIntentions = new HashSet<ConnectionIntention> { (ConnectionIntention)999 } } });
        Check("missing intentions", false, request with { Subject = subject with { ApplicableIntentions = null! } });
        Check("missing rule", false, request with { SubjectRules = null! });
        Check("contact permission separate", false, request with { Resource = DiscoveryResource.Contact, SubjectRules = rules with { AllowContact = false } });
        Check("view survives contact refusal", true, request with { SubjectRules = rules with { AllowContact = false } });
        Check("request limit enforced", false, request with { Resource = DiscoveryResource.Contact, RequestLimitAllows = false });
        Check("incognito disclosure explicit", false, request with { Resource = DiscoveryResource.Contact, IsIncognito = true });
        Check("incognito disclosure confirmed", true, request with { Resource = DiscoveryResource.Contact, IsIncognito = true, IncognitoDisclosureConfirmed = true });
        var traveller = request with { Resource = DiscoveryResource.Contact, RequiresDestinationPresence = true };
        Check("planned destination is no presence", false, traveller);
        Check("upcoming traveller can be viewed", true, traveller with { Resource = DiscoveryResource.Profile });
        var presence = new DestinationPresence(viewerId, area, now.AddMinutes(-1), now.AddMinutes(4), true, true);
        Check("fresh consented presence", true, traveller with { Presence = presence });
        foreach (var invalid in new[] { presence with { AccountId = subjectId }, presence with { AreaId = subjectId }, presence with { ObservedAt = now.AddSeconds(1) }, presence with { ExpiresAt = now }, presence with { HasConsent = false }, presence with { RiskApproved = false }, presence with { ExpiresAt = presence.ObservedAt } })
            Check("invalid presence denies initiation", false, traveller with { Presence = invalid });
        foreach (var context in Enum.GetValues<DiscoveryContext>())
            Check("block across " + context, false, request with { Context = context, BlockedEitherDirection = true });
        Console.WriteLine($"PASS: {count} discovery policy cases (trusted hypothetical snapshots; no API/profile/provider access).");
    }
}
