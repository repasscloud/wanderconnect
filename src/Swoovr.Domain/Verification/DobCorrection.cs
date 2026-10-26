namespace Swoovr.Domain;

/// <summary>
/// Where a member's date of birth stands after an approved ID check (Q58, issue #33, D166): nothing to do; one
/// correction open (the dates differed); or waiting for an admin's manual review (they still differed after the
/// correction, or Didit gave no date). ID Verified is kept in every state; only the date of birth's mark waits.
/// </summary>
public enum DobCheck { None, CorrectionNeeded, ManualReview }

public enum DobReviewOutcome { Verified, NotVerified }

/// <summary>
/// A manual review of a date of birth that didn't match the verified ID (D166), in the admin queue. Ids, times and the
/// outcome only: never either date; the admin reads the document's date in Didit's console (kept 30 days, D147).
/// </summary>
public sealed class DobReview
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid AttemptId { get; set; }
    /// <summary>"still-different" after the member's correction, or "no-document-date" when Didit gave none.</summary>
    public string Reason { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedBy { get; set; }
    public DobReviewOutcome? Outcome { get; set; }
}

public enum DobCorrectionResult { Verified, ManualReview }

/// <summary>
/// Q58 (owner, 26 October 2026, D166): Swoovr keeps the member's own date of birth and never stores the document's.
/// When an approved check's date differs, the member gets one chance (a one-time prompt) to set their date to the one on
/// their ID; the API then reads Didit's decision again and compares. A match marks the date verified; anything else
/// opens a manual review for an admin. A mismatch is never treated as fraud by itself.
/// </summary>
public static class DobCorrectionPolicy
{
    /// <summary>After an approved check: verified on a match, one correction when Didit reported a different date, review when it reported none.</summary>
    public static DobCheck AfterApproval(DateOnly? profile, DateOnly? document) =>
        IdVerificationPolicy.DateOfBirthMatches(profile, document) ? DobCheck.None
        : document is null ? DobCheck.ManualReview : DobCheck.CorrectionNeeded;

    /// <summary>After the member's one correction, compared with Didit's decision read again.</summary>
    public static DobCorrectionResult AfterCorrection(DateOnly corrected, DateOnly? document) =>
        IdVerificationPolicy.DateOfBirthMatches(corrected, document) ? DobCorrectionResult.Verified : DobCorrectionResult.ManualReview;

    /// <summary>The ordinary date-of-birth form is closed while a correction or a review is open, so the prompt is the only way.</summary>
    public static bool OrdinaryChangeAllowed(DobCheck state) => state == DobCheck.None;
}
