using Swoovr.Domain;

/// <summary>
/// Slice R rules: reciprocal matching (excluded chips both ways, D140; Dating "show me" both ways, D138), the viewer's
/// own filters (D137) with Plus filters only on Plus (D151), filter and identity validation, and visitor visibility.
/// </summary>
static class MatchingChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Matching check failed: " + what); checks++; }
        Guid C(int n) => new($"00000000-0000-0000-0000-{n:000000000000}");
        HashSet<Guid> Set(params int[] n) => n.Select(C).ToHashSet();
        MatchFacts F(int? age = 30, HashSet<Guid>? chips = null, HashSet<Guid>? excluded = null, bool id = false, string? gender = "woman",
            string[]? showMe = null, double? lat = null, double? lon = null) =>
            new(age, chips ?? [], excluded ?? [], id, gender, showMe ?? ["man", "woman", "non-binary", "another"], lat, lon);

        // Reciprocity (D138, D140).
        Expect(MatchPolicy.Reciprocal("friends", F(), F()), "no exclusions, not Dating");
        Expect(!MatchPolicy.Reciprocal("friends", F(excluded: Set(1)), F(chips: Set(1))), "the viewer excluded the person's chip");
        Expect(!MatchPolicy.Reciprocal("friends", F(chips: Set(1)), F(excluded: Set(1))), "the person excluded the viewer's chip (both ways)");
        Expect(MatchPolicy.Reciprocal("friends", F(chips: Set(1)), F(chips: Set(1))), "a chip alone never excludes");
        Expect(MatchPolicy.Reciprocal("dating", F(gender: "woman", showMe: ["man"]), F(gender: "man", showMe: ["woman"])), "each in the other's show me");
        Expect(!MatchPolicy.Reciprocal("dating", F(gender: "woman", showMe: ["man"]), F(gender: "man", showMe: ["man"])), "one-sided show me");
        Expect(!MatchPolicy.Reciprocal("dating", F(gender: null), F()), "no gender, no Dating");
        Expect(!MatchPolicy.Reciprocal("dating", F(), F(showMe: [])), "no show me, no Dating");
        Expect(MatchPolicy.Reciprocal("friends", F(gender: "woman", showMe: ["man"]), F(gender: "woman", showMe: ["man"])), "show me is Dating-only");
        // Q57 (D161): "prefer not to say" stays discoverable to any show me; their own show me still decides what they see.
        Expect(MatchPolicy.Reciprocal("dating", F(gender: "man", showMe: ["woman"]), F(gender: "unsaid", showMe: ["man"])), "prefer not to say is discoverable");
        Expect(!MatchPolicy.Reciprocal("dating", F(gender: "man", showMe: ["woman"]), F(gender: "unsaid", showMe: ["woman"])), "their own show me still applies");
        Expect(MatchPolicy.Reciprocal("dating", F(gender: "unsaid", showMe: ["woman"]), F(gender: "woman", showMe: ["man"])), "not limited to others who prefer not to say");
        Expect(MatchPolicy.Reciprocal("dating", F(gender: "woman", showMe: ["another"]), F(gender: "another", showMe: ["woman"])), "another gender is an inclusive show me category");
        Expect(!MatchPolicy.Reciprocal("dating", F(gender: "woman", showMe: ["man"]), F(gender: "another", showMe: ["woman"])), "another isn't in a show me that leaves it out");
        Expect(MatchPolicy.Reciprocal("dating", F(gender: "another", showMe: ["woman"]), F(gender: "woman", showMe: ["another"])), "another isn't limited to others who chose another");
        Expect(MatchPolicy.DatingReady(F()) && !MatchPolicy.DatingReady(F(gender: null)) && !MatchPolicy.DatingReady(F(showMe: [])), "Dating readiness");
        Expect(MatchPolicy.Reciprocal("dating", F(), F(gender: null, showMe: []) with { Bot = true }), "an authorised bot needs no show me");
        Expect(!MatchPolicy.Reciprocal("dating", F(excluded: Set(1)), F(chips: Set(1)) with { Bot = true }), "excluded chips still apply to bots");

        // The viewer's filters (D137).
        var free = PlanTier.Free;
        Expect(MatchPolicy.FilterAllows(null, F(), F(), free), "no filter");
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { MinAge = 35 }, F(), F(age: 30), free), "below the age range");
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { MaxAge = 25 }, F(), F(age: 30), free), "above the age range");
        Expect(MatchPolicy.FilterAllows(new ModeFilter { MinAge = 30, MaxAge = 30 }, F(), F(age: 30), free), "inclusive range");
        Expect(MatchPolicy.FilterAllows(new ModeFilter { RequiredChips = [C(1), C(2)] }, F(), F(chips: Set(2)), free), "any required chip");
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { RequiredChips = [C(1), C(2)], RequiredMatch = ChipMatch.All }, F(), F(chips: Set(2)), free), "all required chips");
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { RequiredChips = [C(1)] }, F(), F(), free), "required chip missing");
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { IdVerifiedOnly = true }, F(), F(id: false), free), "ID Verified only");
        Expect(MatchPolicy.FilterAllows(new ModeFilter { IdVerifiedOnly = true }, F(), F(id: true), free), "ID Verified person");
        var adelaide = F(lat: -34.93, lon: 138.6);
        var gawler = F(lat: -34.6, lon: 138.74); // about 39 km away
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { MaxDistanceKm = 20 }, adelaide, gawler, PlanTier.Plus), "Plus distance excludes");
        Expect(MatchPolicy.FilterAllows(new ModeFilter { MaxDistanceKm = 20 }, adelaide, gawler, free), "a lapsed Plus never hides people");
        Expect(MatchPolicy.FilterAllows(new ModeFilter { MaxDistanceKm = 50 }, adelaide, gawler, PlanTier.Plus), "within the distance");
        Expect(!MatchPolicy.FilterAllows(new ModeFilter { MaxDistanceKm = 50 }, adelaide, F(), PlanTier.Plus), "distance filter needs a home city");
        Expect(MatchPolicy.Shared(F(chips: Set(1, 2, 3)), F(chips: Set(2, 3, 4))) == 2, "shared chips rank");

        // Validation and Plus gating.
        var chips = Set(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        Expect(MatchPolicy.Check(new ModeFilter { MinAge = 18, MaxAge = 40 }, chips, free) == FilterError.None, "valid ages");
        Expect(MatchPolicy.Check(new ModeFilter { MinAge = 17 }, chips, free) == FilterError.AgeRange, "under 18");
        Expect(MatchPolicy.Check(new ModeFilter { MinAge = 40, MaxAge = 30 }, chips, free) == FilterError.AgeRange, "inverted range");
        Expect(MatchPolicy.Check(new ModeFilter { RequiredChips = [C(1), C(2), C(3), C(4), C(5), C(6)] }, chips, free) == FilterError.TooManyChips, "six required");
        Expect(MatchPolicy.Check(new ModeFilter { ExcludedChips = [.. Set(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11)] }, chips, free) == FilterError.TooManyChips, "eleven excluded");
        Expect(MatchPolicy.Check(new ModeFilter { RequiredChips = [C(99)] }, chips, free) == FilterError.UnknownChip, "another mode's chip");
        Expect(MatchPolicy.Check(new ModeFilter { RequiredChips = [C(1)], ExcludedChips = [C(1)] }, chips, free) == FilterError.ChipBothWays, "required and excluded");
        Expect(MatchPolicy.Check(new ModeFilter { MaxDistanceKm = 10 }, chips, free) == FilterError.PlusRequired, "distance needs Plus");
        Expect(MatchPolicy.Check(new ModeFilter { Sort = ExploreSort.Compatibility }, chips, free) == FilterError.PlusRequired, "sorting needs Plus");
        Expect(MatchPolicy.Check(new ModeFilter { MaxDistanceKm = 10, Sort = ExploreSort.Compatibility }, chips, PlanTier.Plus) == FilterError.None, "Plus filters on Plus");
        Expect(MatchPolicy.Check(new ModeFilter { MaxDistanceKm = 501 }, chips, PlanTier.Plus) == FilterError.Distance, "distance limit");
        var genders = new HashSet<string> { "man", "woman", "non-binary", "another", "unsaid" };
        var orientations = new HashSet<string> { "straight", "gay" };
        Expect(MatchPolicy.Check(new MemberIdentity { GenderId = "woman", DatingShowMe = ["man"] }, genders, orientations) == FilterError.None, "valid identity");
        Expect(MatchPolicy.Check(new MemberIdentity { GenderId = "robot" }, genders, orientations) == FilterError.UnknownGender, "unknown gender");
        Expect(MatchPolicy.Check(new MemberIdentity { DatingShowMe = ["robot"] }, genders, orientations) == FilterError.UnknownGender, "unknown show me");
        Expect(MatchPolicy.Check(new MemberIdentity { DatingShowMe = ["unsaid"] }, genders, orientations) == FilterError.UnknownGender, "prefer not to say is never a show me value");
        Expect(MatchPolicy.Check(new MemberIdentity { GenderId = "unsaid", DatingShowMe = ["another"] }, genders, orientations) == FilterError.None, "but it's a valid gender choice");
        Expect(MatchPolicy.Check(new MemberIdentity { OrientationId = "robot" }, genders, orientations) == FilterError.UnknownGender, "unknown orientation");
        Expect(MatchPolicy.Check(new MemberIdentity { Pronouns = new string('x', 41) }, genders, orientations) == FilterError.TextTooLong, "pronouns too long");
        Expect(new MemberIdentity().ShowAge && new MemberIdentity().ShowGender && !new MemberIdentity().ShowOrientation, "defaults: age and gender shown, orientation not");

        // Visitor visibility.
        Expect(MatchPolicy.ShowsVisitor(VisitorVisibility.Both, true) && MatchPolicy.ShowsVisitor(VisitorVisibility.Both, false), "both");
        Expect(MatchPolicy.ShowsVisitor(VisitorVisibility.Current, true) && !MatchPolicy.ShowsVisitor(VisitorVisibility.Current, false), "current");
        Expect(!MatchPolicy.ShowsVisitor(VisitorVisibility.Upcoming, true) && MatchPolicy.ShowsVisitor(VisitorVisibility.Upcoming, false), "upcoming");
        Expect(!MatchPolicy.ShowsVisitor(VisitorVisibility.Neither, true) && !MatchPolicy.ShowsVisitor(VisitorVisibility.Neither, false), "neither");

        Console.WriteLine($"PASS: {checks} Matching cases (excluded chips both ways, a chip alone never excludes, Dating show me both ways and Dating-only, age range, any/all required chips, ID Verified only, Plus distance and sorting only on Plus, filter and identity validation, visitor visibility).");
    }
}
