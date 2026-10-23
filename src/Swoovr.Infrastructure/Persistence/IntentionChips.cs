using Swoovr.Domain;

namespace Swoovr.Infrastructure.Persistence;

/// <summary>
/// The forty intention chips the owner approved (Q16, D106), seeded by the IntentionChips migration. Ids are fixed
/// (D113): 00000016-000M-4000-8000-0000000000NN, M = the mode's place in D102 order, NN = the owner's order. Change a
/// label, order or the available flag with a new migration or by editing the row; never reuse or renumber an id.
/// </summary>
public static class IntentionChips
{
    private static readonly (string Mode, string[] Labels)[] Approved =
    [
        (ConnectionModeIds.Dating, ["Long-term relationship", "Short-term dating", "Casual dating", "Something casual / no strings",
            "Friends with benefits", "Open to exploring", "Marriage / life partner", "Intimacy / physical connection", "Not sure yet",
            "New connections / see where it goes"]),
        (ConnectionModeIds.Friends, ["New friendships", "Close / meaningful friendships", "Casual friendships", "Social groups / meetups",
            "Activity partners", "Networking / meeting people", "Emotional support / someone to talk to", "Travel companions",
            "Local connections", "Expanding my social circle"]),
        (ConnectionModeIds.Activities, ["Finding activity partners", "Group outings", "Exploring local events", "Trying new experiences",
            "Fitness and outdoor activities", "Food and dining experiences", "Arts, culture and entertainment", "Weekend adventures",
            "Travel and sightseeing", "Spontaneous plans"]),
        (ConnectionModeIds.Business, ["Professional networking", "Business partnerships", "Finding clients / customers",
            "Finding service providers", "Mentorship / guidance", "Career opportunities", "Collaboration / projects",
            "Entrepreneurship / startups", "Industry events / meetups", "Knowledge sharing"]),
    ];

    public static Guid Id(int mode, int order) => Guid.Parse($"00000016-{mode:D4}-4000-8000-{order:D12}");

    public static IReadOnlyList<ModeIntention> All { get; } = Approved.SelectMany((m, modeIndex) => m.Labels.Select((label, i) => new ModeIntention
    {
        Id = Id(modeIndex + 1, i + 1), ModeId = m.Mode, Label = label, SortOrder = i + 1, Available = true,
    })).ToList();
}
