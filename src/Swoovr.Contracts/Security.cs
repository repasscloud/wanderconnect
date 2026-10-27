namespace Swoovr.Contracts;

// Security operations (internal policy §12; procedure §12; #89). Admin API only: the security and legal officer and the root admin.

/// <summary>Severity: sev1 (critical) to sev4 (low). Status: open, contained or closed.</summary>
public sealed record SecurityIncidentView(Guid Id, string Severity, string SeverityMeaning, string Status, string Kind, string Summary, Guid? SubjectStaffId,
    DateTimeOffset DetectedAt, DateTimeOffset AssessmentDue, bool AssessmentOverdue, bool? Notifiable, string? AssessmentReason, DateTimeOffset? AssessedAt,
    DateTimeOffset? ContainedAt, DateTimeOffset? ClosedAt);

public sealed record NewSecurityIncident(string? Severity, string? Summary);

/// <summary>The Notifiable Data Breaches assessment: whether it is an eligible data breach, and why.</summary>
public sealed record IncidentAssessment(bool? Notifiable, string? Reason);
