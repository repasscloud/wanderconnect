namespace Swoovr.Contracts;
// Aggregate-only body: no arbitrary keys, source/action/case/account IDs, text, reason, country detail or reviewer identity.
public sealed record ModerationTransparencyHeader(int EndingYear,DateTimeOffset FromInclusive,DateTimeOffset UntilExclusive,
    string TimeZone,DateOnly PublicationBenchmark,DateTimeOffset GeneratedAt,string Scope,string Status);
public sealed record ModerationTransparencyCell(string Metric,string Category,long? Count,string Status);
public sealed record ModerationTransparencyQuality(string CoverageStatus,DateTimeOffset? RecordingBeganAt,
    DateTimeOffset SnapshotAt,IReadOnlyList<string> Limitations);
public sealed record ModerationAutomationDisclosure(string Definition,string Purpose,string HumanReview,
    string AccuracyStatus,double? FalsePositiveRate,double? FalseNegativeRate,string ErrorStatus);
public sealed record ModerationTransparencyExport(ModerationTransparencyHeader Header,ModerationTransparencyQuality DataQuality,
    IReadOnlyList<ModerationTransparencyCell> Counts,IReadOnlyList<ModerationAutomationDisclosure> Automation,
    string TrainingStatus,string OwnInitiativeCoverage);

public sealed record ModerationReportingChoice(string? PolicyBasis=null,string SourceKind="unknown",Guid? SourceId=null);
