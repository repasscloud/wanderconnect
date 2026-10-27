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

/// <summary>
/// An intention chip offered by one mode (Q16, D106): forty approved chips, ten per mode, with fixed ids (D113). Label,
/// order and the available flag are data; a retired chip keeps its row so members' stored choices stay intact.
/// </summary>
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
    /// <summary>D175: an "about" for this mode only; null shows the core profile's about in this mode.</summary>
    public string? About { get; set; }
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

/// <summary>The outcome of a mode save: an error, or the intention ids to store.</summary>
public sealed record ModeChange(ModeChangeError Error, IReadOnlyList<Guid> Stored);

/// <summary>
/// Server-side rules for a member's mode settings (Q12, Q16 rule 15: enforced by the application, not the client).
/// At most five available intentions per mode; a disabled mode keeps its choices but is never discoverable.
/// D113 (Q16 rule 16): a chip retired after the member picked it stays stored and hidden, doesn't count towards the
/// five and never makes a save fail; only adding a retired, unknown or other mode's id is refused.
/// </summary>
public static class ModeSettingsPolicy
{
    public const int MaxIntentions = 5;

    public static ModeChange Check(ConnectionMode? mode, IReadOnlyCollection<Guid> requested, IReadOnlyCollection<Guid> current,
        IReadOnlyCollection<ModeIntention> offered)
    {
        if (mode is null) return new(ModeChangeError.UnknownMode, []);
        if (!mode.Available) return new(ModeChangeError.ModeUnavailable, []);
        var ours = offered.Where(i => i.ModeId == mode.Id).ToList();
        var available = ours.Where(i => i.Available).Select(i => i.Id).ToHashSet();
        var retired = ours.Where(i => !i.Available).Select(i => i.Id).ToHashSet();
        // Retired choices the member already holds are kept whatever the client sends (it never sees them).
        var keptRetired = current.Where(retired.Contains).Distinct().ToList();
        var chosen = requested.Distinct().Where(id => !keptRetired.Contains(id)).ToList();
        if (chosen.Any(id => !available.Contains(id))) return new(ModeChangeError.UnknownIntention, []);
        if (chosen.Count > MaxIntentions) return new(ModeChangeError.TooManyIntentions, []);
        return new(ModeChangeError.None, [.. chosen, .. keptRetired]);
    }
}

public enum ProfileTextError { None, DisplayNameTooLong, AreaTooLong, AboutTooLong, ControlCharacters }

/// <summary>
/// The shared core profile (one identity across modes, Q12 rule 16). DEFAULT limits; area is free text, not a
/// location (the home city is a separate GeoNames reference, D110). Blank values clear the field.
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
/// Cross-mode privacy (#37, owner answer Q67, D175). A profile is seen in one mode at a time: the mode it was opened
/// from, plus the modes in which the pair is already connected; the person's other modes are never listed. Shared across
/// every mode: display name, age (unless hidden), area, badges, gender and pronouns (under the member's own switch).
/// Per mode: the "about" (falling back to the core about) and the intention chips. Orientation and Dating's "show me" are
/// never shown to others; Business-only fields wait for #42. No online or last-active signal exists in any mode. The API
/// applies this; clients only draw what it returns.
/// </summary>
public static class ModePrivacy
{
    public static string? About(string? modeAbout, string? coreAbout) => modeAbout ?? coreAbout;

    /// <summary>
    /// The modes one profile view shows, in the shared order. With a requested mode: that mode (only when shared) and the
    /// connected ones; an unshared requested mode shows nothing (read as not found). Without one: the connected modes, or
    /// else only the first shared mode.
    /// </summary>
    public static IReadOnlyList<string> Shown(string? requested, IReadOnlyList<string> shared, IReadOnlySet<string> connected)
    {
        if (requested is not null && !shared.Contains(requested)) return [];
        var shown = shared.Where(m => m == requested || connected.Contains(m)).ToList();
        if (shown.Count == 0 && requested is null && shared.Count > 0) shown.Add(shared[0]);
        return shown;
    }
}

/// <summary>
/// What a member's own data says about whether others may find them. Facts come from application records only.
/// BotModes holds the modes an admin authorised for an official bot (empty for people).
/// </summary>
public sealed record DiscoveryCandidate(AccountStatus Status, AgeStatus Age, bool BetaActive, bool Bot, string? DisplayName,
    IReadOnlySet<string>? BotModes = null);

/// <summary>
/// Who appears in Explore and whose profile opens (D92, D62, Q11). A person is shown in a mode only if both the viewer
/// and they have it enabled, they made it visible, their account is active and adult, they are on the beta (when it is
/// on), and they have a display name. Staff take part like anyone else (Q11 rule 32). An official bot appears only in
/// modes an admin authorised for it (rule 49). Intentions never exclude anyone (Q16 rule 10). Blocks and other
/// pair rules are checked by the caller before this.
/// </summary>
public static class DiscoveryEligibility
{
    public static bool Adult(AgeStatus age) => age is AgeStatus.Attested or AgeStatus.SelfDeclaredAdult or AgeStatus.Verified or AgeStatus.AdultOverride;

    public static bool Discoverable(DiscoveryCandidate person) =>
        person.Status == AccountStatus.Active && Adult(person.Age) && person.BetaActive && !string.IsNullOrWhiteSpace(person.DisplayName);

    public static bool ShownIn(string modeId, ConnectionMode? mode, MemberMode? viewer, MemberMode? person, DiscoveryCandidate candidate) =>
        mode is { Available: true } && mode.Id == modeId
        && viewer is { Enabled: true } && viewer.ModeId == modeId
        && person is { Enabled: true, Visible: true } && person.ModeId == modeId
        && (!candidate.Bot || candidate.BotModes?.Contains(modeId) == true)
        && Discoverable(candidate);

    /// <summary>A bot may switch on only the modes an admin authorised for it; people may switch on any mode.</summary>
    public static bool MaySwitchOn(bool bot, IReadOnlySet<string> botModes, string modeId) => !bot || botModes.Contains(modeId);
}
