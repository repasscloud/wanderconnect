namespace Swoovr.Contracts;

/// <summary>An intention chip a mode offers (Q16, D106): stable id and display label.</summary>
public sealed record IntentionView(Guid Id, string Label);

/// <summary>A connection mode as configured: stable id, display label, its intention chips in display order, and how many
/// a member may pick.</summary>
public sealed record ConnectionModeView(string Id, string Label, IReadOnlyList<IntentionView> Intentions, int MaxIntentions);

/// <summary>The member's own setting for one mode.</summary>
/// <param name="About">D175: this mode's own about.</param>
/// <param name="ShareCoreAbout">#91: without a mode about, the core about shows in this mode only when this is true (asked, never assumed).</param>
public sealed record MyModeView(string Id, bool Enabled, bool Visible, IReadOnlyList<Guid> Intentions, string? About = null, bool ShareCoreAbout = false, string? DisplayName = null);

/// <summary>The member's own profile: shared core fields plus every available mode's setting. HomeCity is private to the
/// member (D110, D120); it decides which market they are local to. MarketInterest says whether they asked to hear when
/// Swoovr opens where they live (D125 rule 37).</summary>
/// <param name="PictureText">Issue 389 (D382): "What would you like your picture to say?", drawn as a text tile while no main photo is approved.</param>
public sealed record MyProfileView(Guid PublicId, string? DisplayName, string? About, IReadOnlyList<MyModeView> Modes, PlaceView? HomeCity = null,
    bool MarketInterest = false, string? PictureText = null, bool ExploreNoticeDismissed = false);

/// <summary>Issue 389 (D382): sets the text picture (up to 100 characters, one line, no contact details); blank removes it.</summary>
public sealed record PictureTextUpdate(string? Text);

/// <summary>A full replacement of the core profile: leaving HomeCityId out clears the home city.</summary>
public sealed record ProfileUpdate(string? DisplayName, string? About, int? HomeCityId = null);
/// <summary>A full replacement of one mode's settings. ShareCoreAbout (#91): null keeps the current answer; true lets the
/// core about show here when this mode has no about of its own. Q118 DisplayName: null/omitted keeps the override;
/// an empty or whitespace string clears it to the main public name.</summary>
public sealed record ModeUpdate(bool Enabled, bool Visible, IReadOnlyList<Guid>? Intentions, string? About = null, bool? ShareCoreAbout = null, string? DisplayName = null);

/// <summary>One person in Explore for one mode, with the public badges the API decided (Q11). No age, email or account id.</summary>
/// <remarks>Age, Gender and Pronouns only as the person allows (D138, D139) and the mode does (issue 495: Dating and Friends);
/// Orientation in Dating only, when they show it. Never the date of birth.</remarks>
/// <param name="Distance">D348: a band, never a distance or coordinates.</param>
/// <param name="PictureText">Issue 389 (D382): the person's text picture, only while the tile has no approved photo.</param>
/// <param name="Favourite">D449 (Q195): the viewer's own private favourite of this person; never anyone else's.</param>
public sealed record ProfileCard(Guid Id, string DisplayName, string? About, IReadOnlyList<string> Intentions, IReadOnlyList<BadgeView> Badges,
    int? Age = null, string? Gender = null, string? Pronouns = null, DistanceView? Distance = null, PhotoView? Photo = null, string? Visiting = null,
    string? PictureText = null, int PhotoCount = 0, string? Connection = null, bool NewMember = false,
    string? Orientation = null, bool Online = false, bool Favourite = false);

/// <summary>
/// D348: what another member may learn about distance. Band is hidden, nearby (0–2 km), within10Km, km10To20, km20To30,
/// km30To40, km40To50, over50Km or unknown. Basis is gps (Near me) or homeCity (labelled as home cities apart, never a
/// current position), and null when the band is hidden or unknown.
/// </summary>
public sealed record DistanceView(string Band, string? Basis = null);

/// <summary>D348: the member's two independent location privacy settings, both off by default.</summary>
public sealed record LocationSettingsView(bool AppearInNearby, bool ShowDistance);
public sealed record LocationSettingsUpdate(bool AppearInNearby, bool ShowDistance);

/// <summary>Issue 499 (D410): the member's own online status choice, off by default. Available: the host offers it.</summary>
public sealed record OnlineStatusView(bool Available, bool ShowOnline);
public sealed record OnlineStatusUpdate(bool ShowOnline);

/// <summary>D348, #278: after an accepted Near me reading, the open market it is in (null outside every open market) and
/// when it stops being used. Never the position.</summary>
public sealed record NearMeLocationView(string? PresentIn, DateTimeOffset FreshUntil);
/// <summary>Explore for one mode in the viewer's active location (D125, D352), named by Market. Visiting is the checked-in
/// trip's destination city while one is active; null when the home market is the active location.</summary>
/// <param name="Scope">D345: "active" (the default: the active location's market) or "nearMe" (where the member is by fresh device location).</param>
public sealed record ExploreResult(string Mode, IReadOnlyList<ProfileCard> People, string? Market = null, string? Visiting = null, string Scope = "active");

/// <summary>
/// A mode another member shares with the viewer, with that member's intentions in it and where the two stand in it:
/// Connection is "none", "sent", "received" or "connected"; RequestId is the pending request, if any (D112). CanRequest
/// says whether the viewer may start a request in this mode (D125: same market, already connected, or a local writing to
/// a visiting traveller); the API checks it again on every send.
/// </summary>
/// <param name="ArrivalRequired">The viewer is a traveller browsing this person's market (D142) and may request here once arrival is confirmed (D149).</param>
/// <param name="About">The person's about in this mode (their mode about, else their core about if they agreed to show it here, #91).</param>
/// <param name="ConnectionId">The accepted connection in this mode, for disconnecting (D176); null otherwise.</param>
/// <param name="Orientation">Issue 447, D175: Dating only, and only when the person chose to show it; null everywhere else.</param>
public sealed record SharedModeView(string Id, string Label, IReadOnlyList<string> Intentions, string Connection, Guid? RequestId, bool CanRequest = true,
    bool ArrivalRequired = false, string? About = null, Guid? ConnectionId = null, IReadOnlyList<PhotoView>? Photos = null, string? DisplayName = null, PhotoView? MainPhoto = null, string? Orientation = null,
    IReadOnlyList<ProfileDetailView>? Details = null, IReadOnlyList<string>? SharedInterests = null);

/// <summary>Issue 448 (D457): one published profile detail as a viewer reads it in a mode: its labels, never option ids or unpublished values.</summary>
public sealed record ProfileDetailView(string Id, string Category, string Label, IReadOnlyList<string> Values, string CategoryLabel = "");

/// <summary>Issue 448: the detail catalogue members can use today (sensitive categories aren't listed until reviewed).</summary>
public sealed record ProfileDetailCatalogue(int Version, IReadOnlyList<ProfileDetailCategory> Categories, IReadOnlyList<ProfileDetailDefinition> Details);
public sealed record ProfileDetailCategory(string Id, string Label);
/// <summary>Kind is single, multi, boolean or measure (whole numbers in Unit, between Min and Max). Modes: where it may be published.</summary>
public sealed record ProfileDetailDefinition(string Id, string Category, string Label, string Kind, IReadOnlyList<ProfileDetailOption> Options, IReadOnlyList<string> Modes,
    int MaxChoices, int? Min, int? Max, string? Unit);
public sealed record ProfileDetailOption(string Id, string Label);
/// <summary>Issue 448: a member's own saved detail: the value as JSON (an option id, a list of them, true/false or a number) and where it's shown.</summary>
public sealed record ProfileDetailValue(string Id, System.Text.Json.JsonElement Value, IReadOnlyList<string> Modes);
/// <summary>Issue 448: save a detail. Modes empty: saved but not shown to anyone.</summary>
public sealed record ProfileDetailUpdate(System.Text.Json.JsonElement Value, IReadOnlyList<string>? Modes);

/// <summary>#91: one captured version of what others could see (Mode "core" or a mode id), as JSON.</summary>
public sealed record ProfileVersionView(string Mode, DateTimeOffset CapturedAt, string ContentJson, string Sha256);

/// <summary>#91: the subject's profile as visible when the case's first report was made (admin API only).</summary>
public sealed record ProfileAtReport(DateTimeOffset ReportedAt, List<ProfileVersionView> Versions);

/// <summary>Whether the member wants to hear when Swoovr opens where they live (D125 rule 37).</summary>
public sealed record MarketInterestUpdate(bool Interested);
public sealed record PersonProfile(Guid Id, string DisplayName, string? About, IReadOnlyList<SharedModeView> Modes, IReadOnlyList<BadgeView> Badges,
    int? Age = null, string? Gender = null, string? Pronouns = null, PhotoView? Photo = null, DistanceView? Distance = null, string? PictureText = null,
    bool Online = false, DateOnly? LastUpdated = null, bool Favourite = false);
// Issue 568: Online is the D410/D422 green dot (both members chose to show it; never a time). LastUpdated is the day the
// profile the viewer can see last changed (its captured versions, #91): a date only, never activity.
