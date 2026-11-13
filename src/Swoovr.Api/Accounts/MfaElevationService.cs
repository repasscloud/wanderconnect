using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using static Swoovr.Api.Accounts.AccountEndpoints;
namespace Swoovr.Api.Accounts;

/// <summary>Session-bound authorization receipts, independent of both Keycloak authentication and purpose grants.</summary>
public static class MfaElevationService
{
    public static string? Session(ClaimsPrincipal user) => user.FindFirstValue("sid") is { } sid && SignInPolicy.ValidSessionId(sid) ? MfaPolicy.Digest(sid) : null;
    public static string? Client(ClaimsPrincipal user, IConfiguration config) => user.FindFirstValue("azp") is { } client
        && client == (config["Mfa:StaffClientId"] ?? "swoovr-admin-web") ? client : null;
    /// <summary>D366 (owner, issue 336): how long MFA counts, both for a receipt and for the sign-in itself. Default 60, at most 60.</summary>
    public static int Minutes(IConfiguration config) => Math.Clamp(config.GetValue("Mfa:ElevationMinutes", 60), 1, 60);

    public static async Task<bool> HasAsync(CurrentAccount current, ClaimsPrincipal user, SwoovrDbContext db, IConfiguration config, DateTimeOffset now, CancellationToken ct) =>
        await UntilAsync(current, user, db, config, now, ct) is not null;

    /// <summary>
    /// When this staff session's MFA elevation ends, or null when it has none. D366: a sign-in to Admin that itself completed
    /// MFA counts for 60 minutes from that sign-in (the same strength as the "Verify MFA" re-authentication, which also
    /// still works and also lasts 60 minutes). Session, client, revocation and account checks are unchanged.
    /// </summary>
    public static async Task<DateTimeOffset?> UntilAsync(CurrentAccount current, ClaimsPrincipal user, SwoovrDbContext db, IConfiguration config, DateTimeOffset now, CancellationToken ct)
    {
        var session = Session(user); var client = Client(user, config);
        if (current.Account is not { Status: AccountStatus.Active } account || !current.Evidence.MultiFactor || session is null || client is null
            || SessionRevocationPolicy.Refuses(account.SessionsRevokedAt,current.Facts.AuthenticatedAt,user.FindFirstValue("iat"),now)) return null;
        var signIn = current.Facts.AuthenticatedAt.AddMinutes(Minutes(config));
        var receipt = await db.Set<MfaElevation>().Where(e => e.AccountId == account.Id && e.SessionDigest == session && e.ClientId == client
            && e.CompletedAt != null && e.ExpiresAt > now && e.AuthenticatedAt == current.Facts.AuthenticatedAt).MaxAsync(e => e.ExpiresAt, ct);
        DateTimeOffset? until = current.Facts.AuthenticatedAt <= now.AddSeconds(5) && signIn > now ? signIn : null;
        return receipt > (until ?? DateTimeOffset.MinValue) ? receipt : until;
    }
    public static IResult Required() => Problem(403,"mfa-elevation-required","Verify MFA again before this privileged operation.");
    public static void Audit(SwoovrDbContext db, DateTimeOffset now, Guid actor, Guid? target, string action, string outcome, string? reason = null)
    {
        var trace = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "none";
        Accounts.Audit.Record(db,now,actor,"account","mfa."+action,"account",(target??actor).ToString(),"swoovr-api;"+outcome+";trace:"+trace+(reason is null ? "" : ";"+reason));
    }
}
