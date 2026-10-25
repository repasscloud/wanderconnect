namespace Swoovr.Domain;

/// <summary>
/// A configurable profile option list (D138): genders and orientations are data with stable ids, labels and order.
/// Kind is "gender" or "orientation".
/// </summary>
public sealed class ProfileOption
{
    public string Kind { get; set; } = "";
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Available { get; set; } = true;
}

/// <summary>
/// The member's identity details (D138), each separate and optional except where Dating needs them: gender (with an
/// optional description for "another gender"), pronouns, orientation, what is shown publicly, and Dating's "show me".
/// Orientation never decides who someone sees. Hiding a detail never removes the member from reciprocal matching.
/// </summary>
public sealed class MemberIdentity
{
    public Guid AccountId { get; set; }
    public string? GenderId { get; set; }
    public string? GenderDescription { get; set; }
    public string? Pronouns { get; set; }
    public string? OrientationId { get; set; }
    public bool ShowGender { get; set; } = true;
    public bool ShowOrientation { get; set; }
    /// <summary>D139: the calculated age shows by default and can be hidden; filters still use the private date of birth.</summary>
    public bool ShowAge { get; set; } = true;
    /// <summary>Dating "show me": one or more gender ids (D138).</summary>
    public List<string> DatingShowMe { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum ChipMatch { Any, All }
public enum VisitorVisibility { Both, Current, Upcoming, Neither }
public enum ExploreSort { Recent, Compatibility }

/// <summary>
/// One member's filters for one mode (D137). Free: age range, required chips (any or all), excluded chips (reciprocal,
/// D140), ID Verified only, visitor visibility. Plus: distance within the market and compatibility sorting. Height, body
/// type, lifestyle, education and profession wait for their profile fields; saved searches come later.
/// </summary>
public sealed class ModeFilter
{
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public List<Guid> RequiredChips { get; set; } = [];
    public ChipMatch RequiredMatch { get; set; }
    public List<Guid> ExcludedChips { get; set; } = [];
    public bool IdVerifiedOnly { get; set; }
    public VisitorVisibility Visitors { get; set; }
    /// <summary>Plus: distance between home cities, within the market.</summary>
    public int? MaxDistanceKm { get; set; }
    /// <summary>Plus: compatibility (shared chips first) instead of most recent.</summary>
    public ExploreSort Sort { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum FilterError { None, AgeRange, UnknownChip, TooManyChips, ChipBothWays, PlusRequired, Distance, UnknownGender, ShowMeRequired, TextTooLong }

/// <summary>
/// What one person looks like to the matcher: facts from application records only. Age comes from the private date of
/// birth (never shown unless the person allows it); chips are this mode's chips.
/// </summary>
/// <param name="Bot">Official bots (Q11) take part only where an admin authorised them; they have no date of birth or gender, so the
/// Dating "show me" check doesn't apply to them (DEFAULT, D157). Excluded chips still do.</param>
public sealed record MatchFacts(int? Age, IReadOnlySet<Guid> Chips, IReadOnlySet<Guid> Excluded, bool IdVerified, string? GenderId,
    IReadOnlyCollection<string> ShowMe, double? Latitude, double? Longitude, bool Bot = false);

/// <summary>
/// Discovery matching (D137–D140): reciprocal rules that decide whether two people may find and contact each other in
/// a mode, and the viewer's own filters that narrow only their results. Market, safety and reciprocity rules always win
/// over filters; missing attributes don't exclude unless a filter requires them; a chip alone never excludes anyone.
/// </summary>
public static class MatchPolicy
{
    public const int MaxRequiredChips = 5, MaxExcludedChips = 10, MinAge = AgePolicy.Adult, MaxAge = AgePolicy.MaxAge, MaxDistanceKm = 500;
    public const int TextMax = 40;

    /// <summary>
    /// Whether two people may see and contact each other in a mode at all (both directions): no chip either one excluded
    /// is held by the other (D140), and in Dating each one's gender is in the other's "show me" (D138).
    /// </summary>
    public static bool Reciprocal(string modeId, MatchFacts a, MatchFacts b)
    {
        if (a.Excluded.Overlaps(b.Chips) || b.Excluded.Overlaps(a.Chips)) return false;
        if (modeId != ConnectionModeIds.Dating || a.Bot || b.Bot) return true;
        return a.GenderId is not null && b.GenderId is not null && a.ShowMe.Contains(b.GenderId) && b.ShowMe.Contains(a.GenderId);
    }

    /// <summary>Whether a member has what Dating needs to take part (D138, D139): a gender and at least one "show me".</summary>
    public static bool DatingReady(MatchFacts facts) => facts.GenderId is not null && facts.ShowMe.Count > 0;

    /// <summary>
    /// The viewer's own filters (narrow only their results). Plus-only filters count only when the viewer holds Plus,
    /// so a lapsed Plus never hides people with a stale filter.
    /// </summary>
    public static bool FilterAllows(ModeFilter? filter, MatchFacts viewer, MatchFacts person, PlanTier tier)
    {
        if (filter is null) return true;
        if (filter.MinAge is { } min && (person.Age is null || person.Age < min)) return false;
        if (filter.MaxAge is { } max && (person.Age is null || person.Age > max)) return false;
        if (filter.RequiredChips.Count > 0)
        {
            var hits = filter.RequiredChips.Count(person.Chips.Contains);
            if (filter.RequiredMatch == ChipMatch.All ? hits < filter.RequiredChips.Count : hits == 0) return false;
        }
        if (filter.IdVerifiedOnly && !person.IdVerified) return false;
        if (tier >= PlanTier.Plus && filter.MaxDistanceKm is { } km)
        {
            if (DistanceKm(viewer, person) is not { } d || d > km) return false;
        }
        return true;
    }

    /// <summary>Shared chips, the beta's first compatibility indicator (D140): ranks, never excludes.</summary>
    public static int Shared(MatchFacts a, MatchFacts b) => a.Chips.Count(b.Chips.Contains);

    public static double? DistanceKm(MatchFacts a, MatchFacts b) =>
        a.Latitude is { } la && a.Longitude is { } lo && b.Latitude is { } lb && b.Longitude is { } lob ? PlaceNames.DistanceKm(la, lo, lb, lob) : null;

    /// <summary>Checks a filter update against the mode's chips and the member's tier (D137: definitions and entitlements are server-side).</summary>
    public static FilterError Check(ModeFilter filter, IReadOnlySet<Guid> modeChips, PlanTier tier)
    {
        if (filter.MinAge is < MinAge or > MaxAge || filter.MaxAge is < MinAge or > MaxAge || filter.MinAge > filter.MaxAge) return FilterError.AgeRange;
        if (filter.RequiredChips.Count > MaxRequiredChips || filter.ExcludedChips.Count > MaxExcludedChips) return FilterError.TooManyChips;
        if (filter.RequiredChips.Concat(filter.ExcludedChips).Any(c => !modeChips.Contains(c))) return FilterError.UnknownChip;
        if (filter.RequiredChips.Intersect(filter.ExcludedChips).Any()) return FilterError.ChipBothWays;
        if (filter.MaxDistanceKm is < 1 or > MaxDistanceKm) return FilterError.Distance;
        if (tier < PlanTier.Plus && (filter.MaxDistanceKm is not null || filter.Sort != ExploreSort.Recent)) return FilterError.PlusRequired;
        return FilterError.None;
    }

    /// <summary>Checks identity details against the option lists (D138).</summary>
    public static FilterError Check(MemberIdentity identity, IReadOnlySet<string> genders, IReadOnlySet<string> orientations)
    {
        if (identity.GenderId is { } g && !genders.Contains(g)) return FilterError.UnknownGender;
        if (identity.OrientationId is { } o && !orientations.Contains(o)) return FilterError.UnknownGender;
        if (identity.DatingShowMe.Any(s => !genders.Contains(s))) return FilterError.UnknownGender;
        if (identity.GenderDescription is { Length: > TextMax } || identity.Pronouns is { Length: > TextMax }) return FilterError.TextTooLong;
        return FilterError.None;
    }

    /// <summary>
    /// Visitor visibility (D137) for the destination feed: whether a traveller whose stay has (current) or hasn't
    /// (upcoming) started shows to this viewer.
    /// </summary>
    public static bool ShowsVisitor(VisitorVisibility visibility, bool started) => visibility switch
    {
        VisitorVisibility.Both => true,
        VisitorVisibility.Current => started,
        VisitorVisibility.Upcoming => !started,
        _ => false,
    };
}
