namespace Swoovr.Domain;

/// <summary>Restricted internal action receipt. No text/reason/reporter/contact value. Never serialize this entity to clients.</summary>
public sealed class ModerationTransparencyFact
{
    public Guid Id { get; set; }
    // Unique(SourceKind,SourceId); committed atomically with the real action, never a guessed historical event.
    public string SourceKind { get; set; } = ""; // sanction, content-removal, media-decision
    public Guid SourceId { get; set; }
    public Guid? SourceCaseId { get; set; }
    public Guid SubjectAccountId { get; set; } // restricted distinct-account counting, independent retention; no cascade FK
    public DateTimeOffset ActionAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Action { get; set; } = ""; // termination, content-removal; not mere candidate/no-action
    public string HomeCountryAtAction { get; set; } = "unknown"; // AU / other / unknown, never location details
    public string CountryProvenance { get; set; } = "unknown";
    public string ReviewedPolicyBasis { get; set; } = "unknown";
    public bool BasisReviewed { get; set; }
    public string DetectionMechanism { get; set; } = "unknown";
    public string EnforcementMechanism { get; set; } = "unknown";
    public string AutomationDefinition { get; set; } = "none";
    public string Initiative { get; set; } = "unknown"; // reported, own-initiative, unknown
    public string Environment { get; set; } = "unknown"; // production, local-synthetic, unknown
}

public sealed record ModerationReportingPeriod(DateTimeOffset From,DateTimeOffset Until,DateOnly PublicationBenchmark);
public sealed record ModerationMetric(string Status,long? Count);
public static class ModerationTransparencyPolicy
{
    // DEFAULT bounded taxonomy. Legal/policy review may version it prospectively; no report-category inference.
    public static readonly string[] Bases = ["unknown","harassment","threats-violence","sexual-abuse","child-safety","fraud-scam","hate-discrimination","privacy","other-reviewed"];
    public static readonly string[] Detection = ["unknown","member-report","staff-review","local-text-rules","image-screening"];
    public static readonly string[] Enforcement = ["unknown","human","automated-screening"];
    public static readonly string[] Initiative = ["unknown","reported","own-initiative"];
    public static readonly string[] AutomationDefinitions = ["none","unknown","local-text-rules-v1","image-screening-unversioned"];
    // DEFAULT UTC cutoffs, disclosed explicitly. Change before integration if another reviewed reporting timezone is required.
    public static ModerationReportingPeriod Period(int endingYear,DateTimeOffset now)
    {
        if(endingYear<2||endingYear>9998)throw new ArgumentOutOfRangeException(nameof(endingYear));
        var from=new DateTimeOffset(endingYear-1,7,1,0,0,0,TimeSpan.Zero);
        var until=new DateTimeOffset(endingYear,7,1,0,0,0,TimeSpan.Zero);
        if(until>now)throw new ArgumentOutOfRangeException(nameof(endingYear),"Only completed reporting periods are available.");
        return new(from,until,new(endingYear,8,31));
    }
    public static string Country(string? country,string provenance) => provenance=="member-declared-home-at-action"
        ? country=="AU"?"declared-au":country=="other"?"declared-other":"unknown" : "unknown";
    public static string Basis(string? basis,bool reviewed) => reviewed&&basis is not null&&Bases.Contains(basis)?basis:"unknown";
    public static string Bounded(string? value,string[] allowed) => value is not null&&allowed.Contains(value)?value:"unknown";
    // DEFAULT conservative anti-differencing: if any exported measured cell is small, suppress ALL numeric cells in this report.
    // No arbitrary filters/cohorts, no raw CSV/unsuppressed alternate endpoint. A formal disclosure policy can replace this.
    public static long?[] Disclose(IEnumerable<long> counts)
    {
        var values=counts.ToArray();
        if(values.Any(n=>n<0))throw new ArgumentOutOfRangeException(nameof(counts));
        return values.Any(n=>n is >0 and <5)?values.Select(_=>(long?)null).ToArray():values.Select(n=>(long?)n).ToArray();
    }
}

/// <summary>Immutable aggregate-only annual revision; survives independent source/receipt expiry. No source IDs or per-person data.</summary>
public sealed class ModerationTransparencySnapshot
{
    public Guid Id { get; set; }
    public int EndingYear { get; set; }
    public int Revision { get; set; }
    public string DefinitionVersion { get; set; } = "benchmark-v1";
    public DateTimeOffset GeneratedAt { get; set; }
    public DateTimeOffset FinalizedAt { get; set; }
    public string Fingerprint { get; set; } = "";
    public string AggregateJson { get; set; } = ""; // ONLY serialized ModerationTransparencyExport from trusted projection
    // DEFAULT append a reviewed revision/correction, never overwrite historical published figures.
}
