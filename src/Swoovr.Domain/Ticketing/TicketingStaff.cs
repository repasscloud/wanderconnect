namespace Swoovr.Domain;

/// <summary>
/// Swoovr's record of one staff member's two Zammad identities (owner decision, 5 November 2026, Q133/#204):
/// the token-only identity Swoovr Admin acts as, and the web sign-in identity for direct Zammad use. Swoovr computes both
/// identities' permissions and pushes them; anything changed in Zammad is put back on the next verification.
/// The API identity's token is stored encrypted and never leaves the API.
/// </summary>
public sealed class TicketingStaff
{
    public Guid AccountId { get; set; }
    public long? ProviderUserId { get; set; }
    public long? WebProviderUserId { get; set; }
    public byte[] TokenProtected { get; set; } = [];
    /// <summary>Digest of the last desired state applied, so unchanged staff aren't rewritten every pass.</summary>
    public string? AppliedDigest { get; set; }
    public bool WebActive { get; set; }
    /// <summary>The account's session revocation cutoff already applied to Zammad web sessions.</summary>
    public DateTimeOffset? SessionsRevokedApplied { get; set; }
    public DateTimeOffset? SyncedAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public string? FailureCode { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
}

public sealed record StaffDirectoryUser(long Id, string Login, bool Active, IReadOnlyList<string> Roles, IReadOnlyDictionary<long, IReadOnlyList<string>> Groups, string? Email);

/// <summary>Staff account management in the helpdesk, used only by the staff synchronisation (never by request handlers).</summary>
public interface ITicketingStaffDirectory
{
    Task<IReadOnlyList<SupportOption>> GroupsAsync(CancellationToken ct);
    Task<StaffDirectoryUser?> FindAsync(string login, CancellationToken ct);
    Task<StaffDirectoryUser> GetAsync(long id, CancellationToken ct);
    /// <summary>Creates an inactive agent with no groups. Permissions are applied separately.</summary>
    Task<StaffDirectoryUser> CreateAgentAsync(string login, string firstname, string lastname, string? email, CancellationToken ct);
    Task<StaffDirectoryUser> UpdateAgentAsync(long id, bool active, IReadOnlyDictionary<long, string> groups, string firstname, string lastname, string? email, CancellationToken ct);
    /// <summary>Issues an API token owned by that agent (acting on its behalf), for Swoovr Admin to use as them.</summary>
    Task<string> IssueTokenAsync(long id, CancellationToken ct);
    /// <summary>
    /// Deletes that user's API tokens, keeping only the newest one Swoovr issued when <paramref name="keepIssued"/>. Tokens
    /// anyone created by hand (for example from Zammad's profile page) never outlive a synchronisation.
    /// </summary>
    Task<int> RevokeTokensAsync(long id, bool keepIssued, CancellationToken ct);
    /// <summary>Ends every live web session of that user. Returns how many were ended.</summary>
    Task<int> EndSessionsAsync(long id, CancellationToken ct);
    /// <summary>Active agents in the helpdesk, for drift reporting (any not managed by Swoovr).</summary>
    Task<IReadOnlyList<StaffDirectoryUser>> AgentsAsync(CancellationToken ct);
}
