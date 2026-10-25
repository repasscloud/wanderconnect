namespace Swoovr.Contracts;

/// <summary>An intention chip a mode offers (Q16, D106): stable id and display label.</summary>
public sealed record IntentionView(Guid Id, string Label);

/// <summary>A connection mode as configured: stable id, display label, its intention chips in display order, and how many
/// a member may pick.</summary>
public sealed record ConnectionModeView(string Id, string Label, IReadOnlyList<IntentionView> Intentions, int MaxIntentions);

/// <summary>The member's own setting for one mode.</summary>
public sealed record MyModeView(string Id, bool Enabled, bool Visible, IReadOnlyList<Guid> Intentions);

/// <summary>The member's own profile: shared core fields plus every available mode's setting. HomeCity is private to the
/// member (D110, D120); it decides which market they are local to. MarketInterest says whether they asked to hear when
/// Swoovr opens where they live (D125 rule 37).</summary>
public sealed record MyProfileView(Guid PublicId, string? DisplayName, string? Area, string? About, IReadOnlyList<MyModeView> Modes, PlaceView? HomeCity = null,
    bool MarketInterest = false);

/// <summary>A full replacement of the core profile: leaving HomeCityId out clears the home city.</summary>
public sealed record ProfileUpdate(string? DisplayName, string? Area, string? About, int? HomeCityId = null);
public sealed record ModeUpdate(bool Enabled, bool Visible, IReadOnlyList<Guid>? Intentions);

/// <summary>One person in Explore for one mode, with the public badges the API decided (Q11). No age, email or account id.</summary>
public sealed record ProfileCard(Guid Id, string DisplayName, string? Area, string? About, IReadOnlyList<string> Intentions, IReadOnlyList<BadgeView> Badges);
/// <summary>Explore for one mode in the viewer's home market (D125), named by Market.</summary>
public sealed record ExploreResult(string Mode, IReadOnlyList<ProfileCard> People, string? Market = null);

/// <summary>
/// A mode another member shares with the viewer, with that member's intentions in it and where the two stand in it:
/// Connection is "none", "sent", "received" or "connected"; RequestId is the pending request, if any (D112). CanRequest
/// says whether the viewer may start a request in this mode (D125: same market, already connected, or a local writing to
/// a visiting traveller); the API checks it again on every send.
/// </summary>
public sealed record SharedModeView(string Id, string Label, IReadOnlyList<string> Intentions, string Connection, Guid? RequestId, bool CanRequest = true);

/// <summary>Whether the member wants to hear when Swoovr opens where they live (D125 rule 37).</summary>
public sealed record MarketInterestUpdate(bool Interested);
public sealed record PersonProfile(Guid Id, string DisplayName, string? Area, string? About, IReadOnlyList<SharedModeView> Modes, IReadOnlyList<BadgeView> Badges);
