namespace Swoovr.Contracts;

/// <summary>
/// The caller's own validated session, as the API sees it. Wire values are stable
/// lowercase names, never provider claim names. Carries no email, subject or token.
/// </summary>
public sealed record SessionSummary(
    string Realm,
    string Method,
    bool MultiFactor,
    bool EmailVerified,
    bool AdultAttested,
    bool MemberArea);
