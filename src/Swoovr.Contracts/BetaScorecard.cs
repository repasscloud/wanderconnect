namespace Swoovr.Contracts;
public sealed record BetaScorecardMetric(string Key,string Definition,string Status,long? Numerator,long? Denominator,decimal? Percentage);
public sealed record BetaScorecardResult(DateOnly FromInclusive,DateOnly UntilExclusive,string Dataset,string Collection,string Coverage,
    IReadOnlyList<BetaScorecardMetric> Metrics,IReadOnlyList<string> Limitations);
