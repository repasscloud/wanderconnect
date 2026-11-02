using Swoovr.Domain;

/// <summary>
/// Owner answer to Q58 (issue #33, D166): an approved ID check whose date of birth differs opens one correction; the
/// corrected date is compared with Didit's decision again; a match verifies it, anything else goes to an admin's review.
/// </summary>
static class DobCorrectionChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Date-of-birth correction check failed: " + what); checks++; }
        DateOnly D(string s) => DateOnly.Parse(s);

        Expect(DobCorrectionPolicy.AfterApproval(D("1990-01-01"), D("1990-01-01")) == DobCheck.None, "the same date: verified, nothing to do");
        Expect(DobCorrectionPolicy.AfterApproval(D("1990-01-01"), D("1991-01-01")) == DobCheck.CorrectionNeeded, "a different year: one correction");
        Expect(DobCorrectionPolicy.AfterApproval(D("1990-01-01"), D("1990-01-02")) == DobCheck.CorrectionNeeded, "a day apart is still different");
        Expect(DobCorrectionPolicy.AfterApproval(null, D("1990-01-01")) == DobCheck.CorrectionNeeded, "no date on the profile: correct it");
        Expect(DobCorrectionPolicy.AfterApproval(D("1990-01-01"), null) == DobCheck.ManualReview, "Didit gave no date: straight to review");
        Expect(DobCorrectionPolicy.AfterCorrection(D("1991-01-01"), D("1991-01-01")) == DobCorrectionResult.Verified, "a corrected date that matches is verified");
        Expect(DobCorrectionPolicy.AfterCorrection(D("1991-01-02"), D("1991-01-01")) == DobCorrectionResult.ManualReview, "still different: an admin reviews it");
        Expect(DobCorrectionPolicy.AfterCorrection(D("1991-01-01"), null) == DobCorrectionResult.ManualReview, "Didit no longer gives a date: review");
        Expect(DobCorrectionPolicy.OrdinaryChangeAllowed(DobCheck.None), "the ordinary form works with nothing open");
        Expect(!DobCorrectionPolicy.OrdinaryChangeAllowed(DobCheck.CorrectionNeeded) && !DobCorrectionPolicy.OrdinaryChangeAllowed(DobCheck.ManualReview),
            "the prompt is the only way while a correction or review is open");
        Expect(Enum.GetValues<DobCheck>().Length == 3 && Enum.GetValues<DobReviewOutcome>().Length == 2, "three states, two review outcomes");
        // Nothing about the document's date is held: the review has no date fields.
        Expect(!typeof(DobReview).GetProperties().Any(p => p.PropertyType == typeof(DateOnly) || p.PropertyType == typeof(DateOnly?)), "a review stores no date of birth");
        var now = DateTimeOffset.Parse("2026-11-02T10:00:00Z");
        var member = Guid.NewGuid();
        var grant = new DobChangeGrant { AccountId = member, CreatedAt = now, ExpiresAt = now.AddHours(24) };
        Expect(!DobChangePolicy.CanSave(null, false, null, member, now), "initial entry requires explicit confirmation");
        Expect(DobChangePolicy.CanSave(null, true, null, member, now), "confirmed first entry allowed");
        Expect(!DobChangePolicy.CanSave(D("1990-01-01"), true, null, member, now), "existing unverified date locked without migration grace");
        Expect(DobChangePolicy.CanSave(D("1990-01-01"), true, grant, member, now), "live member-bound grant allows confirmed correction");
        Expect(!DobChangePolicy.CanSave(D("1990-01-01"), false, grant, member, now), "grant never skips confirmation");
        Expect(!DobChangePolicy.Live(grant, Guid.NewGuid(), now), "wrong member refused");
        Expect(!DobChangePolicy.Live(grant, member, now.AddHours(24)), "expiry boundary refused");
        grant.ConsumedAt = now;
        Expect(!DobChangePolicy.Live(grant, member, now), "consumed grant cannot replay");
        grant.ConsumedAt = null; grant.RevokedAt = now;
        Expect(!DobChangePolicy.Live(grant, member, now), "revoked grant refused");
        Expect(DobChangePolicy.MayGrant(AccountRole.Admin) && !DobChangePolicy.MayGrant(AccountRole.Support) && !DobChangePolicy.MayGrant(AccountRole.Legal), "only Admin capability grants corrections");
        var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var history = DobChangePolicy.Seal(member, D("1990-01-01"), 1, key);
        var keys = new Dictionary<byte, byte[]> { [1] = key };
        Expect(DobChangePolicy.Open(member, keys, history) == D("1990-01-01"), "prior date round-trips encrypted");
        var refused = false;
        try { DobChangePolicy.Open(Guid.NewGuid(), keys, history); } catch (System.Security.Cryptography.CryptographicException) { refused = true; }
        Expect(refused, "encrypted history cannot be moved to another member");


        Console.WriteLine($"PASS: date-of-birth correction ({checks}).");
    }
}
