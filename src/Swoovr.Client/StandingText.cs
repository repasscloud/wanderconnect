using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>Account standing wording shared by the web, the app and the admin app's member view (#50).</summary>
public static class StandingText
{
    public const string Title = "Account standing";
    public const string Intro = "Your verification, anything you need to do, actions on your account and the reports you've made.";
    public const string SupportUrl = "https://swoovr.com/support";
    public const string SupportEmail = "support@swoovr.com";
    public const string SupportHelp = "Support requests are prioritised by urgency. No fixed response or resolution time is promised.";
    public static string SupportLink(string reference) => SupportUrl + "?reference=" + Uri.EscapeDataString(reference);
    /// <summary>CONFIRMED Q86/D214: the official web and email channels.</summary>
    public const string ReferenceHelp = "Quote the reference if you contact Swoovr support about a report.";
    public const string NothingInForce = "Nothing is limiting your account.";
    public const string NoDecisions = "No decisions about your account.";
    public const string NoReports = "You haven't reported anything.";
    public const string Privacy = "We never show who reported anyone, or any report made about you.";

    public static string State(string state) => state switch
    {
        "restricted" => "Restricted",
        "suspended" => "Suspended",
        "terminated" => "Closed",
        _ => "In good standing",
    };

    public static string Action(string code) => code switch
    {
        "phone" => "Verify your mobile number",
        "age-check" => "Confirm your age with an ID check",
        "dob-correction" => "Correct your date of birth to match your ID",
        _ => "Add your date of birth",
    };

    public static string Age(string status) => status switch
    {
        "verified" => "Age verified with ID",
        "adultOverride" => "Adult (confirmed by Swoovr)",
        "needsVerification" => "Age check needed",
        "underage" => "Under 18",
        "unconfirmed" => "Not confirmed",
        _ => "Confirmed by you (18+)",
    };

    public static string Id(string status) => status switch
    {
        "verified" => "ID Verified",
        "pending" => "ID check in progress",
        "needsAction" => "ID check needs action",
        "rejected" => "ID check not approved",
        "expired" => "ID check expired",
        _ => "Not verified (optional)",
    };

    public static string Dob(StandingVerification v) => !v.DateOfBirthSet ? "Not added"
        : v.DobCheck == "correctionNeeded" ? "Differs from your ID: please correct it"
        : v.DobCheck == "manualReview" ? "Being checked by Swoovr"
        : v.DateOfBirthVerified ? "Matches your ID" : "Added";

    public static string Kind(string kind) => kind switch
    {
        "warning" => "Warning",
        "restriction" => "Restriction",
        "suspension" => "Suspension",
        "termination" => "Account closed",
        "dob-verified" => "Date of birth confirmed",
        "dob-not-verified" => "Date of birth couldn't be confirmed",
        "profile-text-removed" => "Profile text removed by Swoovr",
        "message-removed" => "A message removed by Swoovr",
        "photo-not-approved" => "A photo wasn't approved",
        _ => "Decision",
    };

    public static string SanctionState(string? state) => state switch
    {
        "in-force" => "in force",
        "issued" => "issued",
        "lifted" => "lifted",
        "scheduled" => "starts later",
        "ended" => "ended",
        _ => "",
    };

    public static string Decision(StandingDecision d) =>
        $"{d.At:d MMM yyyy} · {Kind(d.Kind)}{(d.State is { } s ? $" ({SanctionState(s)})" : "")}"
        + (d.AppealStatus is { } a ? $" · appeal {MemberText.AppealStatus(a)}" : d.AppealBy is { } by ? $" · you can appeal until {by:d MMM yyyy}" : "");

    public static string Sanction(StandingSanction s) =>
        $"{Kind(s.Kind)} since {s.StartsAt:d MMM yyyy}" + (s.EndsAt is { } end ? $", until {end:d MMM yyyy}" : s.Kind == "termination" ? "" : ", until lifted");

    public static string Report(StandingReport r) =>
        $"{r.Reference} · {r.CreatedAt:d MMM yyyy}{(r.Serious ? " (serious incident)" : "")}: {MemberText.ReportStatus(r.Status)}" + r.Outcome switch
        {
            "actionTaken" => ". We took action on this report.",
            "noBreachFound" => ". We reviewed it and didn't find a breach of our rules.",
            _ => "",
        };
}
