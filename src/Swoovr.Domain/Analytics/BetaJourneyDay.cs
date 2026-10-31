namespace Swoovr.Domain;

/// <summary>Minimal first-party daily observations; never filters, traits, counterpart IDs or content.</summary>
public sealed class BetaJourneyDay
{
    public Guid AccountId { get; set; }
    public DateOnly Day { get; set; }
    public string Dataset { get; set; } = "unknown";
    public bool ExploreSucceeded { get; set; }
    public bool ExploreHasPeer { get; set; }
}

public static class BetaScorecardPrivacy
{
    public static bool Suppress(IEnumerable<(long? Numerator,long? Denominator)> cells) =>
        cells.Any(c=>c.Numerator is >0 and <5||c.Denominator is >0 and <5||c.Denominator is{}d&&c.Numerator is{}n&&d>=n&&d-n is >0 and <5);
    public static decimal? Percentage(long numerator,long denominator)
    {
        if(numerator<0||denominator<0||numerator>denominator)throw new ArgumentOutOfRangeException(nameof(numerator));
        return denominator==0?null:Math.Round(100m*numerator/denominator,1,MidpointRounding.AwayFromZero);
    }
}
