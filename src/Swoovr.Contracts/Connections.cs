namespace Swoovr.Contracts;

/// <summary>Another member as a request or block list shows them: public id, display name and public badges only.</summary>
public sealed record PersonSummary(Guid Id, string? DisplayName, IReadOnlyList<BadgeView> Badges);

/// <summary>
/// A connection request as one of its two members sees it (D112). Direction is "received" or "sent"; status is
/// "pending", "accepted", "declined", "withdrawn" or "cancelled".
/// Visiting is set when a local sent the request from the Travel feed: the traveller's trip it was about (slice M).
/// </summary>
public sealed record ConnectionRequestView(Guid Id, string Mode, string Direction, string Status, PersonSummary Person, DateTimeOffset CreatedAt,
    VisitView? Visiting = null);

public sealed record RequestPage(IReadOnlyList<ConnectionRequestView> Items, int Page, bool More);

/// <summary>Trip is the recipient's trip when a local sends from the Travel feed; it must be one the sender can see there.</summary>
public sealed record NewConnectionRequest(Guid To, string Mode, Guid? Trip = null);

public sealed record BlockRequest(Guid ProfileId);
public sealed record BlockedPerson(Guid Id, string? DisplayName, DateTimeOffset Since);
