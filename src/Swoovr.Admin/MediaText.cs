namespace Swoovr.Admin;

/// <summary>Short staff-facing labels for media review codes and decisions. Codes stay internal and are not displayed.</summary>
public static class MediaText
{
    public static string Reason(string code) => code switch
    {
        "APPROVED_STANDARD" => "Meets photo rules",
        "APPROVED_NON_EXPLICIT" => "Non-explicit, permitted",
        "APPROVED_AFTER_REVIEW" => "Acceptable after review",
        "APPROVED_FALSE_POSITIVE" => "Automated flag cleared",
        "APPROVED_ALBUM_EXPLICIT" => "Adult content: private albums only",
        "REJECT_EXPLICIT_BETA" => "Explicit content not allowed here",
        "REJECT_IMPERSONATION" => "Impersonation or unauthorised use",
        "REJECT_PRIVACY" => "Personal or location information",
        "REJECT_VIOLENCE" => "Graphic violence",
        "REJECT_HATE" => "Hate or extremist imagery",
        "REJECT_SPAM" => "Spam or promotion",
        "REJECT_IMAGE_QUALITY" => "Image quality",
        "REJECT_OTHER" => "Other",
        "ESCALATE_CHILD_SAFETY" => "Child-safety concern",
        "ESCALATE_NON_CONSENSUAL" => "Possible non-consensual image",
        "ESCALATE_OTHER" => "Uncertain serious concern",
        "LEGACY_APPROVE" => "Approved (uncategorised)",
        "LEGACY_REJECT" => "Rejected (uncategorised)",
        "LEGACY_ESCALATE" => "Escalated (uncategorised)",
        _ => code,
    };

    public static string Decision(string decision) => decision switch
    {
        "approve" => "Approved",
        "reject" => "Rejected",
        "escalate" => "Escalated",
        "appeal-upheld" => "Appeal upheld",
        "appeal-approved" => "Approved on appeal",
        "reconsider-approved" => "Approved on reconsideration",
        "appeal-unavailable" => "Appeal unavailable",
        _ => decision,
    };

    /// <summary>Pill tone: approve (outlined dot), reject (solid), escalate, quiet.</summary>
    public static string Tone(string decision) => decision switch
    {
        "approve" or "appeal-approved" or "reconsider-approved" => "approve",
        "reject" or "appeal-upheld" => "reject",
        "escalate" => "escalate",
        _ => "quiet",
    };

    public static string Signal(string signal) => signal;

    public static string Kind(string? purpose) => purpose == "attachment" ? "Chat attachment" : "Profile photo";

    /// <summary>Issue 232: appeals against this Admin's own decision, which a different Admin decides (D298).</summary>
    public static string AwaitingOtherAdmin(int count) => count == 1 ? "1 appeal waiting for another Admin" : $"{count} appeals waiting for another Admin";
}
