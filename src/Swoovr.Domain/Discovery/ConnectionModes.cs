namespace Swoovr.Domain;

/// <summary>
/// A connection mode (D92): why someone wants to connect. The id is stable and never shown; label, order and
/// availability are configuration rows, so renaming or hiding a mode changes no code (Q12 rule 17).
/// </summary>
public sealed class ConnectionMode
{
    public string Id { get; set; } = "";
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Available { get; set; }
}

/// <summary>An intention chip offered by one mode. The set per mode is not decided (D92), so none are seeded.</summary>
public sealed class ModeIntention
{
    public Guid Id { get; set; }
    public string ModeId { get; set; } = "";
    public string Label { get; set; } = "";
    public int SortOrder { get; set; }
    public bool Available { get; set; }
}

/// <summary>One member's setting for one mode. Each mode is independent (Q12 rules 2–3).</summary>
public sealed class MemberMode
{
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
    public bool Enabled { get; set; }
    /// <summary>Whether others in this mode can find the member; only counts while the mode is enabled.</summary>
    public bool Visible { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class MemberModeIntention
{
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
    public Guid IntentionId { get; set; }
}

public static class ConnectionModeIds
{
    // CONFIRMED D92: these four and no others. Labels live in the database.
    public const string Dating = "dating", Friends = "friends", Activities = "activities", Business = "business";
    public static readonly IReadOnlyList<string> All = [Dating, Friends, Activities, Business];
}

public enum ModeChangeError { None, UnknownMode, ModeUnavailable, UnknownIntention, TooManyIntentions }

/// <summary>
/// Server-side rules for a member's mode settings (Q12: enforced by the application, not the client).
/// DEFAULT: at most five intentions per mode; a disabled mode keeps its choices but is never discoverable.
/// </summary>
public static class ModeSettingsPolicy
{
    public const int MaxIntentions = 5;

    public static ModeChangeError Check(ConnectionMode? mode, IReadOnlyCollection<Guid> intentions, IReadOnlyCollection<ModeIntention> offered)
    {
        if (mode is null) return ModeChangeError.UnknownMode;
        if (!mode.Available) return ModeChangeError.ModeUnavailable;
        if (intentions.Count > MaxIntentions) return ModeChangeError.TooManyIntentions;
        var allowed = offered.Where(i => i.ModeId == mode.Id && i.Available).Select(i => i.Id).ToHashSet();
        return intentions.All(allowed.Contains) ? ModeChangeError.None : ModeChangeError.UnknownIntention;
    }
}

public enum ProfileTextError { None, DisplayNameTooLong, AreaTooLong, AboutTooLong, ControlCharacters }

/// <summary>
/// The shared core profile (one identity across modes, Q12 rule 16). DEFAULT limits; area is free text, not a
/// location (no location data is collected yet). Blank values clear the field.
/// </summary>
public static class ProfileTextPolicy
{
    public const int DisplayNameMax = 40, AreaMax = 60, AboutMax = 500;

    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static ProfileTextError Check(string? displayName, string? area, string? about)
    {
        if (new[] { displayName, area }.Any(v => v is not null && v.Any(char.IsControl))
            || about is not null && about.Any(c => char.IsControl(c) && c is not '\n' and not '\r'))
            return ProfileTextError.ControlCharacters;
        if (displayName?.Length > DisplayNameMax) return ProfileTextError.DisplayNameTooLong;
        if (area?.Length > AreaMax) return ProfileTextError.AreaTooLong;
        return about?.Length > AboutMax ? ProfileTextError.AboutTooLong : ProfileTextError.None;
    }
}

/// <summary>
/// What a member's own data says about whether others may find them. Facts come from application records only.
/// </summary>
public sealed record DiscoveryCandidate(AccountStatus Status, AgeStatus Age, bool BetaActive, bool Bot, bool Employee, string? DisplayName);

/// <summary>
/// Who appears in Explore and whose profile opens (DEFAULT under D92 and D62). A person is shown in a mode only if
/// both the viewer and they have it enabled, they made it visible, their account is active and adult, they are on
/// the beta (when it is on), and they have a display name. Bot and staff accounts stay out until Q11 decides.
/// Blocks and moderation restrictions join this check when those records exist (none yet).
/// </summary>
public static class DiscoveryEligibility
{
    public static bool Adult(AgeStatus age) => age is AgeStatus.Attested or AgeStatus.SelfDeclaredAdult or AgeStatus.Verified or AgeStatus.AdultOverride;

    public static bool Discoverable(DiscoveryCandidate person) =>
        person.Status == AccountStatus.Active && Adult(person.Age) && person.BetaActive && !person.Bot && !person.Employee
        && !string.IsNullOrWhiteSpace(person.DisplayName);

    public static bool ShownIn(string modeId, ConnectionMode? mode, MemberMode? viewer, MemberMode? person, DiscoveryCandidate candidate) =>
        mode is { Available: true } && mode.Id == modeId
        && viewer is { Enabled: true } && viewer.ModeId == modeId
        && person is { Enabled: true, Visible: true } && person.ModeId == modeId
        && Discoverable(candidate);
}
