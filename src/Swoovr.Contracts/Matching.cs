namespace Swoovr.Contracts;

/// <summary>Configurable option lists (D138).</summary>
public sealed record OptionView(string Id, string Label);
public sealed record ProfileOptionsView(IReadOnlyList<OptionView> Genders, IReadOnlyList<OptionView> Orientations);

/// <summary>
/// The member's identity details (D138): gender (description only for "another"), pronouns, orientation, what shows
/// publicly, whether the age shows (D139), and Dating's reciprocal "show me" (gender ids).
/// </summary>
public sealed record IdentityView(string? Gender, string? GenderDescription, string? Pronouns, string? Orientation, bool ShowGender, bool ShowOrientation,
    bool ShowAge, IReadOnlyList<string> DatingShowMe);
public sealed record IdentityUpdate(string? Gender, string? GenderDescription, string? Pronouns, string? Orientation, bool ShowGender, bool ShowOrientation,
    bool ShowAge, IReadOnlyList<string>? DatingShowMe);

/// <summary>
/// One mode's filters (D137). Free: age range, required chips ("any" or "all"), excluded chips (reciprocal, D140), ID
/// Verified only, visitors ("both", "current", "upcoming", "neither"). Plus: MaxDistanceKm and Sort "compatibility".
/// PlusAvailable says whether the member's plan includes the Plus filters (entitlement service, D151).
/// </summary>
public sealed record FilterView(string Mode, int? MinAge, int? MaxAge, IReadOnlyList<Guid> RequiredChips, string RequiredMatch, IReadOnlyList<Guid> ExcludedChips,
    bool IdVerifiedOnly, string Visitors, int? MaxDistanceKm, string Sort, bool PlusAvailable);
public sealed record FilterUpdate(int? MinAge, int? MaxAge, IReadOnlyList<Guid>? RequiredChips, string? RequiredMatch, IReadOnlyList<Guid>? ExcludedChips,
    bool IdVerifiedOnly, string? Visitors, int? MaxDistanceKm, string? Sort);
