namespace Swoovr.Contracts;

public sealed record StaffAccountSummary(Guid Id, string? FirstName, string? LastName, string? Email, string Status,
    IReadOnlyList<string> Roles, DateTimeOffset LastSeenAt, bool Root, long Revision, DateTimeOffset? LastSignInAt = null, string Mfa = "unknown");
public sealed record StaffAccountPage(IReadOnlyList<StaffAccountSummary> Items, int Total, int Page, int Size);
public sealed record StaffAccountDetail(StaffAccountSummary Account, string? Phone, bool ProviderAvailable, bool ProviderEnabled,
    bool EmailVerified, IReadOnlyList<string> CredentialTypes, IReadOnlyList<string> RequiredActions,
    IReadOnlyList<SignInSessionView> Sessions, IReadOnlyList<StaffActionEntry> History, bool CanManage = false);
public sealed record StaffAccountCreate(string FirstName, string LastName, string Email, string? Phone, IReadOnlyList<string> Roles, string Reason, bool Confirm);
public sealed record StaffAccountUpdate(string FirstName, string LastName, string Email, string? Phone, string Status, IReadOnlyList<string> Roles,
    long ExpectedRevision, string Reason, bool Confirm);
public sealed record StaffSecurityAction(string Action, long ExpectedRevision, string Reason, bool Confirm);
