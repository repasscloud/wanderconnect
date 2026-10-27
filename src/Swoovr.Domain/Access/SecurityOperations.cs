namespace Swoovr.Domain;

public enum IncidentSeverity { Sev1, Sev2, Sev3, Sev4 }

public enum IncidentStatus { Open, Contained, Closed }

/// <summary>
/// A security incident (internal policy §12; procedure §12; #89). Opened by staff or by an automatic alert (unusual
/// identity openings, bulk exports, a broken hash chain). Under the Notifiable Data Breaches scheme a suspected eligible
/// breach is assessed at once and within 30 days; the decision whether it is notifiable is recorded before closing.
/// Summaries hold ids and machine values for automatic incidents; never member content.
/// </summary>
public sealed class SecurityIncident
{
    public Guid Id { get; set; }
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; }
    /// <summary>manual, unusual-identity-access, bulk-export, custody-chain-broken or access-log-chain-broken.</summary>
    public string Kind { get; set; } = "";
    public string Summary { get; set; } = "";
    /// <summary>The staff account the alert is about, when there is one.</summary>
    public Guid? SubjectStaffId { get; set; }
    public Guid? OpenedBy { get; set; }
    public DateTimeOffset DetectedAt { get; set; }
    public DateTimeOffset AssessmentDue { get; set; }
    public bool? Notifiable { get; set; }
    public string? AssessmentReason { get; set; }
    public Guid? AssessedBy { get; set; }
    public DateTimeOffset? AssessedAt { get; set; }
    public DateTimeOffset? ContainedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
}

public static class SecurityOperationsPolicy
{
    /// <summary>NDB: assess a suspected eligible data breach within 30 days (start at once).</summary>
    public static readonly TimeSpan AssessmentWindow = TimeSpan.FromDays(30);
    /// <summary>More than three identity openings by one person in an hour is unusual.</summary>
    public const int IdentityOpeningsPerHour = 3;
    public const int SummaryMax = 2000;
    public static readonly IReadOnlyList<string> Kinds = ["manual", "unusual-identity-access", "bulk-export", "custody-chain-broken", "access-log-chain-broken"];

    /// <summary>What each level means (shown in the admin app and the runbook).</summary>
    public static string Describe(IncidentSeverity severity) => severity switch
    {
        IncidentSeverity.Sev1 => "Critical: confirmed unauthorised access to, or loss or alteration of, identity, evidence or message data; a broken hash chain",
        IncidentSeverity.Sev2 => "High: suspected unauthorised access to restricted data, or a compromised staff account",
        IncidentSeverity.Sev3 => "Medium: an unusual access pattern or bulk export that needs review",
        _ => "Low: a policy deviation with no data exposed",
    };

    /// <summary>Automatic incidents' severity.</summary>
    public static IncidentSeverity SeverityFor(string kind) => kind switch
    {
        "custody-chain-broken" or "access-log-chain-broken" => IncidentSeverity.Sev1,
        "unusual-identity-access" or "bulk-export" => IncidentSeverity.Sev3,
        _ => IncidentSeverity.Sev4,
    };

    public static SecurityIncident Open(string kind, IncidentSeverity severity, string summary, Guid? staff, Guid? openedBy, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), Kind = kind, Severity = severity, Status = IncidentStatus.Open, Summary = summary, SubjectStaffId = staff, OpenedBy = openedBy,
        DetectedAt = now, AssessmentDue = now + AssessmentWindow,
    };

    public static bool Unusual(int openingsInLastHour) => openingsInLastHour > IdentityOpeningsPerHour;

    public static bool AssessmentOverdue(SecurityIncident incident, DateTimeOffset now) => incident.AssessedAt is null && incident.AssessmentDue <= now;

    /// <summary>Closing needs the NDB assessment recorded (notifiable or not, with a reason).</summary>
    public static bool CanClose(SecurityIncident incident) => incident.Status != IncidentStatus.Closed && incident.AssessedAt is not null;

    /// <summary>
    /// Re-seals a value under the target key version (#89 key rotation). Returns null when it is already under that
    /// version. Throws if its own version's key is missing (the value is never dropped).
    /// </summary>
    public static byte[]? Reseal(byte[] sealedValue, IReadOnlyDictionary<byte, byte[]> keys, byte target)
    {
        if (sealedValue.Length == 0 || sealedValue[0] == target) return null;
        return ContactHistoryPolicy.Encrypt(target, keys[target], ContactHistoryPolicy.Decrypt(keys, sealedValue));
    }
}
