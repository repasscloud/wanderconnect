using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;

/// <summary>
/// Account standing (#50): support references, what the member must do, sanction states and appeal deadlines, and the
/// shared wording (a blocked photo reads like any refusal).
/// </summary>
static class StandingChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Standing check failed: " + what); checks++; }

        // Support references.
        var made = Enumerable.Range(0, 500).Select(_ => SupportReference.New()).ToList();
        Expect(made.All(r => r.Length == 9 && r.StartsWith("SW-") && r[3..].All(SupportReference.Alphabet.Contains)), "SW- and six unambiguous characters");
        Expect(made.All(r => !r[3..].Any(c => c is '0' or 'O' or '1' or 'I' or 'L')), "no look-alike characters");
        Expect(made.Distinct().Count() > 495, "references are random");
        Expect(SupportReference.Normalise("sw-abc234") == "SW-ABC234" && SupportReference.Normalise(" SW ABC 234 ") == "SW-ABC234" && SupportReference.Normalise("abc234") == "SW-ABC234",
            "case, spaces and a missing dash are forgiven");
        Expect(SupportReference.Normalise("SW-ABC23") is null && SupportReference.Normalise("SW-ABC2O4") is null && SupportReference.Normalise("SW-ABC2345") is null
            && SupportReference.Normalise(null) is null && SupportReference.Normalise("'; drop") is null, "anything else isn't a reference");

        // What to do.
        StandingFacts F(bool required = false, bool phone = true, AgeStatus age = AgeStatus.Attested, bool dob = true, DobCheck check = DobCheck.None) => new(required, phone, age, dob, check);
        Expect(StandingPolicy.ActionsRequired(F()).Count == 0, "nothing to do");
        Expect(StandingPolicy.ActionsRequired(F(required: true, phone: false)).SequenceEqual(["phone"]), "a phone when the gate is on");
        Expect(StandingPolicy.ActionsRequired(F(required: false, phone: false)).Count == 0, "no phone when the gate is off");
        Expect(StandingPolicy.ActionsRequired(F(age: AgeStatus.NeedsVerification, dob: false, required: true, phone: false)).SequenceEqual(["age-check", "date-of-birth", "phone"]),
            "an age check first, then the date of birth, then the phone");
        Expect(StandingPolicy.ActionsRequired(F(check: DobCheck.CorrectionNeeded)).SequenceEqual(["dob-correction"]), "the D166 correction");
        Expect(StandingPolicy.ActionsRequired(F(check: DobCheck.ManualReview)).Count == 0, "a manual review needs nothing from the member");

        // Sanction states and appeal deadlines.
        var now = new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);
        var member = Guid.NewGuid();
        Sanction S(SanctionKind kind, int startedDaysAgo, int? endsInDays, bool lifted = false) => new()
        {
            Id = Guid.NewGuid(), AccountId = member, Kind = kind, StartsAt = now.AddDays(-startedDaysAgo), EndsAt = endsInDays is { } e ? now.AddDays(e) : null,
            LiftedAt = lifted ? now.AddDays(-1) : null, Reason = "Fictional.",
        };
        Expect(StandingPolicy.SanctionState(S(SanctionKind.Restriction, 1, 2), now) == "in-force", "in force");
        Expect(StandingPolicy.SanctionState(S(SanctionKind.Restriction, 5, -1), now) == "ended", "ended by time");
        Expect(StandingPolicy.SanctionState(S(SanctionKind.Suspension, 5, 5, lifted: true), now) == "lifted", "lifted");
        Expect(StandingPolicy.SanctionState(S(SanctionKind.Restriction, -2, 5), now) == "scheduled", "starts later");
        Expect(StandingPolicy.SanctionState(S(SanctionKind.Warning, 1, null), now) == "issued", "a warning is issued, not in force");
        Expect(StandingPolicy.AppealBy(S(SanctionKind.Restriction, 1, 2), false, now) == now.AddDays(29), "30 days from the start to appeal");
        Expect(StandingPolicy.AppealBy(S(SanctionKind.Restriction, 1, 2), true, now) is null, "one appeal per action");
        Expect(StandingPolicy.AppealBy(S(SanctionKind.Termination, 31, null), false, now) is null, "too late after 30 days");
        Expect(StandingPolicy.AppealBy(S(SanctionKind.Suspension, 1, 5, lifted: true), false, now) is null, "nothing to appeal once lifted");

        // Wording.
        Expect(StandingText.State("active") == "In good standing" && StandingText.State("terminated") == "Closed", "states");
        Expect(StandingText.Kind("photo-not-approved") == "A photo wasn't approved", "a refused photo, blocked or rejected alike");
        var report = new StandingReport(Guid.NewGuid(), "SW-ABC234", "profile", "harassment", "closed", "actionTaken", now, false);
        Expect(StandingText.Report(report) == "SW-ABC234 · 29 Oct 2026: Closed. We took action on this report.", "a report line with its reference");
        Expect(StandingText.Decision(new StandingDecision("restriction", now, "in-force", Guid.NewGuid(), null, now.AddDays(30))) == "29 Oct 2026 · Restriction (in force) · you can appeal until 28 Nov 2026",
            "a decision with its appeal deadline");
        Expect(StandingText.Sanction(new StandingSanction(Guid.NewGuid(), "suspension", "x", now, null, null, null)) == "Suspension since 29 Oct 2026, until lifted", "an open-ended suspension");
        Expect(StandingText.Dob(new StandingVerification(true, true, false, "attested", true, false, "correctionNeeded", "verified")).StartsWith("Differs"), "the correction prompt");

        Console.WriteLine($"PASS: account standing ({checks} checks).");
    }
}
