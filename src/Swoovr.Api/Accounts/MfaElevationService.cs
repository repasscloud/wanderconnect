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
    public static async Task<bool> HasAsync(CurrentAccount current, ClaimsPrincipal user, SwoovrDbContext db, IConfiguration config, DateTimeOffset now, CancellationToken ct)
    {
        var session = Session(user); var client = Client(user, config);
        if (current.Account is not { Status: AccountStatus.Active } account || !current.Evidence.MultiFactor || session is null || client is null
            || SessionRevocationPolicy.Refuses(account.SessionsRevokedAt,current.Facts.AuthenticatedAt,user.FindFirstValue("iat"),now)) return false;
        return await db.Set<MfaElevation>().AnyAsync(e => e.AccountId == account.Id && e.SessionDigest == session && e.ClientId == client
            && e.CompletedAt != null && e.ExpiresAt > now && e.AuthenticatedAt == current.Facts.AuthenticatedAt, ct);
    }
    public static IResult Required() => Problem(403,"mfa-elevation-required","Verify MFA again before this privileged operation.");
    public static void Audit(SwoovrDbContext db, DateTimeOffset now, Guid actor, Guid? target, string action, string outcome, string? reason = null)
    {
        var trace = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? "none";
        Accounts.Audit.Record(db,now,actor,"account","mfa."+action,"account",(target??actor).ToString(),"swoovr-api;"+outcome+";trace:"+trace+(reason is null ? "" : ";"+reason));
    }
}
