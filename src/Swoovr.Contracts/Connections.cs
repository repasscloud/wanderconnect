namespace Swoovr.Contracts;

/// <summary>Another member as a request or block list shows them: public id, display name and public badges only.</summary>
public sealed record PersonSummary(Guid Id, string? DisplayName, IReadOnlyList<BadgeView> Badges);

/// <summary>
/// A connection request as one of its two members sees it (D112). Direction is "received" or "sent"; status is
/// "pending", "accepted", "declined", "withdrawn" or "cancelled".
/// </summary>
public sealed record ConnectionRequestView(Guid Id, string Mode, string Direction, string Status, PersonSummary Person, DateTimeOffset CreatedAt);

public sealed record RequestPage(IReadOnlyList<ConnectionRequestView> Items, int Page, bool More);

public sealed record NewConnectionRequest(Guid To, string Mode);

public sealed record BlockRequest(Guid ProfileId);
public sealed record BlockedPerson(Guid Id, string? DisplayName, DateTimeOffset Since);
