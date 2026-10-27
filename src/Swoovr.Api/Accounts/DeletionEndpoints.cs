using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

/// <summary>
/// Account deletion and data export (Q43, D144; D169–D173). Open to any members-realm account that hasn't been deleted,
/// whatever its beta, age, sanction or pending-deletion state (stores require that anyone can leave). Requesting,
/// cancelling, deleting now and asking for an export need a sign-in within the last five minutes
/// (`Accounts:ReauthSeconds`, default 300); clients send the member through Keycloak with prompt=login first.
/// </summary>
public static class DeletionEndpoints
{
    private static readonly JsonSerializerOptions ExportJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static void Map(WebApplication app)
    {
        var me = app.MapGroup("/api/v1/me").RequireAuthorization();

        me.MapGet("/deletion", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OwnerAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(View(current!.Account!, current.Profile));
        });

        me.MapPost("/deletion", async (DeletionRequest request, ClaimsPrincipal user, AccountResolver accounts, AccountDeletion deletion,
            IConfiguration configuration, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OwnerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            if (!request.Confirm) return Problem(StatusCodes.Status400BadRequest, ProblemCodes.ConfirmationRequired, "Confirm that you want to delete your account.");
            var now = clock.GetUtcNow();
            var outcome = await deletion.RequestAsync(current!.Account!, current.Profile, Fresh(current, configuration, now), request.Immediately,
                request.DeleteFounderToken, now, cancellationToken);
            return outcome.Error switch
            {
                DeletionRequestError.StaleSession => Stale(),
                DeletionRequestError.AlreadyPending => Problem(StatusCodes.Status409Conflict, ProblemCodes.DeletionAlreadyPending, "Your account is already being deleted."),
                DeletionRequestError.NotAllowed => Problem(StatusCodes.Status409Conflict, ProblemCodes.DeletionNotAllowed, "This account can't be deleted here. Contact Swoovr support."),
                _ => Results.Ok(View(current.Account!, current.Profile) with { SignedOutEverywhere = outcome.SignedOutEverywhere, FounderCodeEmailed = outcome.FounderCodeEmailed }),
            };
        });

        // "Delete now" during the 30-day recovery period: final deletion on the next purge run.
        me.MapPost("/deletion/now", async (ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OwnerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, db, now) = (current!.Account!, accounts.Db!, clock.GetUtcNow());
            if (account.Status != AccountStatus.PendingDeletion) return NotPending();
            if (!Fresh(current, configuration, now)) return Stale();
            AccountDeletionPolicy.DeleteNow(account, now);
            Audit.Record(db, now, account.Id, "account", "account.deletion-now", "account", account.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(View(account, current.Profile));
        });

        me.MapDelete("/deletion", async (ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OwnerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, db, now) = (current!.Account!, accounts.Db!, clock.GetUtcNow());
            if (account.Status != AccountStatus.PendingDeletion) return NotPending();
            if (!Fresh(current, configuration, now)) return Stale();
            AccountDeletionPolicy.Cancel(account);
            Audit.Record(db, now, account.Id, "account", "account.deletion-cancelled", "account", account.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(View(account, current.Profile));
        });

        me.MapPost("/export", async (ClaimsPrincipal user, AccountResolver accounts, IConfiguration configuration, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OwnerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, db, now) = (current!.Account!, accounts.Db!, clock.GetUtcNow());
            if (!Fresh(current, configuration, now)) return Stale();
            var last = await db.DataExports.Where(e => e.AccountId == account.Id).MaxAsync(e => (DateTimeOffset?)e.CreatedAt, cancellationToken);
            if (!AccountDeletionPolicy.ExportAllowed(last, now))
                return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.ExportTooSoon, "You can download your data once a day. Try again tomorrow.");
            var secret = RandomNumberGenerator.GetBytes(32);
            var export = new DataExport { Id = Guid.NewGuid(), AccountId = account.Id, TokenHash = SHA256.HashData(secret), CreatedAt = now, ExpiresAt = now + AccountDeletionPolicy.ExportLifetime };
            db.DataExports.Add(export);
            Audit.Record(db, now, account.Id, "account", "export.requested", "data-export", export.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ExportTicket("api/v1/me/export/" + Base64Url(secret), export.ExpiresAt));
        });

        me.MapGet("/export/{token}", async (string token, ClaimsPrincipal user, AccountResolver accounts, Verification.VerifiedIdentityStore identities, TimeProvider clock,
            CancellationToken cancellationToken) =>
        {
            var (current, denied) = await OwnerAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, db, now) = (current!.Account!, accounts.Db!, clock.GetUtcNow());
            var hash = TryHash(token);
            var export = hash is null ? null : await db.DataExports.SingleOrDefaultAsync(e => e.TokenHash == hash, cancellationToken);
            // Someone else's link, a used or expired one and nonsense all read the same.
            if (!AccountDeletionPolicy.ExportUsable(export, account.Id, now))
                return Problem(StatusCodes.Status404NotFound, ProblemCodes.ExportUnavailable, "This download link has expired or was already used. Ask for a new one.");
            export!.DownloadedAt = now;
            Audit.Record(db, now, account.Id, "account", "export.downloaded", "data-export", export.Id.ToString());
            await db.SaveChangesAsync(cancellationToken);
            var document = await DataExportBuilder.BuildAsync(db, account, now, cancellationToken, await identities.OwnAsync(account.Id, cancellationToken));
            return Results.File(JsonSerializer.SerializeToUtf8Bytes(document, ExportJson), "application/json",
                $"swoovr-data-{now:yyyy-MM-dd}.json");
        });
    }

    /// <summary>The caller as a members-realm account that still exists, in any state; the deletion routes' own gate.</summary>
    public static async Task<(CurrentAccount? Current, IResult? Denied)> OwnerAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null)
            return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        if (current.Facts.Realm != AccountRealm.Members || current.Account.Status == AccountStatus.Deleted) return (null, Results.Forbid());
        return (current, null);
    }

    private static bool Fresh(CurrentAccount current, IConfiguration configuration, DateTimeOffset now) =>
        AccountDeletionPolicy.Fresh(current.Facts.AuthenticatedAt, now,
            TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Accounts:ReauthSeconds", 300), 30, 900)));

    private static IResult Stale() => Problem(StatusCodes.Status403Forbidden, ProblemCodes.StaleSession, "Sign in again to do this.");
    private static IResult NotPending() => Problem(StatusCodes.Status409Conflict, ProblemCodes.DeletionNotPending, "Your account isn't being deleted.");

    public static DeletionView View(Account account, MemberProfile? profile) => new(
        account.Status == AccountStatus.PendingDeletion ? "pending" : "none", account.DeletionRequestedAt, account.DeletionDueAt,
        profile?.FoundingMember == true, account.DeleteFounderToken);

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[]? TryHash(string token)
    {
        if (token.Length != 43 || token.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))) return null;
        var bytes = Convert.FromBase64String(token.Replace('-', '+').Replace('_', '/') + "=");
        return SHA256.HashData(bytes);
    }
}
