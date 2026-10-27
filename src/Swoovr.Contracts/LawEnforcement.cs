namespace Swoovr.Contracts;

// Restricted law-enforcement case management (police-contact procedure §2–§14, D195). Admin API only, `legal` role only;
// never on the member BFF allow-list. Staff implicated in a case are excluded from it.

/// <summary>
/// A new case. Channel: email, phone, letter, portal or in-person. Urgency: critical, urgent or standard. Subjects are
/// member account ids (up to 20). The first request is required. Contact details are as the requester gave them; they are
/// never used to verify the requester (§3.2).
/// </summary>
public sealed record NewLeCase(string? Channel, DateTimeOffset? ReceivedAt, string? Urgency, string? Agency, string? AgencyCountry, string? AgencyReference,
    string? OfficerName, string? OfficerRank, string? OfficerServiceNumber, string? OfficerEmail, string? OfficerPhone, DateTimeOffset? StatutoryDeadline,
    bool Privileged, bool Confidential, List<Guid>? Subjects, NewLeRequest? Request);

/// <summary>Kind: preservation, disclosure, emergency-disclosure, assistance or other.</summary>
public sealed record NewLeRequest(string? Kind, string? Description, DateTimeOffset? From, DateTimeOffset? Until);

/// <summary>Timer: response-due, response-overdue, update-due, update-overdue or closed; Due is when.</summary>
public sealed record LeCaseSummary(Guid Id, string Reference, string Status, string Urgency, string Agency, string AgencyCountry, DateTimeOffset ReceivedAt,
    Guid OwnerId, bool Verified, bool Privileged, bool Confidential, string Timer, DateTimeOffset? Due, int OpenRequests, int Holds);

public sealed record LeRequestView(Guid Id, string Kind, string Description, DateTimeOffset ReceivedAt, DateTimeOffset? From, DateTimeOffset? Until,
    string Status, string? DecisionReason, Guid? DecidedBy, DateTimeOffset? DecidedAt, bool BreakGlass, DateTimeOffset? RetrospectiveDue,
    Guid? RetrospectiveBy, DateTimeOffset? RetrospectiveAt);

public sealed record LeCasePersonView(Guid AccountId, string Kind, DateTimeOffset AddedAt);

public sealed record LeNotificationView(Guid Id, Guid AccountId, string Decision, string Reason, DateTimeOffset? ReconsiderAt, Guid DecidedBy,
    DateTimeOffset DecidedAt, bool ReconsiderationDue);

public sealed record LeTransferView(Guid Id, Guid ExportId, string PackageSha256, string Recipient, string Method, Guid SentBy, DateTimeOffset SentAt,
    DateTimeOffset? ReceiptAt, string? ReceiptReference);

public sealed record LeChecklistItem(string Item, bool Done);

public sealed record LeCaseDetail(Guid Id, string Reference, string Status, string Channel, DateTimeOffset ReceivedAt, string Urgency, string Agency,
    string AgencyCountry, string? AgencyReference, string OfficerName, string? OfficerRank, string? OfficerServiceNumber, string? OfficerEmail,
    string? OfficerPhone, Guid OwnerId, DateTimeOffset CreatedAt, string? VerificationSource, string? VerificationEvidence, Guid? VerifiedBy,
    DateTimeOffset? VerifiedAt, string? LegalAssessment, string? LegalInstrument, bool? Mandatory, DateTimeOffset? AssessedAt, string? App65Note,
    DateTimeOffset? App65At, bool Privileged, bool Confidential, string? ConfidentialityAssessment, DateTimeOffset? StatutoryDeadline,
    DateTimeOffset? FirstResponseAt, DateTimeOffset? LastUpdateAt, string Timer, DateTimeOffset? Due, DateTimeOffset? ClosedAt, string? ClosureNote,
    DateTimeOffset? RetainUntil, List<LeRequestView> Requests, List<LeCasePersonView> People, List<LeNotificationView> Notifications,
    List<LeTransferView> Transfers, List<LegalHoldSummary> Holds, List<LeChecklistItem> Closure, List<DisclosureView> Disclosures);

/// <summary>Source: official-directory, agency-switchboard, agency-portal or in-person (never the requester's own email).</summary>
public sealed record LeVerification(string? Source, string? Evidence);

/// <summary>Instrument: warrant, court-order, subpoena, statutory-notice, app-6.2e, serious-threat, consent or none.</summary>
public sealed record LeAssessment(string? Assessment, string? Instrument, bool? Mandatory, string? ConfidentialityAssessment);

public sealed record LeNote(string? Note);

public sealed record LeFlags(bool Privileged, bool Confidential);

/// <summary>Kind: acknowledgement (the first response) or update.</summary>
public sealed record LeResponse(string? Kind);

/// <summary>Outcome: preserved, disclosed, partly-disclosed, refused, withdrawn or redirected. BreakGlass: an emergency approval
/// by the on-call officer without separation, reviewed afterwards by another officer.</summary>
public sealed record LeDecision(string? Outcome, string? Reason, bool BreakGlass);

/// <summary>Kind: subject (a member) or implicated-staff (a staff account, then excluded from the case).</summary>
public sealed record LePerson(Guid AccountId, string? Kind);

public sealed record LeOwner(Guid OwnerId);

/// <summary>Decision: notified, delay or withhold; delay and withhold need ReconsiderAt within six months.</summary>
public sealed record LeNotificationRequest(Guid AccountId, string? Decision, string? Reason, DateTimeOffset? ReconsiderAt);

/// <summary>A downloaded export of a hold serving this case. Method: secure-link, encrypted-media, agency-portal or in-person.</summary>
public sealed record LeTransferRequest(Guid ExportId, string? Recipient, string? Method, DateTimeOffset? SentAt);

public sealed record LeReceipt(string? Reference, DateTimeOffset? ReceivedAt);

/// <summary>ConfirmObligations: the officer confirms outstanding legal obligations and follow-up duties were checked (§13).</summary>
public sealed record LeClose(string? Note, bool ConfirmObligations);

/// <summary>Live and overdue counts for the legal home page.</summary>
public sealed record LeOverview(int Open, int Overdue, int ReconsiderationsDue, int RetrospectivesDue);

/// <summary>
/// The annual transparency aggregate (§13): counts only, confidential cases left out, and any count from 1 to 4 shown as
/// null ("fewer than 5").
/// </summary>
public sealed record TransparencyReport(int Year, int? Cases, int? Emergencies, Dictionary<string, int?> RequestKinds, Dictionary<string, int?> Outcomes,
    Dictionary<string, int?> AgencyCountries, int? HoldsPlaced, int? AccountsAffected, int SuppressedBelow);
