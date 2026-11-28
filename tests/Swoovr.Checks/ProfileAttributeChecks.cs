using System.Text.Json;
using Swoovr.Domain;

/// <summary>
/// Issue 448 (owner decision 27 November 2026, D457): the profile detail catalogue and its rules. Stable unique ids,
/// sensitive categories unavailable, typed validation, canonical storage, per-mode publication (default unpublished,
/// Dating-only details never elsewhere), "Prefer not to say" distinct from unanswered, deprecated options kept but not
/// newly chosen, and nothing shown that isn't published in the viewer's mode.
/// </summary>
static class ProfileAttributeChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Profile detail check failed: " + what); checks++; }
        JsonElement J(string json) => JsonDocument.Parse(json).RootElement.Clone();
        AttributeRefusal Check(string id, string json, params string[] modes) => ProfileAttributes.Check(id, J(json), modes, out _, out _);

        // The catalogue: unique stable ids (≤ 32 characters, lowercase), known categories, unique option ids, sensible kinds.
        var all = ProfileAttributes.All;
        Expect(all.Select(a => a.Id).Distinct().Count() == all.Count, "unique detail ids");
        Expect(all.All(a => System.Text.RegularExpressions.Regex.IsMatch(a.Id, "^[a-z][a-z-]{0,31}$")), "ids fit the column and the BFF route");
        Expect(all.All(a => ProfileAttributes.Categories.Any(c => c.Id == a.Category)), "every detail has a category");
        Expect(all.All(a => a.Options.Select(o => o.Id).Distinct().Count() == a.Options.Count), "unique option ids per detail");
        Expect(all.Where(a => a.Available && a.Kind is AttributeKind.Single or AttributeKind.Multi).All(a => a.Options.Count >= 2), "choices to choose from");
        Expect(all.Where(a => a.Kind == AttributeKind.Measure).All(a => a.Min < a.Max && a.Unit is not null), "measures have a range and unit");
        Expect(all.All(a => a.Modes.Count > 0 && a.Modes.All(ProfileAttributes.AllModes.Contains)), "modes are real modes");
        // Sensitive categories (#448 §5): listed, never available until reviewed.
        foreach (var id in new[] { "weight", "ethnicity", "nationality", "religion", "spirituality", "politics", "causes", "romantic-orientation", "accessibility" })
            Expect(ProfileAttributes.Find(id) is { Sensitive: true, Available: false }, "sensitive and unavailable: " + id);
        Expect(Check("religion", "\"x\"", "dating") == AttributeRefusal.Unavailable && Check("weight", "70", "dating") == AttributeRefusal.Unavailable, "can't be saved");
        Expect(Check("nope", "\"x\"") == AttributeRefusal.Unknown, "unknown detail");
        // Dating-only details never leave Dating.
        foreach (var id in new[] { "relationship-status", "relationship-structure", "relationship-intentions", "wants-children", "dating-pace", "date-activities", "long-distance", "affection" })
            Expect(ProfileAttributes.Find(id)!.Modes.SequenceEqual(["dating"]), "Dating only: " + id);
        Expect(Check("relationship-status", "\"single\"", "friends") == AttributeRefusal.InvalidModes, "not publishable in Friends");

        // Values: typed and canonical.
        Expect(ProfileAttributes.Check("smoking", J("\"never\""), [], out var c1, out var m1) == AttributeRefusal.None && c1 == "\"never\"" && m1.Count == 0, "saved unpublished by default");
        Expect(Check("smoking", "\"prefer-not\"", "dating") == AttributeRefusal.None, "Prefer not to say is an answer");
        Expect(Check("smoking", "\"daily\"") == AttributeRefusal.InvalidValue && Check("smoking", "[\"never\"]") == AttributeRefusal.InvalidValue && Check("smoking", "1") == AttributeRefusal.InvalidValue, "single: one known option");
        Expect(ProfileAttributes.Check("interests", J("[\"travel\",\"hiking\"]"), ["friends", "dating"], out var c2, out var m2) == AttributeRefusal.None
            && c2 == "[\"hiking\",\"travel\"]" && m2.SequenceEqual(["dating", "friends"]), "multi: catalogue order, modes in mode order");
        Expect(Check("interests", "[]") == AttributeRefusal.InvalidValue && Check("interests", "[\"hiking\",\"hiking\"]") == AttributeRefusal.InvalidValue, "multi: one or more, no repeats");
        Expect(Check("interests", JsonSerializer.Serialize(ProfileAttributes.Find("interests")!.Options.Take(ProfileAttributes.MaxMulti + 1).Select(o => o.Id))) == AttributeRefusal.InvalidValue, "multi: at most the limit");
        Expect(Check("style", "[\"casual\",\"classic\",\"sporty\",\"creative\"]") == AttributeRefusal.InvalidValue, "style: at most 3");
        Expect(Check("chat-first", "true") == AttributeRefusal.None && Check("chat-first", "\"yes\"") == AttributeRefusal.InvalidValue, "yes/no");
        Expect(Check("height", "178") == AttributeRefusal.None && Check("height", "119") == AttributeRefusal.InvalidValue && Check("height", "231") == AttributeRefusal.InvalidValue && Check("height", "178.5") == AttributeRefusal.InvalidValue, "height: whole centimetres 120–230");
        Expect(Check("smoking", "\"never\"", "dating", "dating") == AttributeRefusal.InvalidModes && Check("smoking", "\"never\"", "nowhere") == AttributeRefusal.InvalidModes, "modes: known, once each");
        Expect(Check("interests", "[\"hiking\"]", "business") == AttributeRefusal.None && Check("smoking", "\"never\"", "business") == AttributeRefusal.InvalidModes, "Business only for details meant for it");

        // Shown only where published, with labels.
        var height = new ProfileAttributeValue { AttributeId = "height", Value = "178", Modes = ["dating"] };
        Expect(ProfileAttributes.Shown(height, "dating")?.Single() == "178 cm (5′10″)" && ProfileAttributes.Shown(height, "friends") is null, "published in Dating only");
        var unpublished = new ProfileAttributeValue { AttributeId = "smoking", Value = "\"never\"", Modes = [] };
        Expect(ProfileAttributes.AllModes.All(m => ProfileAttributes.Shown(unpublished, m) is null), "saved but unpublished: never shown");
        var sensitive = new ProfileAttributeValue { AttributeId = "religion", Value = "\"x\"", Modes = ["dating"] };
        Expect(ProfileAttributes.Shown(sensitive, "dating") is null, "a stored sensitive value is never shown");
        var wrongMode = new ProfileAttributeValue { AttributeId = "relationship-status", Value = "\"single\"", Modes = ["friends", "dating"] };
        Expect(ProfileAttributes.Shown(wrongMode, "friends") is null && ProfileAttributes.Shown(wrongMode, "dating")?.Single() == "Single", "a Dating-only value never shows elsewhere, even if stored");
        Expect(ProfileAttributes.Shown(new ProfileAttributeValue { AttributeId = "interests", Value = "[\"hiking\",\"gone\"]", Modes = ["friends"] }, "friends")!.SequenceEqual(["Hiking"]), "unknown option ids dropped");
        Expect(ProfileAttributes.Shown(new ProfileAttributeValue { AttributeId = "chat-first", Value = "true", Modes = ["friends"] }, "friends")!.Single() == "Yes", "yes/no label");
        Expect(ProfileAttributes.OptionIds("not json").Count == 0, "bad JSON reads as nothing");
        Expect(ProfileAttributes.Height(152) == "152 cm (5′0″)" && ProfileAttributes.Height(200) == "200 cm (6′7″)", "height in both units");

        // Issue 452 (D459): "You both like…": both chose it and both published interests in this mode; labels, no score.
        ProfileAttributeValue I(string json, params string[] modes) => new() { AttributeId = "interests", Value = json, Modes = [.. modes] };
        Expect(ProfileAttributes.SharedInterests(I("[\"hiking\",\"travel\",\"coffee\"]", "friends"), I("[\"travel\",\"hiking\",\"gym\"]", "friends"), "friends").SequenceEqual(["Hiking", "Travel"]), "the overlap, in catalogue order");
        Expect(ProfileAttributes.SharedInterests(I("[\"hiking\"]"), I("[\"hiking\"]", "friends"), "friends").Count == 0, "the viewer hasn't published theirs: nothing");
        Expect(ProfileAttributes.SharedInterests(I("[\"hiking\"]", "friends"), I("[\"hiking\"]", "dating"), "friends").Count == 0, "the person hasn't published theirs in this mode: nothing");
        Expect(ProfileAttributes.SharedInterests(I("[\"hiking\"]", "friends"), null, "friends").Count == 0 && ProfileAttributes.SharedInterests(I("[\"hiking\"]", "friends"), I("[\"gym\"]", "friends"), "friends").Count == 0, "no overlap: nothing");
        Expect(ProfileAttributes.SharedInterests(new() { AttributeId = "smoking", Value = "\"never\"", Modes = ["friends"] }, new() { AttributeId = "smoking", Value = "\"never\"", Modes = ["friends"] }, "friends").Count == 0, "interests only, never another detail");
        Expect(Swoovr.Client.ProfileDetailText.BothLike(["Hiking"]) == "You both like hiking" && Swoovr.Client.ProfileDetailText.BothLike(["Hiking", "Travel"]) == "You both like hiking and travel"
            && Swoovr.Client.ProfileDetailText.BothLike(["Coffee", "TV", "Travel"]) == "You both like coffee, TV and travel", "wording");

        Console.WriteLine($"PASS: profile details (#448, D457) ({checks}).");
    }
}
