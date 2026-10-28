using System.Security.Cryptography;

namespace Swoovr.Domain;

/// <summary>
/// Member-facing support references for reports (#50): "SW-" and six characters from an alphabet without look-alikes
/// (no 0/O, 1/I/L), so a member can read one out and support can find the report in the admin app. A reference names a
/// report, never a person.
/// </summary>
public static class SupportReference
{
    public const string Prefix = "SW-";
    public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    public const int Length = 6;

    public static string New() => Prefix + new string(Enumerable.Range(0, Length).Select(_ => Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)]).ToArray());

    /// <summary>A reference as typed (any case, spaces or a missing dash forgiven), or null if it can't be one.</summary>
    public static string? Normalise(string? text)
    {
        var compact = new string((text ?? "").Where(c => !char.IsWhiteSpace(c) && c != '-').ToArray()).ToUpperInvariant();
        if (compact.StartsWith("SW")) compact = compact[2..];
        return compact.Length == Length && compact.All(Alphabet.Contains) ? Prefix + compact : null;
    }
}

/// <summary>What the member must do now, in the order they should do it.</summary>
public sealed record StandingFacts(bool PhoneRequired, bool PhoneVerified, AgeStatus AgeStatus, bool DateOfBirthSet, DobCheck DobCheck);

/// <summary>
/// Account standing rules (#50). The member sees their verification state, what they must do, sanctions in force with
/// their end dates and whether they may be appealed, decisions about them, and their own reports with a support
/// reference. Never: reports about them or who made them, case notes, internal flags (a case Swoovr opened itself, the
/// home-location review flag), ban matching, contact history, fraud signals, legal holds or law-enforcement matters.
/// A blocked photo reads exactly like a rejected one (procedure §4).
/// </summary>
public static class StandingPolicy
{
    public const string Phone = "phone", AgeCheck = "age-check", DateOfBirth = "date-of-birth", DobCorrection = "dob-correction";

    public static IReadOnlyList<string> ActionsRequired(StandingFacts f)
    {
        var actions = new List<string>();
        if (AgeAssurance.Blocks(f.AgeStatus)) actions.Add(AgeCheck);
        if (f.DobCheck == DobCheck.CorrectionNeeded) actions.Add(DobCorrection);
        else if (!f.DateOfBirthSet) actions.Add(DateOfBirth);
        if (f.PhoneRequired && !f.PhoneVerified) actions.Add(Phone);
        return actions;
    }

    /// <summary>A sanction's state as the member sees it: issued (a warning), in force, ended (its time ran out) or lifted.</summary>
    public static string SanctionState(Sanction s, DateTimeOffset now) =>
        s.LiftedAt is not null ? "lifted"
        : s.Kind == SanctionKind.Warning ? "issued"
        : Enforcement.Live(s, now) ? "in-force"
        : s.StartsAt > now ? "scheduled" : "ended";

    /// <summary>The last day an appeal can be made, while one still can.</summary>
    public static DateTimeOffset? AppealBy(Sanction s, bool appealed, DateTimeOffset now) =>
        ModerationPolicy.MayAppeal(s, s.AccountId, appealed, now) ? s.StartsAt + ModerationPolicy.AppealWindow : null;
}
