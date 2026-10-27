namespace Swoovr.Domain;

/// <summary>
/// A one-time data export (Q64, D172): the member asked for their data after a fresh sign-in and got a link that works
/// once within 15 minutes. Only a hash of the link's secret is stored; the file itself is built when it is downloaded
/// and never kept.
/// </summary>
public sealed class DataExport
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public byte[] TokenHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? DownloadedAt { get; set; }
}

/// <summary>
/// A terminated member deleted their account (Q63, D171): a keyed hash of their verified email, kept two years so a new
/// registration with the same address is flagged to the admin queue. Never the address, never blocked automatically.
/// </summary>
public sealed class TerminatedEmailHold
{
    public byte[] EmailHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}

public enum DeletionRequestError { None, NotAllowed, StaleSession, AlreadyPending }

/// <summary>
/// Account deletion (Q43, D144; details Q61–Q65, D169–D173). A request deactivates the account at once (Pending deletion:
/// out of discovery, signed out everywhere) and schedules final deletion after a 30-day recovery period, or on the next
/// purge run when the member chose to delete now. Requesting, cancelling, deleting now and exporting all need a sign-in
/// within the last five minutes. Final deletion keeps a tombstone (id, Deleted, times) so audit entries and moderation
/// records still point at an id; those are kept two years (subject to legal review) and then purged with the tombstone.
/// </summary>
public static class AccountDeletionPolicy
{
    public static readonly TimeSpan RecoveryPeriod = TimeSpan.FromDays(30);
    public static readonly TimeSpan ReauthWindow = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan RecordRetention = TimeSpan.FromDays(730);
    public static readonly TimeSpan TerminatedHold = TimeSpan.FromDays(730);
    public static readonly TimeSpan ExportCooldown = TimeSpan.FromHours(24);
    public static readonly TimeSpan ExportLifetime = TimeSpan.FromMinutes(15);

    /// <summary>Active and locked accounts may ask (a locked account can still leave); pending and deleted ones can't.</summary>
    public static bool CanRequest(AccountStatus status) => status is AccountStatus.Active or AccountStatus.Locked or AccountStatus.Suspended;

    /// <summary>A sign-in at most <paramref name="window"/> old (and not from the future beyond clock skew).</summary>
    public static bool Fresh(DateTimeOffset authenticatedAt, DateTimeOffset now, TimeSpan window) =>
        now - authenticatedAt is var age && age >= TimeSpan.FromSeconds(-5) && age <= window;

    public static DeletionRequestError CheckRequest(Account account, bool fresh) =>
        account.Status == AccountStatus.PendingDeletion ? DeletionRequestError.AlreadyPending
        : !CanRequest(account.Status) ? DeletionRequestError.NotAllowed
        : !fresh ? DeletionRequestError.StaleSession
        : DeletionRequestError.None;

    /// <summary>Deactivates the account and schedules final deletion; the previous status comes back if the member cancels.</summary>
    public static void Request(Account account, DateTimeOffset now, bool immediately, bool deleteFounderToken)
    {
        account.DeletionRestoreStatus = account.Status;
        account.Status = AccountStatus.PendingDeletion;
        account.DeletionRequestedAt = now;
        account.DeletionDueAt = immediately ? now : now + RecoveryPeriod;
        account.DeleteFounderToken = deleteFounderToken;
    }

    /// <summary>"Delete now" during the recovery period: final deletion on the next purge run.</summary>
    public static bool DeleteNow(Account account, DateTimeOffset now)
    {
        if (account.Status != AccountStatus.PendingDeletion) return false;
        if (account.DeletionDueAt > now) account.DeletionDueAt = now;
        return true;
    }

    /// <summary>Cancelling restores the status from before the request, while final deletion hasn't happened.</summary>
    public static bool Cancel(Account account)
    {
        if (account.Status != AccountStatus.PendingDeletion) return false;
        account.Status = account.DeletionRestoreStatus ?? AccountStatus.Active;
        (account.DeletionRestoreStatus, account.DeletionRequestedAt, account.DeletionDueAt, account.DeleteFounderToken) = (null, null, null, false);
        return true;
    }

    public static bool Due(Account account, DateTimeOffset now) =>
        account.Status == AccountStatus.PendingDeletion && account.DeletionDueAt is { } due && due <= now;

    /// <summary>
    /// The tombstone left after final deletion: no email and nothing that describes the person. The (issuer, subject) pair
    /// stays: it is a random Keycloak user id whose user no longer exists, and keeping it means an access token issued
    /// just before deletion (valid for a few more minutes) finds the tombstone and is refused instead of creating a new
    /// account.
    /// </summary>
    public static void Tombstone(Account account, DateTimeOffset now)
    {
        account.Status = AccountStatus.Deleted;
        account.StatusReason = null;
        account.Email = null;
        account.DeletedAt = now;
        (account.DeletionRestoreStatus, account.DeleteFounderToken) = (null, false);
    }

    /// <summary>Tombstones whose retention has run out; their audit and moderation records go with them.</summary>
    public static bool RetentionOver(Account account, DateTimeOffset now) =>
        account.Status == AccountStatus.Deleted && account.DeletedAt is { } deleted && now - deleted >= RecordRetention;

    /// <summary>Whether another export may be requested now (one per 24 hours).</summary>
    public static bool ExportAllowed(DateTimeOffset? lastRequested, DateTimeOffset now) =>
        lastRequested is not { } last || now - last >= ExportCooldown;

    /// <summary>The export link works once, before it expires, for the member who asked for it.</summary>
    public static bool ExportUsable(DataExport? export, Guid accountId, DateTimeOffset now) =>
        export is not null && export.AccountId == accountId && export.DownloadedAt is null && export.ExpiresAt > now;
}

/// <summary>
/// The members realm's user administration, used only by account deletion (Q61, D169): sign the member out of every
/// session when they ask, and delete their Keycloak user at final deletion. Never mints or reads member tokens (D35).
/// </summary>
public interface IIdentityAdmin
{
    bool Configured { get; }
    /// <summary>Ends every Keycloak session of the user. False when Keycloak couldn't be reached or refused.</summary>
    Task<bool> SignOutEverywhereAsync(string subject, CancellationToken cancellationToken);
    /// <summary>Deletes the Keycloak user. True once it is gone (including when it was already gone).</summary>
    Task<bool> DeleteUserAsync(string subject, CancellationToken cancellationToken);
}
