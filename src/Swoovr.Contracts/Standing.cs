namespace Swoovr.Contracts;

/// <summary>
/// Account standing (#50): one page for the member (and the same view for staff helping them). State is active,
/// restricted, suspended or terminated. Never reporter identities, reports about the member, internal flags, ban matching,
/// fraud signals or legal holds.
/// </summary>
public sealed record AccountStandingView(string State, StandingVerification Verification, IReadOnlyList<StandingAction> Actions,
    IReadOnlyList<StandingSanction> InForce, IReadOnlyList<StandingDecision> Decisions, IReadOnlyList<StandingReport> Reports);

/// <param name="DobCheck">none, correctionNeeded or manualReview (D166).</param>
/// <param name="IdStatus">notStarted, pending, verified, needsAction, rejected or expired (D126).</param>
public sealed record StandingVerification(bool EmailVerified, bool PhoneVerified, bool PhoneRequired, string AgeStatus, bool DateOfBirthSet, bool DateOfBirthVerified,
    string DobCheck, string IdStatus);

/// <summary>Something the member must do: phone, age-check, date-of-birth or dob-correction, with the page that does it.</summary>
public sealed record StandingAction(string Code, string Link);

/// <summary>A sanction in force: the kind, the staff-written reason the notice already shows, when it ends, and the appeal.</summary>
public sealed record StandingSanction(Guid Id, string Kind, string Reason, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, DateTimeOffset? AppealBy, string? AppealStatus);

/// <summary>
/// A decision about the member, newest first. Kind: warning, restriction, suspension, termination (State issued for a warning, else in-force,
/// ended, lifted or scheduled), dob-verified, dob-not-verified, or photo-not-approved.
/// </summary>
public sealed record StandingDecision(string Kind, DateTimeOffset At, string? State, Guid? SanctionId, string? AppealStatus, DateTimeOffset? AppealBy);

/// <summary>One of the member's own reports with its support reference (SW-XXXXXX) and what the reporter may know.</summary>
public sealed record StandingReport(Guid Id, string Reference, string SubjectType, string Category, string Status, string? Outcome, DateTimeOffset CreatedAt, bool Serious);

/// <summary>What support finds from a reference: the report, its case and who's involved (by account id only).</summary>
public sealed record SupportLookupView(string Reference, Guid ReportId, Guid CaseId, string CaseStatus, string Queue, string Priority, string Category, bool Serious,
    DateTimeOffset CreatedAt, Guid ReporterAccountId, Guid SubjectAccountId);
