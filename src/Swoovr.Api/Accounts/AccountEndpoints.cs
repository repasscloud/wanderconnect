using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Profiles.ProfileEndpoints;

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
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true);
            return denied ?? Results.Ok(View(current!.Profile!, Today(clock), await BadgeOrderAsync(accounts.Db!, cancellationToken)));
        });
        me.MapPost("/coupons", RedeemAsync);
        me.MapPut("/age", async (AgeUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, profile, db) = (current!.Account!, current.Profile!, accounts.Db!);
            var now = clock.GetUtcNow();
            // D139 (supersedes D69): a date of birth is required; an age alone or clearing it is refused.
            if (update.DateOfBirth is null)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.DateOfBirthRequired, "Enter your date of birth.");
            // DEFAULT (D157): once ID verification confirmed the age, a different date of birth needs support.
            if (profile.AgeStatus == AgeStatus.Verified && profile.DateOfBirth is not null && profile.DateOfBirth != update.DateOfBirth)
                return Problem(StatusCodes.Status409Conflict, ProblemCodes.DateOfBirthLocked, "Your age was confirmed by ID verification. Contact Swoovr support to change it.");
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
                : Results.Ok(View(profile, Today(clock), await BadgeOrderAsync(db, cancellationToken)));
        });
    }

    /// <summary>
    /// The coupon code screen. Founder codes (FOUNDER- prefix) go to the founder check (D65);
    /// there are no ordinary coupons yet. Every refusal reads the same, and failed attempts are
    /// audited and limited per account.
    /// </summary>
    private static async Task<IResult> RedeemAsync(CouponRequest request, ClaimsPrincipal user, AccountResolver accounts, Founders founders,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
        if (denied is not null) return denied;
        var (account, profile, db) = (current!.Account!, current.Profile!, accounts.Db!);
        var now = clock.GetUtcNow();
        if (await founders.RecentFailuresAsync(account.Id, now, cancellationToken) >= Founders.AttemptsPerHour)
            return Problem(StatusCodes.Status429TooManyRequests, ProblemCodes.TooManyAttempts, "Too many attempts. Try again later.");
        FounderToken? token = null;
        var outcome = FounderRedeemOutcome.Rejected;
        if (FounderTokenCodec.IsFounderCode(request.Code) && FounderTokenCodec.TryParse(request.Code, out var canonical, out var version))
        {
            token = await db.FounderTokens.SingleOrDefaultAsync(t => t.Code == canonical, cancellationToken);
            outcome = FounderRedeemPolicy.Evaluate(token, canonical, version, current.Facts.EmailVerified, current.VerifiedEmail,
                founders.Secrets.GetValueOrDefault(version), profile);
            // One live account per place; another live holder means this account cannot take it.
            if (outcome == FounderRedeemOutcome.Restored
                && await db.MemberProfiles.AnyAsync(p => p.FounderPlace == token!.Place && p.AccountId != account.Id, cancellationToken))
                outcome = FounderRedeemOutcome.Rejected;
        }
        if (outcome == FounderRedeemOutcome.Rejected)
        {
            var kind = FounderTokenCodec.IsFounderCode(request.Code) ? "founder" : "other";
            Audit.Record(db, now, account.Id, "account", "coupon.rejected", "account", account.Id.ToString(), kind);
            await db.SaveChangesAsync(cancellationToken);
            return Problem(StatusCodes.Status400BadRequest, ProblemCodes.CouponInvalid, "That code can't be used.");
        }
        if (outcome == FounderRedeemOutcome.Restored)
        {
            var place = await db.FounderPlaces.SingleAsync(p => p.Place == token!.Place, cancellationToken);
            // Restoring keeps the original start, so it never restarts the free Plus period (D65).
            (profile.FoundingMember, profile.FounderPlace, profile.FoundingSince) = (true, place.Place, place.GrantedAt);
            Audit.Record(db, now, account.Id, "account", "founder.restored", "founder-token", token!.Id.ToString(), $"place:{place.Place}");
            try { await db.SaveChangesAsync(cancellationToken); }
            catch (DbUpdateException)
            {
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.CouponInvalid, "That code can't be used.");
            }
        }
        return Results.Ok(View(profile, DateOnly.FromDateTime(now.UtcDateTime), await BadgeOrderAsync(db, cancellationToken)));
    }

    /// <summary>The caller as an active member with a profile, or the response that refuses them.</summary>
    /// <remarks>
    /// D127: an account flagged NeedsVerification is refused everywhere (403 age-verification-required) except the
    /// routes that pass <paramref name="ageGateExempt"/>: its own account summary and ID verification.
    /// </remarks>
    /// <remarks>
    /// D133: a suspended or terminated account is refused (403 account-suspended) except where
    /// <paramref name="barredExempt"/> is passed: its account summary, notices and appeals.
    /// </remarks>
    public static async Task<(CurrentAccount? Current, IResult? Denied)> MemberAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken,
        bool ageGateExempt = false, bool barredExempt = false)
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
        // Sanctions are read on every request, so an action applies at once to every client and live session (D133).
        var sanctions = await accounts.Db!.Sanctions.Where(s => s.AccountId == current.Account.Id && s.LiftedAt == null).ToListAsync(cancellationToken);
        var enforcement = Enforcement.For(sanctions, DateTimeOffset.UtcNow);
        if (enforcement.Barred && !barredExempt)
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AccountSuspended,
                enforcement.Terminated ? "This account has been closed." : "This account is suspended."));
        if (!ageGateExempt && current.Profile is { } profile && AgeAssurance.Blocks(profile.AgeStatus))
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AgeVerificationRequired, "Verify your age to keep using Swoovr."));
        return (current with { Enforcement = enforcement }, null);
    }

    public static MemberAccount View(MemberProfile profile, DateOnly today, IReadOnlyList<Badge> badgeOrder) => new(
        Wire(profile.AgeStatus), AgePolicy.CurrentAge(profile, today), profile.DateOfBirth, profile.DeclaredAge,
        Swoovr.Api.Profiles.ProfileEndpoints.Badges(profile, badgeOrder));

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
