using System.Security.Cryptography;
using System.Text;
namespace Swoovr.Domain;

/// <summary>One-use request and subsequent session-bound privilege receipt. This is never an authentication session/token.</summary>
public sealed class MfaElevation
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string ChallengeDigest { get; set; } = "";
    public string SessionDigest { get; set; } = "";
    public string ClientId { get; set; } = "";
    public DateTimeOffset InitialAuthenticationAt { get; set; }
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset ChallengeExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? AuthenticatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
}
public static class MfaPolicy
{
    public static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    public static bool CanComplete(MfaElevation row, Guid actor, string session, string client, DateTimeOffset authenticatedAt, bool multiFactor, DateTimeOffset now) =>
        row.AccountId == actor && row.SessionDigest == session && row.ClientId == client && multiFactor && row.CompletedAt is null
        && now < row.ChallengeExpiresAt && authenticatedAt > row.InitialAuthenticationAt && authenticatedAt.ToUnixTimeSeconds() >= row.RequestedAt.ToUnixTimeSeconds()
        && authenticatedAt <= now.AddSeconds(5);
    public static int Rank(AccountRole role) => role switch { AccountRole.Admin => 3, AccountRole.TrustSafety => 2, AccountRole.Support => 1, _ => 0 };
    public static bool RequirementMayChange(AccountRole actor, AccountRole? imposed) => Rank(actor) > 0 && Rank(actor) >= Rank(imposed ?? AccountRole.Unknown);
}
