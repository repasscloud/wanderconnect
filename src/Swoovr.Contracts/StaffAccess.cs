namespace Swoovr.Contracts;

// Staff access controls (internal policy §11, §15; procedure §5; #88). Admin API only, never on the member BFF allow-list.

/// <summary>Capability: identity-open or evidence-export (the `legal` role) or termination (admins). Purpose: 1–500 characters.</summary>
public sealed record AccessGrantRequest(string? Capability, string? Purpose);

public sealed record AccessGrantView(Guid Id, string Capability, string Purpose, DateTimeOffset GrantedAt, DateTimeOffset ExpiresAt, int Renewals, bool Active,
    DateTimeOffset? RevokedAt);

public sealed record SensitiveAccessView(long Sequence, DateTimeOffset At, Guid? Actor, string Action, string Resource, string? ResourceId, int Status);

/// <summary>Intact: the hash chain over the returned entries (newest last) is unbroken.</summary>
public sealed record SensitiveAccessLogView(List<SensitiveAccessView> Entries, bool Intact);

public sealed record StaffExclusionRequest(Guid StaffAccountId, Guid SubjectAccountId, string? Reason);

public sealed record StaffExclusionView(Guid StaffAccountId, Guid SubjectAccountId, string Reason, Guid CreatedBy, DateTimeOffset CreatedAt);

public sealed record ExportApprovalView(Guid Id, Guid HoldId, Guid RequestedBy, string Purpose, DateTimeOffset RequestedAt, Guid? ApprovedBy, DateTimeOffset? ApprovedAt,
    DateTimeOffset? UsedAt);

public sealed record StaffRevokeRequest(string? Reason);

/// <summary>KeycloakSignedOut is false when the staff realm has no service client to end Keycloak sessions; the API refuses them anyway.</summary>
public sealed record StaffRevokeResult(int RolesRemoved, int GrantsRevoked, bool KeycloakSignedOut);
