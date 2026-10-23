using Swoovr.Domain;

/// <summary>D92 mode rules: stable ids, per-mode settings, intentions, profile text and who Explore may show.</summary>
static class ConnectionModeChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Connection mode check failed: " + what); checks++; }

        Expect(ConnectionModeIds.All.SequenceEqual(["dating", "friends", "activities", "business"]), "exactly the four D92 modes, no travel");

        var dating = new ConnectionMode { Id = "dating", Label = "Dating", Available = true };
        var hidden = new ConnectionMode { Id = "business", Label = "Business", Available = false };
        var serious = new ModeIntention { Id = Guid.NewGuid(), ModeId = "dating", Label = "fictional chip", Available = true };
        var retired = new ModeIntention { Id = Guid.NewGuid(), ModeId = "dating", Label = "retired chip", Available = false };
        var otherMode = new ModeIntention { Id = Guid.NewGuid(), ModeId = "friends", Label = "friends chip", Available = true };
        ModeIntention[] offered = [serious, retired, otherMode];
        Expect(ModeSettingsPolicy.Check(dating, [], offered) == ModeChangeError.None, "no intentions is fine (none configured yet)");
        Expect(ModeSettingsPolicy.Check(dating, [serious.Id], offered) == ModeChangeError.None, "an offered intention");
        Expect(ModeSettingsPolicy.Check(null, [], offered) == ModeChangeError.UnknownMode, "unknown mode");
        Expect(ModeSettingsPolicy.Check(hidden, [], offered) == ModeChangeError.ModeUnavailable, "unavailable mode");
        Expect(ModeSettingsPolicy.Check(dating, [retired.Id], offered) == ModeChangeError.UnknownIntention, "retired intention");
        Expect(ModeSettingsPolicy.Check(dating, [otherMode.Id], offered) == ModeChangeError.UnknownIntention, "another mode's intention");
        Expect(ModeSettingsPolicy.Check(dating, [Guid.NewGuid()], offered) == ModeChangeError.UnknownIntention, "made-up intention");
        Expect(ModeSettingsPolicy.Check(dating, Enumerable.Range(0, 6).Select(_ => serious.Id).ToList(), offered) == ModeChangeError.TooManyIntentions, "more than five");

        Expect(ProfileTextPolicy.Clean("  Fictional Sam  ") == "Fictional Sam" && ProfileTextPolicy.Clean("   ") is null, "trim and blank clears");
        Expect(ProfileTextPolicy.Check("Sam", "Adelaide", "Line one\nline two") == ProfileTextError.None, "plain text with line breaks in about");
        Expect(ProfileTextPolicy.Check(new string('a', 41), null, null) == ProfileTextError.DisplayNameTooLong, "name length");
        Expect(ProfileTextPolicy.Check(null, new string('a', 61), null) == ProfileTextError.AreaTooLong, "area length");
        Expect(ProfileTextPolicy.Check(null, null, new string('a', 501)) == ProfileTextError.AboutTooLong, "about length");
        Expect(ProfileTextPolicy.Check("Sam\u0007", null, null) == ProfileTextError.ControlCharacters, "control characters in name");
        Expect(ProfileTextPolicy.Check("Sam", "Adel\naide", null) == ProfileTextError.ControlCharacters, "line break in area");

        var person = new DiscoveryCandidate(AccountStatus.Active, AgeStatus.Attested, BetaActive: true, Bot: false, Employee: false, DisplayName: "Fictional Sam");
        Expect(DiscoveryEligibility.Discoverable(person), "active adult on the beta with a name");
        foreach (var (label, changed) in new (string, DiscoveryCandidate)[] {
            ("locked", person with { Status = AccountStatus.Locked }), ("suspended", person with { Status = AccountStatus.Suspended }),
            ("underage", person with { Age = AgeStatus.Underage }), ("no 18+ record", person with { Age = AgeStatus.Unconfirmed }),
            ("age check pending", person with { Age = AgeStatus.NeedsVerification }), ("not on the beta", person with { BetaActive = false }),
            ("bot (Q11)", person with { Bot = true }), ("staff (Q11)", person with { Employee = true }), ("no name", person with { DisplayName = " " }) })
            Expect(!DiscoveryEligibility.Discoverable(changed), label + " is never shown");
        foreach (var age in new[] { AgeStatus.SelfDeclaredAdult, AgeStatus.Verified, AgeStatus.AdultOverride })
            Expect(DiscoveryEligibility.Discoverable(person with { Age = age }), age + " counts as adult");

        MemberMode On(bool visible = true) => new() { ModeId = "dating", Enabled = true, Visible = visible };
        Expect(DiscoveryEligibility.ShownIn("dating", dating, On(), On(), person), "both on, visible");
        Expect(!DiscoveryEligibility.ShownIn("dating", dating, null, On(), person), "viewer never turned the mode on");
        Expect(!DiscoveryEligibility.ShownIn("dating", dating, new MemberMode { ModeId = "dating", Enabled = false }, On(), person), "viewer turned it off");
        Expect(!DiscoveryEligibility.ShownIn("dating", dating, On(), On(visible: false), person), "person hid themselves");
        Expect(!DiscoveryEligibility.ShownIn("dating", dating, On(), new MemberMode { ModeId = "dating", Enabled = false, Visible = true }, person), "visible but off");
        Expect(!DiscoveryEligibility.ShownIn("dating", dating, On(), new MemberMode { ModeId = "friends", Enabled = true, Visible = true }, person), "another mode's setting");
        Expect(!DiscoveryEligibility.ShownIn("dating", new ConnectionMode { Id = "dating", Available = false }, On(), On(), person), "mode switched off in configuration");
        Expect(!DiscoveryEligibility.ShownIn("dating", dating, On(), On(), person with { BetaActive = false }), "mode never bypasses the gate");

        Console.WriteLine($"PASS: {checks} connection mode cases (four stable ids, per-mode intentions, profile text limits, discovery eligibility both ways, Q11 exclusions).");
    }
}
