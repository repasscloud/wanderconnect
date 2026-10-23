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
        ModeChangeError Save(IReadOnlyCollection<Guid> requested, ConnectionMode? mode = null) =>
            ModeSettingsPolicy.Check(mode ?? dating, requested, [], offered).Error;
        Expect(Save([]) == ModeChangeError.None, "no intentions is fine");
        Expect(Save([serious.Id]) == ModeChangeError.None, "an offered intention");
        Expect(ModeSettingsPolicy.Check(null, [], [], offered).Error == ModeChangeError.UnknownMode, "unknown mode");
        Expect(Save([], hidden) == ModeChangeError.ModeUnavailable, "unavailable mode");
        Expect(Save([retired.Id]) == ModeChangeError.UnknownIntention, "adding a retired intention");
        Expect(Save([otherMode.Id]) == ModeChangeError.UnknownIntention, "another mode's intention");
        Expect(Save([Guid.NewGuid()]) == ModeChangeError.UnknownIntention, "made-up intention");

        // Q16: ten chips a mode, five at most; duplicates count once.
        var ten = Enumerable.Range(1, 10).Select(i => new ModeIntention { Id = Guid.NewGuid(), ModeId = "dating", Label = "chip " + i, Available = true }).ToList();
        ModeIntention[] full = [.. ten, retired];
        Expect(ModeSettingsPolicy.Check(dating, ten.Take(5).Select(i => i.Id).ToList(), [], full).Error == ModeChangeError.None, "five is allowed");
        Expect(ModeSettingsPolicy.Check(dating, ten.Take(6).Select(i => i.Id).ToList(), [], full).Error == ModeChangeError.TooManyIntentions, "six is refused");
        Expect(ModeSettingsPolicy.Check(dating, [.. ten.Take(5).Select(i => i.Id), ten[0].Id, ten[1].Id], [], full) is { Error: ModeChangeError.None, Stored.Count: 5 },
            "duplicates count once");

        // D113 (Q16 rule 16): a chip retired after it was picked stays stored and hidden, doesn't count, never blocks a save.
        var held = new[] { ten[0].Id, retired.Id };
        var kept = ModeSettingsPolicy.Check(dating, [ten[0].Id], held, full);
        Expect(kept.Error == ModeChangeError.None && kept.Stored.Contains(retired.Id), "a held retired chip survives a save that leaves it out");
        Expect(ModeSettingsPolicy.Check(dating, [ten[0].Id, retired.Id], held, full) is { Error: ModeChangeError.None, Stored.Count: 2 },
            "resending a held retired chip is not an addition");
        Expect(ModeSettingsPolicy.Check(dating, ten.Take(5).Select(i => i.Id).ToList(), held, full) is { Error: ModeChangeError.None, Stored.Count: 6 },
            "a held retired chip doesn't count towards the five");
        Expect(ModeSettingsPolicy.Check(dating, [], held, full).Stored.SequenceEqual([retired.Id]), "clearing every chip keeps the retired one hidden");
        Expect(ModeSettingsPolicy.Check(dating, [ten[0].Id], [otherMode.Id], full).Stored.SequenceEqual([ten[0].Id]), "nothing from another mode is carried over");

        // The approved set as seeded (D106, D113): forty chips, ten per mode, fixed distinct ids, labels within the column.
        var seed = Swoovr.Infrastructure.Persistence.IntentionChips.All;
        Expect(seed.Count == 40 && ConnectionModeIds.All.All(m => seed.Count(i => i.ModeId == m) == 10), "forty chips, ten per mode");
        Expect(seed.Select(i => i.Id).Distinct().Count() == 40 && seed.All(i => i.Available && i.Label.Length is > 0 and <= 40), "distinct ids, active, labels fit");
        Expect(seed[0] is { Label: "Long-term relationship", SortOrder: 1 } && seed[0].Id == Guid.Parse("00000016-0001-4000-8000-000000000001"), "first Dating chip id");
        Expect(seed[^1] is { ModeId: "business", Label: "Knowledge sharing", SortOrder: 10 } && seed[^1].Id == Guid.Parse("00000016-0004-4000-8000-000000000010"), "last Business chip id");

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

        Console.WriteLine($"PASS: {checks} connection mode cases (four stable ids, per-mode intentions and the five limit, retired chips kept (D113), the forty seeded chips, profile text limits, discovery eligibility both ways).");
    }
}
