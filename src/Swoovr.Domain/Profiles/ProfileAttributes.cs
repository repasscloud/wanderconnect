using System.Text.Json;

namespace Swoovr.Domain;

/// <summary>How an attribute's value is shaped.</summary>
public enum AttributeKind { Single, Multi, Boolean, Measure }

/// <summary>One selectable option: a stable id (never shown) and its English label (a localisation key later).</summary>
public sealed record AttributeOption(string Id, string Label, bool Deprecated = false);

/// <summary>
/// One optional profile detail in the catalogue (#448, D457). Ids are stable and never reused; labels can change. Modes are
/// the discovery modes the detail may be published in (Dating-only details never leave Dating). Sensitive details
/// (ethnicity, religion, politics, weight and the like) are listed so their ids are reserved, but stay unavailable until their
/// privacy review, consent and notice wording are done (owner, #448 §5): they can't be saved or shown.
/// </summary>
public sealed record AttributeDefinition(string Id, string Category, string Label, AttributeKind Kind, IReadOnlyList<AttributeOption> Options,
    IReadOnlyList<string> Modes, bool Sensitive = false, int MaxChoices = 1, int? Min = null, int? Max = null, string? Unit = null, bool Deprecated = false)
{
    /// <summary>Whether members can save and publish it today.</summary>
    public bool Available => !Sensitive && !Deprecated;
}

/// <summary>A member's saved value and where it's published. Nothing is published until the member chooses a mode.</summary>
public sealed class ProfileAttributeValue
{
    public Guid AccountId { get; set; }
    public string AttributeId { get; set; } = "";
    /// <summary>The value as JSON: an option id, a list of option ids, true/false, or a whole number in the canonical unit.</summary>
    public string Value { get; set; } = "";
    /// <summary>The discovery modes it's published in (empty: saved, not shown to anyone).</summary>
    public List<string> Modes { get; set; } = [];
    public int CatalogueVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>Why a value can't be saved.</summary>
public enum AttributeRefusal { None, Unknown, Unavailable, InvalidValue, InvalidModes }

/// <summary>
/// Issue 448 (owner decision, 27 November 2026; D457): optional, typed profile details. Every detail is optional, stored
/// once and published per mode (default unpublished), shown only where the member published it and the viewer can see that
/// mode. Details never feed discovery ranking, filters, compatibility or any score (#448 §14), and sensitive categories are
/// unavailable until reviewed (§5). An unanswered detail has no row; "Prefer not to say" is an answer.
/// </summary>
public static class ProfileAttributes
{
    /// <summary>Raised when options or details change; stored with each value so older answers stay readable.</summary>
    public const int Version = 1;
    public const int MaxMulti = 12;
    public static readonly IReadOnlyList<string> AllModes = ["dating", "friends", "activities", "business"];
    private static readonly IReadOnlyList<string> Social = ["dating", "friends", "activities"];
    private static readonly IReadOnlyList<string> DatingOnly = ["dating"];

    private static AttributeOption O(string id, string label) => new(id, label);
    private static readonly AttributeOption PreferNot = O("prefer-not", "Prefer not to say");
    private static List<AttributeOption> L(params (string Id, string Label)[] options) => options.Select(o => O(o.Id, o.Label)).ToList();
    private static List<AttributeOption> WithPreferNot(params (string Id, string Label)[] options) => [.. L(options), PreferNot];
    private static readonly List<AttributeOption> Frequency = WithPreferNot(("never", "Never"), ("sometimes", "Sometimes"), ("often", "Often"));
    private static readonly List<AttributeOption> YesNo = [];

    private static AttributeDefinition Single(string id, string category, string label, IReadOnlyList<string> modes, List<AttributeOption> options, bool sensitive = false) =>
        new(id, category, label, AttributeKind.Single, options, modes, sensitive);
    private static AttributeDefinition Multi(string id, string category, string label, IReadOnlyList<string> modes, List<AttributeOption> options, int max = MaxMulti, bool sensitive = false) =>
        new(id, category, label, AttributeKind.Multi, options, modes, sensitive, max);
    private static AttributeDefinition Flag(string id, string category, string label, IReadOnlyList<string> modes) => new(id, category, label, AttributeKind.Boolean, YesNo, modes);

    /// <summary>Categories in display order, with their headings.</summary>
    public static readonly IReadOnlyList<(string Id, string Label)> Categories =
    [
        ("appearance", "Appearance"), ("lifestyle", "Lifestyle"), ("family", "Family and home"), ("relationships", "Relationships and dating"),
        ("personality", "Personality"), ("interests", "Interests"), ("background", "Culture and languages"), ("work", "Education and work"),
        ("beliefs", "Beliefs and values"), ("astrology", "Astrology"), ("communication", "Communication"), ("availability", "Availability"),
        ("accessibility", "Accessibility"),
    ];

    private static readonly List<AttributeOption> Languages = L(("en", "English"), ("zh", "Mandarin"), ("yue", "Cantonese"), ("ar", "Arabic"), ("vi", "Vietnamese"),
        ("it", "Italian"), ("el", "Greek"), ("hi", "Hindi"), ("pa", "Punjabi"), ("es", "Spanish"), ("tl", "Tagalog"), ("fr", "French"), ("de", "German"),
        ("ja", "Japanese"), ("ko", "Korean"), ("id", "Indonesian"), ("pt", "Portuguese"), ("ru", "Russian"), ("ta", "Tamil"), ("ur", "Urdu"), ("ne", "Nepali"),
        ("th", "Thai"), ("fa", "Persian"), ("tr", "Turkish"), ("auslan", "Auslan"), ("other", "Another language"));

    private static readonly List<AttributeOption> Interests = L(("hiking", "Hiking"), ("running", "Running"), ("cycling", "Cycling"), ("swimming", "Swimming"),
        ("gym", "Gym"), ("yoga", "Yoga"), ("surfing", "Surfing"), ("climbing", "Climbing"), ("team-sport", "Team sport"), ("watching-sport", "Watching sport"),
        ("camping", "Camping"), ("gardening", "Gardening"), ("cooking", "Cooking"), ("baking", "Baking"), ("coffee", "Coffee"), ("wine", "Wine"),
        ("food", "Eating out"), ("travel", "Travel"), ("photography", "Photography"), ("art", "Art"), ("design", "Design"), ("music", "Music"),
        ("live-music", "Live music"), ("dancing", "Dancing"), ("theatre", "Theatre"), ("film", "Film"), ("tv", "TV"), ("reading", "Reading"),
        ("writing", "Writing"), ("podcasts", "Podcasts"), ("gaming", "Gaming"), ("board-games", "Board games"), ("technology", "Technology"),
        ("science", "Science"), ("history", "History"), ("languages", "Learning languages"), ("volunteering", "Volunteering"), ("animals", "Animals"),
        ("fashion", "Fashion"), ("diy", "DIY"), ("cars", "Cars"), ("meditation", "Meditation"), ("beach", "The beach"), ("markets", "Markets"));

    /// <summary>The catalogue, in display order within each category.</summary>
    public static readonly IReadOnlyList<AttributeDefinition> All =
    [
        // Appearance (identity details — age, gender, pronouns, orientation — stay in their own model, D138).
        new("height", "appearance", "Height", AttributeKind.Measure, [], AllModes, Min: 120, Max: 230, Unit: "cm"),
        new("weight", "appearance", "Weight", AttributeKind.Measure, [], Social, Sensitive: true, Min: 30, Max: 300, Unit: "kg"),
        Single("body-type", "appearance", "Body type", Social, WithPreferNot(("slim", "Slim"), ("athletic", "Athletic"), ("average", "Average"), ("curvy", "Curvy"), ("solid", "Solid"), ("larger", "Larger"))),
        Single("eye-colour", "appearance", "Eye colour", Social, L(("brown", "Brown"), ("blue", "Blue"), ("green", "Green"), ("hazel", "Hazel"), ("grey", "Grey"), ("other", "Other"))),
        Single("hair-colour", "appearance", "Hair colour", Social, L(("black", "Black"), ("brown", "Brown"), ("blonde", "Blonde"), ("red", "Red"), ("grey", "Grey or white"), ("dyed", "Dyed"), ("none", "No hair"))),
        Single("hair-length", "appearance", "Hair length", Social, L(("none", "Shaved or bald"), ("short", "Short"), ("medium", "Medium"), ("long", "Long"))),
        Single("facial-hair", "appearance", "Facial hair", Social, L(("none", "None"), ("stubble", "Stubble"), ("moustache", "Moustache"), ("beard", "Beard"))),
        Single("tattoos", "appearance", "Tattoos", Social, L(("none", "None"), ("few", "A few"), ("many", "Many"))),
        Single("piercings", "appearance", "Piercings", Social, L(("none", "None"), ("few", "A few"), ("many", "Many"))),
        Multi("style", "appearance", "Personal style", Social, L(("casual", "Casual"), ("classic", "Classic"), ("sporty", "Sporty"), ("alternative", "Alternative"), ("smart", "Smart"), ("outdoorsy", "Outdoorsy"), ("creative", "Creative")), 3),
        // Lifestyle.
        Single("smoking", "lifestyle", "Smoking", Social, Frequency),
        Single("vaping", "lifestyle", "Vaping", Social, Frequency),
        Single("alcohol", "lifestyle", "Alcohol", Social, WithPreferNot(("never", "Never"), ("socially", "Socially"), ("often", "Often"))),
        Single("cannabis", "lifestyle", "Cannabis", Social, Frequency),
        Single("exercise", "lifestyle", "Exercise", Social, L(("never", "Never"), ("sometimes", "Sometimes"), ("often", "Often"), ("daily", "Every day"))),
        Single("diet", "lifestyle", "Diet", Social, L(("anything", "Eats anything"), ("vegetarian", "Vegetarian"), ("vegan", "Vegan"), ("pescatarian", "Pescatarian"), ("halal", "Halal"), ("kosher", "Kosher"), ("other", "Other"))),
        Single("sleep", "lifestyle", "Sleep schedule", Social, L(("early-bird", "Early bird"), ("night-owl", "Night owl"), ("flexible", "It varies"))),
        // Family and home.
        Single("children", "family", "Children", Social, WithPreferNot(("none", "Don't have children"), ("at-home", "Have children at home"), ("not-at-home", "Have children, not at home"))),
        Single("wants-children", "family", "Wants children", DatingOnly, WithPreferNot(("yes", "Wants children"), ("no", "Doesn't want children"), ("open", "Open to it"), ("unsure", "Not sure"))),
        Multi("pets", "family", "Pets", Social, L(("none", "No pets"), ("dog", "Dog"), ("cat", "Cat"), ("bird", "Bird"), ("fish", "Fish"), ("other", "Other pets")), 5),
        Single("wants-pets", "family", "Wants pets", Social, L(("yes", "Would like pets"), ("no", "Doesn't want pets"), ("open", "Open to it"))),
        Single("living", "family", "Living situation", Social, WithPreferNot(("alone", "Live alone"), ("housemates", "Live with housemates"), ("family", "Live with family"), ("partner", "Live with a partner"))),
        // Relationships and dating: Dating only.
        Single("relationship-status", "relationships", "Relationship status", DatingOnly, WithPreferNot(("single", "Single"), ("separated", "Separated"), ("divorced", "Divorced"), ("widowed", "Widowed"), ("partnered", "In a relationship"))),
        Single("relationship-structure", "relationships", "Relationship style", DatingOnly, WithPreferNot(("monogamous", "Monogamous"), ("open", "Open"), ("polyamorous", "Polyamorous"), ("figuring-out", "Still figuring it out"))),
        Multi("relationship-intentions", "relationships", "Looking for", DatingOnly, L(("long-term", "A long-term relationship"), ("short-term", "Something short-term"), ("dating", "Dating to see where it goes"), ("marriage", "Marriage"), ("unsure", "Not sure yet")), 3),
        Single("romantic-orientation", "relationships", "Romantic orientation", DatingOnly, [], sensitive: true),
        Single("dating-pace", "relationships", "Dating pace", DatingOnly, L(("slow", "Slow and steady"), ("steady", "Steady"), ("quick", "Happy to move quickly"))),
        Multi("affection", "relationships", "Affection style", DatingOnly, L(("words", "Kind words"), ("time", "Time together"), ("gifts", "Thoughtful gifts"), ("help", "Helping out"), ("touch", "Physical affection")), 3),
        Multi("date-activities", "relationships", "Favourite dates", DatingOnly, L(("coffee", "Coffee"), ("drinks", "Drinks"), ("dinner", "Dinner"), ("walk", "A walk"), ("outdoors", "Something outdoors"), ("show", "A show or gig"), ("museum", "A gallery or museum"), ("active", "Something active")), 5),
        Single("long-distance", "relationships", "Long distance", DatingOnly, L(("yes", "Open to long distance"), ("no", "Prefers someone nearby"), ("maybe", "Maybe, for the right person"))),
        // Personality.
        Multi("traits", "personality", "Personality", Social, L(("adventurous", "Adventurous"), ("calm", "Calm"), ("curious", "Curious"), ("easygoing", "Easygoing"), ("funny", "Funny"), ("kind", "Kind"), ("organised", "Organised"), ("outgoing", "Outgoing"), ("thoughtful", "Thoughtful"), ("creative", "Creative"), ("ambitious", "Ambitious"), ("loyal", "Loyal")), 5),
        Single("social-energy", "personality", "Social energy", Social, L(("introvert", "Introvert"), ("ambivert", "Somewhere in between"), ("extrovert", "Extrovert"))),
        Multi("social-preferences", "personality", "Socialising", Social, L(("one-on-one", "One-on-one"), ("small-groups", "Small groups"), ("big-groups", "Big groups"), ("quiet-nights", "Quiet nights in"), ("going-out", "Going out")), 3),
        Multi("humour", "personality", "Humour", Social, L(("dry", "Dry"), ("silly", "Silly"), ("witty", "Witty"), ("dark", "Dark"), ("puns", "Puns")), 3),
        Multi("weekends", "personality", "Weekends", Social, L(("outdoors", "Outdoors"), ("social", "Catching up with friends"), ("relaxing", "Relaxing at home"), ("sport", "Sport"), ("exploring", "Exploring somewhere new"), ("working", "Working on projects")), 3),
        // Interests: shared interests (#452) compare these ids only.
        Multi("interests", "interests", "Interests", AllModes, Interests),
        // Culture and languages.
        Single("ethnicity", "background", "Ethnicity", Social, [], sensitive: true),
        Single("nationality", "background", "Nationality", Social, [], sensitive: true),
        Multi("languages", "background", "Languages", AllModes, Languages, 8),
        Multi("languages-learning", "background", "Learning", AllModes, Languages, 4),
        // Education and work.
        Single("education", "work", "Education", AllModes, WithPreferNot(("school", "High school"), ("trade", "Trade or TAFE"), ("bachelor", "Bachelor's degree"), ("postgraduate", "Postgraduate degree"), ("studying", "Studying now"))),
        // Beliefs and values: unavailable until reviewed (#448 §5).
        Single("religion", "beliefs", "Religion", Social, [], sensitive: true),
        Single("spirituality", "beliefs", "Spirituality", Social, [], sensitive: true),
        Single("politics", "beliefs", "Political views", Social, [], sensitive: true),
        Multi("causes", "beliefs", "Causes I care about", Social, [], sensitive: true),
        // Astrology.
        Single("star-sign", "astrology", "Star sign", Social, L(("aries", "Aries"), ("taurus", "Taurus"), ("gemini", "Gemini"), ("cancer", "Cancer"), ("leo", "Leo"), ("virgo", "Virgo"), ("libra", "Libra"), ("scorpio", "Scorpio"), ("sagittarius", "Sagittarius"), ("capricorn", "Capricorn"), ("aquarius", "Aquarius"), ("pisces", "Pisces"))),
        Single("chinese-zodiac", "astrology", "Chinese zodiac", Social, L(("rat", "Rat"), ("ox", "Ox"), ("tiger", "Tiger"), ("rabbit", "Rabbit"), ("dragon", "Dragon"), ("snake", "Snake"), ("horse", "Horse"), ("goat", "Goat"), ("monkey", "Monkey"), ("rooster", "Rooster"), ("dog", "Dog"), ("pig", "Pig"))),
        // Communication (#570): they inform people and never block a message (messaging rules stay in the safety settings).
        Single("reply-frequency", "communication", "Replies", AllModes, L(("frequent", "Replies often"), ("daily", "Replies daily"), ("when-available", "Replies when available"), ("flexible", "Flexible"))),
        Multi("communication-methods", "communication", "Prefers to talk by", AllModes, L(("messaging", "Messaging"), ("voice", "Voice calls"), ("video", "Video calls"), ("in-person", "Meeting in person")), 4),
        Flag("chat-first", "communication", "Likes to chat before meeting", AllModes),
        Flag("voice-calls", "communication", "Open to voice calls", AllModes),
        Flag("video-calls", "communication", "Open to video calls", AllModes),
        Flag("arranged-meetings", "communication", "Prefers arranged meet-ups to spontaneous ones", AllModes),
        // Availability (#571): general availability only; trips stay in the travel subsystem.
        Multi("availability", "availability", "Usually free", AllModes, L(("weekdays", "Weekdays"), ("weekday-evenings", "Weekday evenings"), ("weekends", "Weekends"), ("daytime", "Daytime"), ("evenings", "Evenings"), ("flexible", "Flexible"), ("arranged", "By arrangement")), 7),
        Multi("travel-interests", "availability", "Travel interests", Social, L(("beach", "Beaches"), ("cities", "Cities"), ("nature", "Nature"), ("food", "Food"), ("culture", "Culture"), ("road-trips", "Road trips")), 4),
        Single("travel-style", "availability", "Travel style", Social, L(("planner", "Plans everything"), ("spontaneous", "Spontaneous"), ("mix", "A bit of both"))),
        // Accessibility: health-related, so unavailable until reviewed.
        Multi("accessibility", "accessibility", "Accessibility", AllModes, [], sensitive: true),
    ];

    private static readonly Dictionary<string, AttributeDefinition> ById = All.ToDictionary(a => a.Id);

    public static AttributeDefinition? Find(string? id) => id is not null && ById.TryGetValue(id, out var d) ? d : null;

    /// <summary>
    /// Checks a value against its definition and returns it in canonical JSON (option ids in catalogue order, no repeats).
    /// Deprecated options can be kept but not newly chosen.
    /// </summary>
    public static AttributeRefusal Check(string? id, JsonElement value, IReadOnlyCollection<string>? modes, out string canonical, out List<string> cleanModes,
        IReadOnlyCollection<string>? currentOptions = null)
    {
        (canonical, cleanModes) = ("", []);
        var definition = Find(id);
        if (definition is null) return AttributeRefusal.Unknown;
        if (!definition.Available) return AttributeRefusal.Unavailable;
        var asked = modes ?? [];
        if (asked.Count > AllModes.Count || asked.Any(m => !definition.Modes.Contains(m)) || asked.Distinct().Count() != asked.Count) return AttributeRefusal.InvalidModes;
        cleanModes = AllModes.Where(asked.Contains).ToList();
        bool Allowed(string option) => definition.Options.FirstOrDefault(o => o.Id == option) is { } o && (!o.Deprecated || currentOptions?.Contains(option) == true);
        switch (definition.Kind)
        {
            case AttributeKind.Single when value.ValueKind == JsonValueKind.String && value.GetString() is { } one && Allowed(one):
                canonical = JsonSerializer.Serialize(one); return AttributeRefusal.None;
            case AttributeKind.Multi when value.ValueKind == JsonValueKind.Array:
                var chosen = value.EnumerateArray().Select(e => e.ValueKind == JsonValueKind.String ? e.GetString() : null).ToList();
                if (chosen.Count == 0 || chosen.Count > definition.MaxChoices || chosen.Any(c => c is null || !Allowed(c)) || chosen.Distinct().Count() != chosen.Count)
                    return AttributeRefusal.InvalidValue;
                canonical = JsonSerializer.Serialize(definition.Options.Select(o => o.Id).Where(chosen.Contains).ToList()); return AttributeRefusal.None;
            case AttributeKind.Boolean when value.ValueKind is JsonValueKind.True or JsonValueKind.False:
                canonical = value.GetBoolean() ? "true" : "false"; return AttributeRefusal.None;
            case AttributeKind.Measure when value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) && n >= definition.Min && n <= definition.Max:
                canonical = n.ToString(System.Globalization.CultureInfo.InvariantCulture); return AttributeRefusal.None;
            default: return AttributeRefusal.InvalidValue;
        }
    }

    /// <summary>The option ids a stored value holds (empty for a yes/no or a measure).</summary>
    public static IReadOnlyList<string> OptionIds(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind switch
            {
                JsonValueKind.String => [doc.RootElement.GetString()!],
                JsonValueKind.Array => doc.RootElement.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!).ToList(),
                _ => [],
            };
        }
        catch (JsonException) { return []; }
    }

    /// <summary>
    /// The labels a viewer reads for one published value, or null when it isn't shown: unknown, unavailable (a sensitive or
    /// retired detail), not published in this mode, or not valid for it. "Prefer not to say" is shown as the member chose it.
    /// </summary>
    public static IReadOnlyList<string>? Shown(ProfileAttributeValue value, string mode)
    {
        var definition = Find(value.AttributeId);
        if (definition is null || !definition.Available || !definition.Modes.Contains(mode) || !value.Modes.Contains(mode)) return null;
        switch (definition.Kind)
        {
            case AttributeKind.Boolean: return value.Value == "true" ? ["Yes"] : value.Value == "false" ? ["No"] : null;
            case AttributeKind.Measure:
                return int.TryParse(value.Value, out var n) ? [definition.Unit == "cm" ? Height(n) : $"{n} {definition.Unit}"] : null;
            default:
                var labels = OptionIds(value.Value).Select(id => definition.Options.FirstOrDefault(o => o.Id == id)?.Label).Where(l => l is not null).Select(l => l!).ToList();
                return labels.Count == 0 ? null : labels;
        }
    }

    /// <summary>"178 cm (5′10″)": both units from the canonical centimetres.</summary>
    public static string Height(int centimetres)
    {
        var inches = (int)Math.Round(centimetres / 2.54);
        return $"{centimetres} cm ({inches / 12}′{inches % 12}″)";
    }
}
