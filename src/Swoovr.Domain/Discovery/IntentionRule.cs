namespace Swoovr.Domain;

// Internal vocabulary defaults, not provider claims or public numeric wire values.
public enum ConnectionIntention
{
    LongTermRelationship, DatingRomance, CasualConnection, FriendsWithBenefits,
    Friendship, Conversation, ActivityCompanion
}
public enum RequiredIntentionMatch { Any, All }

/// <summary>Evaluates full applicable selections, including undisplayed selections.</summary>
public sealed record IntentionRule(
    IReadOnlySet<ConnectionIntention> Required,
    RequiredIntentionMatch Match,
    IReadOnlySet<ConnectionIntention> Excluded)
{
    public bool IsValid => Enum.IsDefined(Match) && ValidSet(Required) && ValidSet(Excluded)
        && !Required.Overlaps(Excluded);

    public bool Allows(IReadOnlySet<ConnectionIntention>? applicable) =>
        IsValid && ValidSet(applicable) && !Excluded.Overlaps(applicable!)
        && (Required.Count == 0 || (Match == RequiredIntentionMatch.Any
            ? Required.Overlaps(applicable!) : Required.IsSubsetOf(applicable!)));

    private static bool ValidSet(IReadOnlySet<ConnectionIntention>? values) =>
        values is not null && values.All(value => Enum.IsDefined(value));
}
