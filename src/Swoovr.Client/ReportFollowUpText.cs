namespace Swoovr.Client;

/// <summary>Always offer support after every report; no sexual-content classifier is required.</summary>
public static class ReportFollowUpText
{
    public const string Confirmation = "Your report was submitted. You can review any general outcome in Notices and reports. Account standing has its support reference.";
    public const string SafetyHelp = "If this has affected you, Safety & Support has independent support resources. Choose your country to see available resources.";
    public const string SafetyPath = "/safety";
    public const string ComplaintHelp = "To complain about how your report was handled or about Swoovr's compliance with the Online Dating Code, contact Swoovr support. Include your report reference if you have one.";
    public const string Notification = "There is an update to a report you submitted. Open Notices and reports to review it.";
    public static string ComplaintUrl(string reference) => StandingText.SupportLink(reference);
    public static string ComplaintEmail => StandingText.SupportEmail;
    public static string Availability => StandingText.SupportHelp;
}
