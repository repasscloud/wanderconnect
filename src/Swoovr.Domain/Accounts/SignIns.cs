using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Swoovr.Domain;

/// <summary>One of the member's Keycloak sessions, as the members realm reports it. No address, browser string or token.</summary>
public sealed record IdentitySession(string Id, DateTimeOffset Started, DateTimeOffset LastActive, IReadOnlyList<string> Clients);

/// <summary>
/// What the members realm holds for one user (#185, D341): open sessions, linked Apple/Google sign-ins (provider aliases only,
/// never the provider's user id or name) and which of the member's own credentials exist (types only, never values).
/// </summary>
public sealed record IdentitySignIns(IReadOnlyList<IdentitySession> Sessions, IReadOnlyList<string> LinkedProviders, bool HasPassword, bool HasPasskey);

/// <summary>Outcome of removing a linked sign-in at the identity provider.</summary>
public enum UnlinkOutcome { Unlinked, NotLinked, LastSignInMethod, Failed }

/// <summary>
/// #185 (Q126 A, D317, D341): rules for Security &amp; access → Active sessions and Linked sign-ins. Members only; staff and venue
/// realms never reach these routes. Pure functions so the API, the Keycloak adapter and the checks share one answer.
/// </summary>
public static partial class SignInPolicy
{
    /// <summary>Keycloak 26 session ids are URL-safe tokens; anything else is never sent back to Keycloak.</summary>
    public static bool ValidSessionId(string? id) => id is not null && SessionIdPattern().IsMatch(id);

    /// <summary>Provider aliases are lowercase realm aliases (`apple`, `google`); anything else is refused before any call.</summary>
    public static bool ValidProvider(string? alias) => alias is not null && ProviderPattern().IsMatch(alias);

    /// <summary>
    /// The member's browser sees a handle, not Keycloak's session id: SHA-256 of the id, base64url, 22 characters. A handle is
    /// matched only against the caller's own sessions, so it can't name another member's session.
    /// </summary>
    public static string Handle(string sessionId) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(sessionId)))[..22].Replace('+', '-').Replace('/', '_');

    public static bool ValidHandle(string? handle) => handle is { Length: 22 } && HandlePattern().IsMatch(handle);

    /// <summary>The device label: which Swoovr client the session signed in to. Keycloak's admin API has no browser or device name.</summary>
    public static string Device(IReadOnlyList<string> clients, string webClientId = "swoovr-web", string mobileClientId = "swoovr-mobile") =>
        clients.Contains(mobileClientId) ? "app" : clients.Contains(webClientId) ? "web" : "other";

    /// <summary>The label a member reads for a linked provider alias.</summary>
    public static string ProviderLabel(string alias) => alias switch
    {
        "apple" => "Apple",
        "google" => "Google",
        _ => alias.Length == 0 ? alias : char.ToUpperInvariant(alias[0]) + alias[1..],
    };

    /// <summary>
    /// A linked sign-in may be removed only when another way to sign in remains: a password, a passkey or another linked provider
    /// (issue #185: "an unlink step that keeps at least one sign-in method").
    /// </summary>
    public static bool CanUnlink(IdentitySignIns signIns, string alias) =>
        signIns.LinkedProviders.Contains(alias)
        && (signIns.HasPassword || signIns.HasPasskey || signIns.LinkedProviders.Any(p => p != alias));

    /// <summary>Sessions to end for "Sign out of all other devices": everything except the caller's own session.</summary>
    public static IReadOnlyList<string> Others(IReadOnlyList<IdentitySession> sessions, string currentSessionId) =>
        sessions.Where(s => s.Id != currentSessionId && ValidSessionId(s.Id)).Select(s => s.Id).ToList();

    /// <summary>The caller's own session matching a handle, or null. The current session is never matched (use Sign out).</summary>
    public static IdentitySession? Find(IReadOnlyList<IdentitySession> sessions, string handle, string? currentSessionId) =>
        sessions.FirstOrDefault(s => s.Id != currentSessionId && ValidSessionId(s.Id) && CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Handle(s.Id)), Encoding.ASCII.GetBytes(handle)));

    [GeneratedRegex("^[A-Za-z0-9_-]{1,128}$")]
    private static partial Regex SessionIdPattern();

    [GeneratedRegex("^[a-z0-9][a-z0-9_-]{0,63}$")]
    private static partial Regex ProviderPattern();

    [GeneratedRegex("^[A-Za-z0-9_-]{22}$")]
    private static partial Regex HandlePattern();
}
