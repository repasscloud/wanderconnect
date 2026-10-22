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
    public DateTimeOffset? FoundingSince { get; set; }
    public VerificationStatus PhotoStatus { get; set; }
    public DateTimeOffset? PhotoCheckedAt { get; set; }
    public VerificationStatus IdStatus { get; set; }
    public DateTimeOffset? IdCheckedAt { get; set; }
    /// <summary>Backend only, never a badge and never implies ID verified (D58).</summary>
    public bool AdminVerified { get; set; }
    public DateTimeOffset? AdminVerifiedAt { get; set; }
    public bool Bot { get; set; }
    public bool Employee { get; set; }
}

public enum Badge { Founder, PhotoVerified, IdVerified, Staff, Bot }
public sealed record ShownBadge(Badge Badge, DateTimeOffset? CheckedAt);

/// <summary>
/// The badges a viewer sees (D55): clients draw only what this returns. DEFAULT while Q11 is
/// OPEN: every applicable badge shows, in this order.
/// </summary>
public static class BadgePolicy
{
    public static IReadOnlyList<ShownBadge> For(MemberProfile? profile)
    {
        if (profile is null) return [];
        var badges = new List<ShownBadge>();
        if (profile.FoundingMember) badges.Add(new(Badge.Founder, null));
        if (profile.PhotoStatus == VerificationStatus.Verified) badges.Add(new(Badge.PhotoVerified, profile.PhotoCheckedAt));
        // Admin verified never stands in for ID verified (D58).
        if (profile.IdStatus == VerificationStatus.Verified) badges.Add(new(Badge.IdVerified, profile.IdCheckedAt));
        if (profile.Employee) badges.Add(new(Badge.Staff, null));
        if (profile.Bot) badges.Add(new(Badge.Bot, null));
        return badges;
    }
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
