using System.Security.Claims;
using System.Text.Json;
using Swoovr.Contracts;
using Swoovr.Domain;

namespace Swoovr.Api.Accounts;

public static class AccountEndpoints
{
    public static IResult Problem(int status, string code, string title) =>
        Results.Problem(statusCode: status, title: title, extensions: new Dictionary<string, object?> { ["code"] = code });

    public static string Wire<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static void Map(WebApplication app)
    {
        app.MapGet("/api/v1/session", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var current = await accounts.ResolveAsync(user, cancellationToken);
            if (current is null) return Results.Unauthorized();
            return Results.Ok(new SessionSummary(
                Wire(current.Facts.Realm), Wire(current.Evidence.Method), current.Evidence.MultiFactor,
                current.Facts.EmailVerified, current.Facts.AdultAttestedAt is not null,
                current.Allows(AccountRole.Member), current.Allows(AccountRole.Admin), current.Allows(AccountRole.Support),
                Wire(current.Beta)));
        }).RequireAuthorization();

        var me = app.MapGroup("/api/v1/me").RequireAuthorization();
        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            return denied ?? Results.Ok(View(current!.Profile!, Today(clock)));
        });
        me.MapPut("/age", async (AgeUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, profile, db) = (current!.Account!, current.Profile!, accounts.Db!);
            var now = clock.GetUtcNow();
            var change = AgePolicy.Evaluate(profile.AgeStatus, update.DateOfBirth, update.Age, DateOnly.FromDateTime(now.UtcDateTime));
            if (change.Error != AgeChangeError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidAge, "That date of birth or age can't be used.");
            profile.DateOfBirth = update.DateOfBirth;
            profile.DeclaredAge = update.DateOfBirth is null ? update.Age : null;
            profile.DeclaredOn = profile.DeclaredAge is null ? null : DateOnly.FromDateTime(now.UtcDateTime);
            if (profile.AgeStatus != change.Status) (profile.AgeStatus, profile.AgeStatusAt) = (change.Status, now);
            // The audit records which kind of entry was made and the outcome, never the date or age.
            var entry = update.DateOfBirth is not null ? "dob" : update.Age is not null ? "age" : "cleared";
            Audit.Record(db, now, account.Id, "account", "age.updated", "account", account.Id.ToString(), $"{entry}:{change.Status}");
            if (change.LocksAccount)
            {
                // D40: an under-18 entry locks the account until support or an admin reviews it.
                account.Status = AccountStatus.Locked;
                account.StatusReason = "underage";
                Audit.Record(db, now, null, "system", "account.locked", "account", account.Id.ToString(), "underage");
            }
            await db.SaveChangesAsync(cancellationToken);
            return change.LocksAccount
                ? Problem(StatusCodes.Status403Forbidden, ProblemCodes.AccountLocked, "This account is locked.")
                : Results.Ok(View(profile, Today(clock)));
        });
    }

    /// <summary>The caller as an active member with a profile, or the response that refuses them.</summary>
    public static async Task<(CurrentAccount? Current, IResult? Denied)> MemberAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null)
            return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        if (current.Account.Status != AccountStatus.Active && current.Facts.Realm == AccountRealm.Members)
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AccountLocked, "This account is locked."));
        if (current.Facts.Realm == AccountRealm.Members && !BetaAccessPolicy.Allows(current.Beta))
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.BetaNotEnrolled, "Swoovr is in private beta."));
        if (!current.Allows(AccountRole.Member)) return (null, Results.Forbid());
        return (current, null);
    }

    public static MemberAccount View(MemberProfile profile, DateOnly today) => new(
        Wire(profile.AgeStatus), AgePolicy.CurrentAge(profile, today), profile.DateOfBirth, profile.DeclaredAge,
        BadgePolicy.For(profile).Select(b => new BadgeView(Wire(b.Badge), b.CheckedAt)).ToList());

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
