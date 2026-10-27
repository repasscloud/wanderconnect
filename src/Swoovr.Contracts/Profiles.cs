namespace Swoovr.Contracts;

/// <summary>An intention chip a mode offers (Q16, D106): stable id and display label.</summary>
public sealed record IntentionView(Guid Id, string Label);

/// <summary>A connection mode as configured: stable id, display label, its intention chips in display order, and how many
/// a member may pick.</summary>
public sealed record ConnectionModeView(string Id, string Label, IReadOnlyList<IntentionView> Intentions, int MaxIntentions);

/// <summary>The member's own setting for one mode.</summary>
/// <param name="About">D175: this mode's own about.</param>
/// <param name="ShareCoreAbout">#91: without a mode about, the core about shows in this mode only when this is true (asked, never assumed).</param>
public sealed record MyModeView(string Id, bool Enabled, bool Visible, IReadOnlyList<Guid> Intentions, string? About = null, bool ShareCoreAbout = false);

/// <summary>The member's own profile: shared core fields plus every available mode's setting. HomeCity is private to the
/// member (D110, D120); it decides which market they are local to. MarketInterest says whether they asked to hear when
/// Swoovr opens where they live (D125 rule 37).</summary>
public sealed record MyProfileView(Guid PublicId, string? DisplayName, string? Area, string? About, IReadOnlyList<MyModeView> Modes, PlaceView? HomeCity = null,
    bool MarketInterest = false);

/// <summary>A full replacement of the core profile: leaving HomeCityId out clears the home city.</summary>
public sealed record ProfileUpdate(string? DisplayName, string? Area, string? About, int? HomeCityId = null);
/// <summary>A full replacement of one mode's settings. ShareCoreAbout (#91): null keeps the current answer; true lets the
/// core about show here when this mode has no about of its own.</summary>
public sealed record ModeUpdate(bool Enabled, bool Visible, IReadOnlyList<Guid>? Intentions, string? About = null, bool? ShareCoreAbout = null);

/// <summary>One person in Explore for one mode, with the public badges the API decided (Q11). No age, email or account id.</summary>
/// <remarks>Age, Gender and Pronouns only as the person allows (D138, D139); never the date of birth.</remarks>
/// <param name="DistanceBand">#90: a rounded band between home cities ("Under 5 km" … "Over 100 km"), never a distance.</param>
public sealed record ProfileCard(Guid Id, string DisplayName, string? Area, string? About, IReadOnlyList<string> Intentions, IReadOnlyList<BadgeView> Badges,
    int? Age = null, string? Gender = null, string? Pronouns = null, string? DistanceBand = null, PhotoView? Photo = null);
/// <summary>Explore for one mode in the viewer's home market (D125), named by Market.</summary>
public sealed record ExploreResult(string Mode, IReadOnlyList<ProfileCard> People, string? Market = null);

/// <summary>
/// A mode another member shares with the viewer, with that member's intentions in it and where the two stand in it:
/// Connection is "none", "sent", "received" or "connected"; RequestId is the pending request, if any (D112). CanRequest
/// says whether the viewer may start a request in this mode (D125: same market, already connected, or a local writing to
/// a visiting traveller); the API checks it again on every send.
/// </summary>
/// <param name="ArrivalRequired">The viewer is a traveller browsing this person's market (D142) and may request here once arrival is confirmed (D149).</param>
/// <param name="About">The person's about in this mode (their mode about, else their core about if they agreed to show it here, #91).</param>
/// <param name="ConnectionId">The accepted connection in this mode, for disconnecting (D176); null otherwise.</param>
public sealed record SharedModeView(string Id, string Label, IReadOnlyList<string> Intentions, string Connection, Guid? RequestId, bool CanRequest = true,
    bool ArrivalRequired = false, string? About = null, Guid? ConnectionId = null, IReadOnlyList<PhotoView>? Photos = null);

/// <summary>#91: one captured version of what others could see (Mode "core" or a mode id), as JSON.</summary>
public sealed record ProfileVersionView(string Mode, DateTimeOffset CapturedAt, string ContentJson, string Sha256);

/// <summary>#91: the subject's profile as visible when the case's first report was made (admin API only).</summary>
public sealed record ProfileAtReport(DateTimeOffset ReportedAt, List<ProfileVersionView> Versions);

/// <summary>Whether the member wants to hear when Swoovr opens where they live (D125 rule 37).</summary>
public sealed record MarketInterestUpdate(bool Interested);
public sealed record PersonProfile(Guid Id, string DisplayName, string? Area, string? About, IReadOnlyList<SharedModeView> Modes, IReadOnlyList<BadgeView> Badges,
    int? Age = null, string? Gender = null, string? Pronouns = null, PhotoView? Photo = null);
