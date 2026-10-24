namespace Swoovr.Domain;

public enum LocalAccess { Open, HomeCityRequired, MarketNotOpen }

public enum HomeMove { Allowed, Locked }

/// <summary>
/// Market rules for home discovery (D125, D146). Local discovery is based on where a member lives: Explore, profiles
/// and requests reach only people whose home city is in the same open market, or an official bot assigned to it.
/// Remote discovery needs a declared trip (Travel), and established relationships are never cut by a market boundary.
/// Facts come from application records only; the API applies the same rules to every client.
/// </summary>
public static class MarketAccess
{
    /// <summary>How long after moving the home city into one market a move into another is refused (Q24 rules 28 and 34).</summary>
    public static readonly TimeSpan HomeMoveWait = TimeSpan.FromDays(30);

    /// <summary>
    /// Whether a member may use local discovery in a mode: they need a home city (rule 30), and it must be in an open
    /// market that offers the mode (rules 5 and 33; a suspended market stops new discovery, rule 47).
    /// </summary>
    public static LocalAccess Local(bool hasHomeCity, Market? homeMarket, IReadOnlyCollection<string> marketModes, string modeId) =>
        !hasHomeCity ? LocalAccess.HomeCityRequired
        : homeMarket is { Open: true } && marketModes.Contains(modeId) ? LocalAccess.Open
        : LocalAccess.MarketNotOpen;

    /// <summary>
    /// Whether a person counts as local to the viewer's open market (rules 9–10, 38–42): a person by their own home
    /// market, an official bot by its market assignments (`*` is every open market). Staff are people like anyone else.
    /// </summary>
    public static bool SameMarket(string? viewerOpenMarket, string? personHomeMarket, bool personIsBot, IReadOnlyCollection<string>? botMarkets) =>
        viewerOpenMarket is not null && (personIsBot
            ? botMarkets is not null && (botMarkets.Contains(viewerOpenMarket) || botMarkets.Contains(BotMarket.Everywhere))
            : personHomeMarket == viewerOpenMarket);

    /// <summary>
    /// Whether another member's profile opens for the viewer (rules 11–13, 21, 46): same market, an existing request or
    /// connection between them in any mode, or a local looking at a traveller visiting their market.
    /// </summary>
    public static bool ProfileOpens(bool sameMarket, bool related, bool visiting) => sameMarket || related || visiting;

    /// <summary>
    /// Whether the viewer may start a request in one mode (rules 9, 19–21, 46): same market, a connected pair adding a
    /// mode (they are already in contact), or a local writing to a traveller visiting their market in that mode.
    /// Travellers never start contact with a destination's locals here; that waits for arrival (D142).
    /// </summary>
    public static bool MayRequest(bool sameMarket, bool connected, bool visitingInMode) => sameMarket || connected || visitingInMode;

    /// <summary>
    /// Moving the home city (rules 28 and 34): leaving every market, staying in the same one, or returning to the market
    /// last moved into is always allowed; moving into a different market is refused within 30 days of the last move.
    /// </summary>
    public static HomeMove CanMoveHome(string? currentMarket, string? newMarket, string? lastMovedTo, DateTimeOffset? lastMovedAt, DateTimeOffset now) =>
        newMarket is null || newMarket == currentMarket || newMarket == lastMovedTo || lastMovedAt is null || now - lastMovedAt >= HomeMoveWait
            ? HomeMove.Allowed : HomeMove.Locked;
}
