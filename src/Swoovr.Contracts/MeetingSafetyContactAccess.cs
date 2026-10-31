namespace Swoovr.Contracts;

// POST bodies only: never put code or resource grant in a URL or browser-visible API state.
public sealed record MeetingSafetyContactCodeRequest(string Link);
public sealed record MeetingSafetyContactCodeCheck(string Link, string Code);
public sealed record MeetingSafetyContactReadRequest(string Link, string Grant);
public sealed record MeetingSafetyContactAccessScope(bool Available);
public sealed record MeetingSafetyContactCodeReceipt(string Status);
/// <summary>Raw grant is returned once to the trusted SSR host for a protected HttpOnly cookie, never JavaScript.</summary>
public sealed record MeetingSafetyContactGrantReceipt(bool Verified, string? Grant = null, DateTimeOffset? ExpiresAt = null);
