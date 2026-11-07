namespace Swoovr.Domain;

/// <summary>#206: application authority, independent from provider roles and external organisation membership.</summary>
public static class StaffManagementPolicy
{
    public static bool InternalRole(AccountRole role) => role is AccountRole.Admin or AccountRole.Support or AccountRole.TrustSafety or AccountRole.Legal or AccountRole.Business;
    public static bool ValidRoles(IReadOnlyCollection<AccountRole> roles) => roles.Count <= 5 && roles.All(InternalRole) && roles.Distinct().Count() == roles.Count;
    public static bool CanManage(Guid actor, bool root, Account target, IReadOnlyCollection<AccountRole> existing, IReadOnlyCollection<AccountRole> desired) =>
        actor != target.Id && target.Realm == AccountRealm.Staff && !target.IsRootAdmin && target.Status is not (AccountStatus.Deleted or AccountStatus.PendingDeletion)
        && ValidRoles(desired) && (root || !existing.Contains(AccountRole.Legal) && !desired.Contains(AccountRole.Legal));
}

/// <summary>Provider metadata only. No secret, credential value or raw session id leaves the adapter.</summary>
public sealed record IdentityAccountInfo(string Subject, string? FirstName, string? LastName, string? Email, bool EmailVerified, bool Enabled,
    string? Phone, IReadOnlyList<string> CredentialTypes, IReadOnlyList<string> RequiredActions);
public sealed record IdentityAccountUpdate(string FirstName, string LastName, string Email, string? Phone, bool Enabled);

public interface IIdentityDirectory : IIdentityAdmin
{
    Task<IdentityAccountInfo?> AccountAsync(string subject, CancellationToken token);
    Task<string?> CreateAccountAsync(IdentityAccountUpdate update, CancellationToken token);
    Task<bool> UpdateAccountAsync(string subject, IdentityAccountUpdate update, CancellationToken token);
    Task<bool> RequireActionsAsync(string subject, IReadOnlyList<string> actions, CancellationToken token);
    Task<bool> PasswordEmailAsync(string subject, CancellationToken token);
}
/// <summary>Different realm and service credential; never resolve a staff subject through the members adapter.</summary>
public interface IStaffIdentityAdmin : IIdentityDirectory;
