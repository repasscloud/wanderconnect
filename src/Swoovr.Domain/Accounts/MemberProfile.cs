namespace Swoovr.Domain;

/// <summary>Verification statuses from identity, trust and safety; only Verified shows a badge.</summary>
public enum VerificationStatus { NotStarted, Pending, Verified, NeedsAction, Rejected, Expired }

/// <summary>
/// Age status is application-owned (D40). Unconfirmed: no 18+ confirmation recorded yet. Attested: only the Keycloak "I'm 18 or over" box.
/// SelfDeclaredAdult: an 18+ date of birth or age was entered; this is never "age verified".
/// Underage: an under-18 date of birth or age; the account is locked. NeedsVerification: a
/// review asked for an age check. Verified: an identity provider confirmed adulthood (Q04).
/// AdultOverride: support/admin confirmed adulthood (D40).
/// </summary>
public enum AgeStatus { Unconfirmed, Attested, SelfDeclaredAdult, Underage, NeedsVerification, Verified, AdultOverride }

/// <summary>
/// The member's profile flags (D55, docs/product/08-PROFILE-FLAGS-AND-MODES.md) and age
/// details, one row per members-realm account. Flags are set only by the application.
/// </summary>
public sealed class MemberProfile
{
    public Guid AccountId { get; set; }
    /// <summary>Optional (owner, 23 October 2026). When present it decides the age.</summary>
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>Optional age entered instead of a date of birth, with the day it was entered.</summary>
    public int? DeclaredAge { get; set; }
    public DateOnly? DeclaredOn { get; set; }
    public AgeStatus AgeStatus { get; set; }
    public DateTimeOffset? AgeStatusAt { get; set; }

    public bool FoundingMember { get; set; }
    /// <summary>When the founding place was first granted; a restore keeps it (D65: no new free period).</summary>
    public DateTimeOffset? FoundingSince { get; set; }
    /// <summary>The founding place this account holds; one live account per place.</summary>
    public int? FounderPlace { get; set; }
    public VerificationStatus PhotoStatus { get; set; }
    public DateTimeOffset? PhotoCheckedAt { get; set; }
    public VerificationStatus IdStatus { get; set; }
    public DateTimeOffset? IdCheckedAt { get; set; }
    /// <summary>
    /// D128: starts true, false once a self-service ID check starts; an admin may set it again (D129). IsIdVerified is
    /// <see cref="IdStatus"/> == Verified.
    /// </summary>
    public bool CanRequestIdVerification { get; set; } = true;
    /// <summary>
    /// Owner answer to Q53 (D158): true when an approved ID check reported the same date of birth as the profile's. A
    /// different date leaves it false; the document's date is compared and dropped, never stored. Changing the profile's
    /// date clears it.
    /// </summary>
    public bool DateOfBirthVerified { get; set; }
    public DateTimeOffset? DateOfBirthVerifiedAt { get; set; }
    /// <summary>D166: a correction or a manual review open after an approved ID check whose date differed.</summary>
    public DobCheck DobCheck { get; set; }
    public DateTimeOffset? DobCheckAt { get; set; }
    /// <summary>D130: when the member first met every founding condition (FounderEligibility); never moves once set.</summary>
    public DateTimeOffset? FounderEligibleAt { get; set; }
    /// <summary>D130: when the founding benefits start, kept apart from the allocation time (<see cref="FoundingSince"/>).</summary>
    public DateTimeOffset? FounderEntitlementFrom { get; set; }
    /// <summary>Backend only, never a badge and never implies ID verified (D58).</summary>
    public bool AdminVerified { get; set; }
    public DateTimeOffset? AdminVerifiedAt { get; set; }
    public bool Bot { get; set; }
    public bool Employee { get; set; }

    /// <summary>
    /// The id other members see in profile links (D92 slice): random, never the account id, issuer or subject.
    /// </summary>
    public Guid PublicId { get; set; }
    /// <summary>Shared core profile across every connection mode (Q12 rule 16); limits in ProfileTextPolicy.</summary>
    public string? DisplayName { get; set; }
    public string? Area { get; set; }
    public string? About { get; set; }
    /// <summary>The member's home city, a GeoNames id (D110). Decides which market they are local to; private to them.</summary>
    public int? HomeGeoNameId { get; set; }
    /// <summary>
    /// The market the home city is in, whatever its status (D146). Worked out by the server from the home city and the
    /// market areas, recomputed when either changes; never returned to clients.
    /// </summary>
    public string? HomeMarketId { get; set; }
    /// <summary>The market the member last moved their home city into, and when (Q24 rules 28 and 34).</summary>
    public string? HomeMarketMovedTo { get; set; }
    public DateTimeOffset? HomeMarketMovedAt { get; set; }
    /// <summary>
    /// D150: set when home-location checks disagree with the home city often enough for an admin to look
    /// (HomeLocationPolicy); cleared by an admin. Never suspends or hides anyone by itself, and never shown to others.
    /// </summary>
    public DateTimeOffset? HomeReviewFlaggedAt { get; set; }
}

public enum Badge { Founder, PhotoVerified, IdVerified, Staff, Bot }
public sealed record ShownBadge(Badge Badge, DateTimeOffset? CheckedAt);

/// <summary>
/// The display order of public badges (Q11 rule 61, D108): one row per badge in `badge_definitions`, so the order is
/// configuration, not code. Seeded founder, photo, ID, staff, bot (D74).
/// </summary>
public sealed class BadgeDefinition
{
    public Badge Badge { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>
/// The badges a viewer sees (D55, Q11): clients draw only what this returns, for the member themselves and for others.
/// Several show at once (rule 57), each from its own record (rule 58). Admin verified never shows (D58, rule 15).
/// Official bots carry only the bot badge: no Photo or ID badge and no founding recognition (rules 54, 59). Staff and
/// bot badges are mandatory while the record is active (rules 34, 47); there is no setting that hides a badge.
/// </summary>
public static class BadgePolicy
{
    public static readonly IReadOnlyList<Badge> DefaultOrder = [Badge.Founder, Badge.PhotoVerified, Badge.IdVerified, Badge.Staff, Badge.Bot];

    public static IReadOnlyList<ShownBadge> For(MemberProfile? profile) => For(profile, DefaultOrder);

    /// <summary>In the configured order; a badge missing from the configuration still shows, after the configured ones.</summary>
    public static IReadOnlyList<ShownBadge> For(MemberProfile? profile, IReadOnlyList<Badge> order)
    {
        if (profile is null) return [];
        if (profile.Bot) return [new(Badge.Bot, null)];
        var badges = new List<ShownBadge>();
        if (profile.FoundingMember) badges.Add(new(Badge.Founder, null));
        if (profile.PhotoStatus == VerificationStatus.Verified) badges.Add(new(Badge.PhotoVerified, profile.PhotoCheckedAt));
        // Admin verified never stands in for ID verified (D58).
        if (profile.IdStatus == VerificationStatus.Verified) badges.Add(new(Badge.IdVerified, profile.IdCheckedAt));
        if (profile.Employee) badges.Add(new(Badge.Staff, null));
        int Rank(Badge badge) => order.IndexOf(badge) is >= 0 and var i ? i : order.Count + (int)badge;
        return badges.OrderBy(b => Rank(b.Badge)).ToList();
    }

    private static int IndexOf(this IReadOnlyList<Badge> order, Badge badge)
    {
        for (var i = 0; i < order.Count; i++) if (order[i] == badge) return i;
        return -1;
    }
}

/// <summary>
/// A connection mode an admin authorised for one official bot (Q11 rules 48–49, D108). A bot appears in a mode only
/// with this row and its own setting switched on; removing the row hides it there at once.
/// </summary>
public sealed class BotModeAuthorisation
{
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
    public DateTimeOffset AuthorisedAt { get; set; }
    public Guid? AuthorisedBy { get; set; }
}

public enum AgeChangeError { None, DateOfBirthInFuture, DateOfBirthTooOld, AgeOutOfRange }
public sealed record AgeChange(AgeChangeError Error, AgeStatus Status, bool LocksAccount, int? Age);

/// <summary>
/// Age entry rules (owner, 23 October 2026, refining D40): a date of birth, when given,
/// decides the age; otherwise an entered age is used; both may be left blank. An under-18
/// result locks the account (D40). Statuses set by verification or staff are kept unless
/// the entry is under 18.
/// </summary>
public static class AgePolicy
{
    public const int Adult = 18;
    public const int MaxAge = 120;

    /// <summary>Whole years. A 29 February birthday falls on 1 March in other years (the later, safer day).</summary>
    public static int YearsOn(DateOnly dateOfBirth, DateOnly today)
    {
        var years = today.Year - dateOfBirth.Year;
        return (today.Month, today.Day).CompareTo((dateOfBirth.Month, dateOfBirth.Day)) < 0 ? years - 1 : years;
    }

    /// <summary>The current age, if known: from the date of birth, else the declared age plus whole years since.</summary>
    public static int? CurrentAge(MemberProfile profile, DateOnly today) =>
        profile.DateOfBirth is { } dob ? YearsOn(dob, today)
        : profile.DeclaredAge is { } age && profile.DeclaredOn is { } on ? age + YearsOn(on, today)
        : null;

    public static AgeChange Evaluate(AgeStatus current, DateOnly? dateOfBirth, int? age, DateOnly today)
    {
        int? years = null;
        if (dateOfBirth is { } dob)
        {
            if (dob > today) return new(AgeChangeError.DateOfBirthInFuture, current, false, null);
            years = YearsOn(dob, today);
            if (years > MaxAge) return new(AgeChangeError.DateOfBirthTooOld, current, false, null);
        }
        else if (age is { } entered)
        {
            if (entered is < 0 or > MaxAge) return new(AgeChangeError.AgeOutOfRange, current, false, null);
            years = entered;
        }
        if (years < Adult) return new(AgeChangeError.None, AgeStatus.Underage, true, years);
        var keep = current is AgeStatus.Verified or AgeStatus.AdultOverride or AgeStatus.NeedsVerification or AgeStatus.Underage;
        var status = keep ? current : years is not null ? AgeStatus.SelfDeclaredAdult
            : current == AgeStatus.SelfDeclaredAdult ? AgeStatus.Attested : current;
        return new(AgeChangeError.None, status, false, years);
    }
}
