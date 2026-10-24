using Swoovr.Domain;

/// <summary>D125 and D146 market rules: who may use local discovery, who counts as local, profiles, requests and home moves.</summary>
static class MarketChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Market check failed: " + what); checks++; }
        var beta = new Market { Id = "adelaide", Status = MarketStatus.Beta };
        string[] all = ["dating", "friends", "activities", "business"];

        // Local discovery (rules 5, 30–33, 47).
        Expect(MarketAccess.Local(true, beta, all, "dating") == LocalAccess.Open, "a home city in a beta market");
        Expect(MarketAccess.Local(true, new Market { Status = MarketStatus.Active }, all, "dating") == LocalAccess.Open, "an active market");
        Expect(MarketAccess.Local(false, null, [], "dating") == LocalAccess.HomeCityRequired, "no home city");
        Expect(MarketAccess.Local(false, beta, all, "dating") == LocalAccess.HomeCityRequired, "a stale market without a home city still needs one");
        Expect(MarketAccess.Local(true, null, [], "dating") == LocalAccess.MarketNotOpen, "outside every market");
        Expect(MarketAccess.Local(true, new Market { Status = MarketStatus.Planned }, all, "dating") == LocalAccess.MarketNotOpen, "a planned market");
        Expect(MarketAccess.Local(true, new Market { Status = MarketStatus.Suspended }, all, "dating") == LocalAccess.MarketNotOpen, "a suspended market stops new discovery");
        Expect(MarketAccess.Local(true, beta, ["friends"], "dating") == LocalAccess.MarketNotOpen, "a mode the market doesn't offer");

        // Who is local (rules 9–10, 38–42).
        Expect(MarketAccess.SameMarket("adelaide", "adelaide", false, null), "two Adelaide members");
        Expect(!MarketAccess.SameMarket("adelaide", "melbourne", false, null), "an Adelaide member and a Melbourne member");
        Expect(!MarketAccess.SameMarket("adelaide", null, false, null), "someone outside every market");
        Expect(!MarketAccess.SameMarket(null, null, false, null), "two people outside every market never meet locally");
        Expect(!MarketAccess.SameMarket(null, "adelaide", false, null), "a viewer without an open market");
        Expect(MarketAccess.SameMarket("adelaide", null, true, new HashSet<string> { "adelaide" }), "a bot assigned to the market");
        Expect(MarketAccess.SameMarket("adelaide", "melbourne", true, new HashSet<string> { BotMarket.Everywhere }), "a bot assigned everywhere, whatever its home");
        Expect(!MarketAccess.SameMarket("adelaide", "adelaide", true, new HashSet<string>()), "a bot with no assignment, even with an Adelaide home");
        Expect(!MarketAccess.SameMarket("adelaide", "adelaide", true, null), "a bot whose assignments weren't loaded");
        Expect(!MarketAccess.SameMarket("adelaide", null, true, new HashSet<string> { "melbourne" }), "a bot assigned elsewhere");

        // Profiles and requests (rules 11–13, 19–21, 46).
        Expect(MarketAccess.ProfileOpens(true, false, false) && MarketAccess.ProfileOpens(false, true, false) && MarketAccess.ProfileOpens(false, false, true), "profiles open by market, relationship or visit");
        Expect(!MarketAccess.ProfileOpens(false, false, false), "a profile link from another market opens nothing");
        Expect(MarketAccess.MayRequest(true, false, false), "a request within the market");
        Expect(MarketAccess.MayRequest(false, true, false), "a connected pair adds a mode across markets");
        Expect(MarketAccess.MayRequest(false, false, true), "a local writes to a visiting traveller");
        Expect(!MarketAccess.MayRequest(false, false, false), "no remote contact otherwise");

        // Home moves (rules 28 and 34).
        var now = DateTimeOffset.Parse("2026-10-25T00:00:00Z");
        Expect(MarketAccess.CanMoveHome(null, "adelaide", null, null, now) == HomeMove.Allowed, "the first home market");
        Expect(MarketAccess.CanMoveHome("adelaide", "adelaide", "adelaide", now.AddDays(-1), now) == HomeMove.Allowed, "a new suburb in the same market");
        Expect(MarketAccess.CanMoveHome("adelaide", null, "adelaide", now.AddDays(-1), now) == HomeMove.Allowed, "leaving every market");
        Expect(MarketAccess.CanMoveHome(null, "adelaide", "adelaide", now.AddDays(-1), now) == HomeMove.Allowed, "returning to the market last moved into");
        Expect(MarketAccess.CanMoveHome("melbourne", "adelaide", "melbourne", now.AddDays(-29), now) == HomeMove.Locked, "another market within 30 days");
        Expect(MarketAccess.CanMoveHome(null, "adelaide", "melbourne", now.AddDays(-2), now) == HomeMove.Locked, "via no home city doesn't reset the wait");
        Expect(MarketAccess.CanMoveHome("melbourne", "adelaide", "melbourne", now.AddDays(-30), now) == HomeMove.Allowed, "another market after 30 days");

        Console.WriteLine($"PASS: {checks} Market cases (home city required, open/planned/suspended markets and offered modes, locals by home market, bots by assignment, profiles and requests by market, relationship or visit, the 30-day home move wait).");
    }
}
