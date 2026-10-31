namespace Swoovr.Contracts;

/// <summary>
/// The caller's own validated session, as the API sees it. Wire values are stable
/// lowercase names, never provider claim names. Carries no email, subject or token.
/// Beta is notRequired, enrolled, notEnrolled or unavailable (D62); MemberArea already includes it.
/// </summary>
public sealed record SessionSummary(
    string Realm,
    string Method,
    bool MultiFactor,
    bool EmailVerified,
    bool AdultAttested,
    bool MemberArea,
    bool AdminArea,
    bool SupportArea,
    string Beta,
    string? EmailChangeProblem = null);
