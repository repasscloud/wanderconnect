namespace Swoovr.Contracts;
public sealed record MfaView(string Status, bool Required, bool ResetPending, IReadOnlyList<string> Methods, DateTimeOffset? LastMfaAuthentication, DateTimeOffset? LastReset, bool RecoveryCodesAvailable = false);
public sealed record MfaChange(bool Required, string Reason, bool Confirm);
public sealed record MfaReset(string CaseReference, bool IdentityVerified, bool Confirm);
public sealed record MfaElevationChallenge(string Challenge, DateTimeOffset ExpiresAt);
public sealed record MfaElevationComplete(string Challenge);
public sealed record MfaElevationView(bool Elevated, DateTimeOffset? ExpiresAt);
