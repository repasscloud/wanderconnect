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
        app.MapGet("/api/session", async (ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var current = await accounts.ResolveAsync(user, cancellationToken);
            if (current is null) return Results.Unauthorized();
            return Results.Ok(new SessionSummary(
                Wire(current.Facts.Realm), Wire(current.Evidence.Method), current.Evidence.MultiFactor,
                current.Facts.EmailVerified, current.Facts.AdultAttestedAt is not null,
                current.Allows(AccountRole.Member), current.Allows(AccountRole.Admin), current.Allows(AccountRole.Support),
                Wire(current.Beta), current.EmailChangeProblem));
        }).RequireAuthorization();

        var me = app.MapGroup("/api/me").RequireAuthorization();
        me.MapGet("", async (ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, barredExempt: true, pendingDeletionExempt: true,mfaExempt:true);
            if (denied is not null) return denied;
            // D170: a pending deletion outranks any other state; clients show the deletion screen.
            var state = current!.Account!.Status == AccountStatus.PendingDeletion ? "pendingDeletion" : current.Enforcement.State;
            // #190 §10 (D323): an active sign-in lock outranks sanctions here; the member sees the locked screen and its public reference.
            var lockNow = clock.GetUtcNow();
            var lockReference = await accounts.Db!.AccountLocks.AsNoTracking()
                .Where(l => l.AccountId == current.Account.Id && l.RemovedAt == null && (l.ExpiresAt == null || l.ExpiresAt > lockNow))
                .Select(l => l.CaseReference).FirstOrDefaultAsync(cancellationToken);
            if (lockReference is not null && state != "pendingDeletion") state = "locked";
            var phone = await accounts.Db!.MemberPhones.AnyAsync(p => p.AccountId == current.Account.Id, cancellationToken);
            var policyRequired = accounts.PolicyVersionSelected&&await accounts.Services.GetRequiredService<Swoovr.Api.Policies.PolicyAcceptanceService>().RequiredAsync(current.Account.Id,cancellationToken);
            var namesRequired = !RegisteredNamePolicy.Complete(current.Account);
            return Results.Ok(View(current.Profile!, Today(clock), await BadgeOrderAsync(accounts.Db!, cancellationToken), state) with
                { PhoneVerified = phone, PhoneRequired = accounts.PhoneRequired && !phone, PhoneGateEnabled=accounts.PhoneRequired, EmailChangeProblem = current.EmailChangeProblem, PolicyRequired=policyRequired,
                    // Issue 254 (D342): one onboarding order for every client, the same order MemberAsync refuses routes in.
                    SetupStep = AccountSetup.Next(namesRequired, accounts.PhoneRequired && !phone, policyRequired, current.Profile!.DateOfBirth),
                    RegisteredNamesRequired=namesRequired, MfaAccessRequired=current.Account.MfaResetPending || current.Account.MfaRequired&&!current.Evidence.MultiFactor,
                    DateOfBirthCorrectionUntil=await DobChangeEndpoints.UntilAsync(accounts.Db!,current.Account.Id,clock.GetUtcNow(),cancellationToken),
                    StatusReference = state == "locked" ? lockReference : null,
                    RestrictionLimits = state == "restricted" ? current.Enforcement.Limits : null,
                    RestrictedUntil = state == "restricted" ? current.Enforcement.RestrictedUntil : null });
        });
        // Issue 254 (D342): the member's own contact details for Account settings, masked unless they ask to reveal them.
        me.MapGet("/contact", async (bool? reveal, ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken, ageGateExempt: true, phoneExempt: true, policyExempt: true, registeredNamesExempt: true);
            if (denied is not null) return denied;
            var number = await accounts.Db!.MemberPhones.AsNoTracking().Where(p => p.AccountId == current!.Account!.Id).Select(p => p.Number).FirstOrDefaultAsync(cancellationToken);
            var email = current!.Account!.Email;
            return reveal == true
                ? Results.Ok(new ContactView(email, number, true))
                : Results.Ok(new ContactView(ContactMask.Email(email), number is null ? null : PhoneVerificationPolicy.Masked(number), false));
        });
        me.MapPost("/coupons", RedeemAsync);
        me.MapPut("/age", async (AgeUpdate update, ClaimsPrincipal user, AccountResolver accounts, TimeProvider clock, CancellationToken cancellationToken) =>
        {
            var (current, denied) = await MemberAsync(user, accounts, cancellationToken);
            if (denied is not null) return denied;
            var (account, profile, db) = (current!.Account!, current.Profile!, accounts.Db!);
            await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,account.Id,cancellationToken);
            await db.Entry(account).ReloadAsync(cancellationToken);
            await db.Entry(profile).ReloadAsync(cancellationToken);
            if(account.Status!=AccountStatus.Active)return Problem(403,ProblemCodes.AccountLocked,"This account is locked.");
            (current,denied)=await MemberAsync(user,accounts,cancellationToken);
            if(denied is not null)return denied;
            await using var preservation=await Swoovr.Api.Retention.PreservationLease.SharedAsync(db,cancellationToken);
            await using var transaction=await db.Database.BeginTransactionAsync(cancellationToken);
            var now = clock.GetUtcNow();
            // D139 (supersedes D69): a date of birth is required; an age alone or clearing it is refused.
            if (update.DateOfBirth is null)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.DateOfBirthRequired, "Enter your date of birth.");
            // D166's provider-mismatch prompt remains independent of D299's Admin-authorised correction.
            if (!DobCorrectionPolicy.OrdinaryChangeAllowed(profile.DobCheck))
                return profile.DobCheck == DobCheck.CorrectionNeeded
                    ? Problem(StatusCodes.Status409Conflict, ProblemCodes.DobCorrectionRequired, "Set your date of birth to the one on your ID to finish verification.")
                    : Problem(StatusCodes.Status409Conflict, ProblemCodes.DobUnderReview, "Swoovr is checking your date of birth. Contact support to change it.");
            var change = AgePolicy.Evaluate(profile.AgeStatus, update.DateOfBirth, update.Age, DateOnly.FromDateTime(now.UtcDateTime));
            if (change.Error != AgeChangeError.None)
                return Problem(StatusCodes.Status400BadRequest, ProblemCodes.InvalidAge, "That date of birth or age can't be used.");
            if(profile.DateOfBirth==update.DateOfBirth)return Results.Ok(View(profile,Today(clock),await BadgeOrderAsync(db,cancellationToken)));
            var grant=await db.DobChangeGrants.Where(g=>g.AccountId==account.Id&&g.ConsumedAt==null&&g.RevokedAt==null&&g.ExpiresAt>now&&g.CreatedAt<=now)
                .OrderByDescending(g=>g.CreatedAt).FirstOrDefaultAsync(cancellationToken);
            if(profile.DateOfBirth is not null&&!DobChangePolicy.Live(grant,account.Id,now))
                return Problem(409,ProblemCodes.DateOfBirthLocked,"Your saved date of birth is locked. Request a correction at https://swoovr.com/support.");
            if(!DobChangePolicy.CanSave(profile.DateOfBirth,update.Confirmed,grant,account.Id,now))
                return Problem(400,ProblemCodes.DobConfirmationRequired,"Confirm this date is correct before saving. It will then be locked.");
            var keys=accounts.Services.GetRequiredService<Swoovr.Api.Verification.IdentityKeys>();
            if(profile.DateOfBirth is not null&&keys.CurrentVersion is null)return Problem(503,ProblemCodes.NotConfigured,"Date correction is temporarily unavailable.");
            var storedIdentity=profile.IdStatus==VerificationStatus.Verified?await db.VerifiedIdentities.FindAsync([account.Id],cancellationToken):null;
            if(storedIdentity is not null&&(storedIdentity.Sealed.Length==0||!keys.Encryption.ContainsKey(storedIdentity.Sealed[0])))
                return Problem(503,ProblemCodes.NotConfigured,"The stored identity comparison is temporarily unavailable.");
            if(profile.DateOfBirth is not null&&!await DobChangeEndpoints.ConsumeAsync(db,grant!.Id,account.Id,now,cancellationToken))
                return Problem(409,ProblemCodes.DateOfBirthLocked,"This correction is no longer available.");
            DobChangeEndpoints.PreservePrevious(db,keys,profile,update.DateOfBirth.Value,grant?.Id,now);
            if(profile.DateOfBirth is not null)Audit.Record(db,now,account.Id,"account","dob.correction-used","account",account.Id.ToString(),grant!.Id.ToString());
            (profile.DateOfBirthVerified,profile.DateOfBirthVerifiedAt)=(false,null);
            if(profile.IdStatus==VerificationStatus.Verified)
            {
                VerifiedIdentityFields? identity;
                try{identity=await accounts.Services.GetRequiredService<Swoovr.Api.Verification.VerifiedIdentityStore>().OwnAsync(account.Id,cancellationToken);}
                catch(Exception error)when(error is System.Security.Cryptography.CryptographicException or JsonException)
                {return Problem(503,ProblemCodes.NotConfigured,"The stored identity comparison is temporarily unavailable.");}
                var matched=IdVerificationPolicy.DateOfBirthMatches(update.DateOfBirth,identity?.DateOfBirth);
                (profile.DateOfBirthVerified,profile.DateOfBirthVerifiedAt)=(matched,matched?now:null);
                (profile.DobCheck,profile.DobCheckAt)=(matched?DobCheck.None:DobCheck.ManualReview,now);
                Audit.Record(db,now,account.Id,"account",matched?"dob.verified":"dob.review-opened","account",account.Id.ToString(),"admin-authorised-correction");
                if(!matched&&!await db.DobReviews.AnyAsync(r=>r.AccountId==account.Id&&r.ResolvedAt==null,cancellationToken))
                {
                    var attempt=await db.IdVerificationAttempts.Where(a=>a.AccountId==account.Id&&a.State==IdAttemptState.Approved).OrderByDescending(a=>a.CompletedAt).FirstOrDefaultAsync(cancellationToken);
                    db.DobReviews.Add(new DobReview{Id=Guid.NewGuid(),AccountId=account.Id,AttemptId=attempt?.Id??Guid.Empty,Reason="admin-correction-mismatch",CreatedAt=now});
                }
            }
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
            await transaction.CommitAsync(cancellationToken);
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
    /// D170: an account pending deletion is refused (403 account-deletion-pending) except where
    /// <paramref name="pendingDeletionExempt"/> is passed (its account summary).
    /// </remarks>
    /// <remarks>
    /// D133: a suspended or terminated account is refused (403 account-suspended) except where
    /// <paramref name="barredExempt"/> is passed: its account summary, notices and appeals.
    /// </remarks>
    public static async Task<(CurrentAccount? Current, IResult? Denied)> MemberAsync(ClaimsPrincipal user, AccountResolver accounts, CancellationToken cancellationToken,
        bool ageGateExempt = false, bool barredExempt = false, bool pendingDeletionExempt = false, bool phoneExempt = false,bool policyExempt=false,bool registeredNamesExempt=false,bool mfaExempt=false)
    {
        var current = await accounts.ResolveAsync(user, cancellationToken);
        if (current is null) return (null, Results.Unauthorized());
        if (current.Account is null)
            return (null, Problem(StatusCodes.Status503ServiceUnavailable, ProblemCodes.PersistenceUnavailable, "Accounts are not available."));
        if (!mfaExempt && !barredExempt && current.Facts.Realm == AccountRealm.Members && (current.Account.MfaResetPending || current.Account.MfaRequired && !current.Evidence.MultiFactor))
            return (null, Problem(403,"mfa-required","Configure and verify MFA before continuing."));
        // D170: while deletion is pending only the account summary and the deletion routes (DeletionEndpoints) answer.
        if (current.Account.Status == AccountStatus.PendingDeletion && current.Facts.Realm == AccountRealm.Members)
        {
            if (!pendingDeletionExempt)
                return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AccountDeletionPending, "Your account is being deleted."));
            if (current.Profile is null) return (null, Results.Forbid());
            return (current, null);
        }
        if (current.Account.Status != AccountStatus.Active && current.Facts.Realm == AccountRealm.Members)
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AccountLocked, "This account is locked."));
        // #190 §10 (D323): a staff sign-in lock is read on every request, so no session, token or cached client gets past it.
        // Only the account summary, notices and appeals (barredExempt) stay open; they show the locked screen and reference.
        if (current.Facts.Realm == AccountRealm.Members && !barredExempt)
        {
            var now = DateTimeOffset.UtcNow;
            var held = await accounts.Db!.AccountLocks.AsNoTracking()
                .Where(l => l.AccountId == current.Account.Id && l.RemovedAt == null && (l.ExpiresAt == null || l.ExpiresAt > now))
                .Select(l => l.CaseReference).FirstOrDefaultAsync(cancellationToken);
            if (held is not null)
                return (null, Results.Problem(statusCode: 403, title: "This account is locked.",
                    extensions: new Dictionary<string, object?> { ["code"] = ProblemCodes.AccountLocked, ["reference"] = held }));
        }
        if (current.Facts.Realm == AccountRealm.Members && !BetaAccessPolicy.Allows(current.Beta))
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.BetaNotEnrolled, "Swoovr is in private beta."));
        if (!current.Allows(AccountRole.Member)) return (null, Results.Forbid());
        // Sanctions are read on every request, so an action applies at once to every client and live session (D133).
        var sanctions = await accounts.Db!.Sanctions.Where(s => s.AccountId == current.Account.Id && s.LiftedAt == null).ToListAsync(cancellationToken);
        var enforcement = Enforcement.For(sanctions, DateTimeOffset.UtcNow);
        // D325 (Q132 B, supersedes D160): suspension and termination bar the member; a restriction limits only what it names,
        // checked by each feature (discovery, requests, messages, photo uploads).
        if (enforcement.Barred && !barredExempt)
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AccountSuspended, enforcement.Terminated ? "This account has been closed." : "This account is suspended."));
        if (!ageGateExempt && current.Profile is { } profile && AgeAssurance.Blocks(profile.AgeStatus))
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.AgeVerificationRequired, "Verify your age to keep using Swoovr."));
        if(!barredExempt&&!registeredNamesExempt&&!RegisteredNamePolicy.Complete(current.Account))
            return(null,Problem(403,ProblemCodes.RegisteredNamesRequired,"Confirm or complete your registered names under Me before continuing."));
        // D148: with the gate on, a verified mobile number comes first (the account summary, notices, appeals, deletion
        // and the phone routes stay open).
        if (accounts.PhoneRequired && !phoneExempt && !barredExempt && current.Facts.Realm == AccountRealm.Members
            && !await accounts.Db.MemberPhones.AnyAsync(p => p.AccountId == current.Account.Id, cancellationToken))
            return (null, Problem(StatusCodes.Status403Forbidden, ProblemCodes.PhoneVerificationRequired, "Verify your mobile number to keep using Swoovr."));
        if(!barredExempt&&!policyExempt&&accounts.PolicyVersionSelected&&await accounts.Services.GetRequiredService<Swoovr.Api.Policies.PolicyAcceptanceService>().RequiredAsync(current.Account.Id,cancellationToken))
            return(null,Problem(403,ProblemCodes.PolicyAcceptanceRequired,"Review and explicitly accept the currently required approved policies under Me."));
        if(!enforcement.Barred&&current.Facts.Realm==AccountRealm.Members&&current.Profile is {Bot:false}
            &&accounts.Services.GetService<Swoovr.Api.Analytics.BetaJourneyRecorder>() is{}journey)
            await journey.RecordAsync(current.Account.Id,false,false,cancellationToken);
        return (current with { Enforcement = enforcement }, null);
    }

    public static MemberAccount View(MemberProfile profile, DateOnly today, IReadOnlyList<Badge> badgeOrder, string accountState = "active") => new(
        Wire(profile.AgeStatus), AgePolicy.CurrentAge(profile, today), profile.DateOfBirth, profile.DeclaredAge,
        Swoovr.Api.Profiles.ProfileEndpoints.Badges(profile, badgeOrder), profile.DateOfBirthVerified, accountState, Wire(profile.DobCheck));

    private static DateOnly Today(TimeProvider clock) => DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
}
