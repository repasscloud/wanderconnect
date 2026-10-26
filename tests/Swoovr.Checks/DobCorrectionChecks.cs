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

        Console.WriteLine($"PASS: date-of-birth correction ({checks}).");
    }
}
