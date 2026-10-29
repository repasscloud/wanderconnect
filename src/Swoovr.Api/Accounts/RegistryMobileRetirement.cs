using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using static Swoovr.Api.Accounts.AccountEndpoints;

namespace Swoovr.Api.Accounts;

public static partial class RegistryAdminEndpoints
{
    private static async Task<IResult> RetireMobileAsync(Guid id,MobileBindingRetirementRequest request,ClaimsPrincipal user,
        AccountResolver accounts,IIdentityAdmin identity,ContactHistoryKeys history,TimeProvider clock,CancellationToken token)
    {
        var now=clock.GetUtcNow();
        var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);
        if(denied is not null)return denied;
        if(!request.CircumstancesReviewed||!Reason(request.Reason))
            return Problem(400,ProblemCodes.InvalidRequest,"Confirm the circumstances and ownership review and record a bounded explanation.");
        var db=accounts.Db!;var keys=accounts.Services.GetRequiredService<BannedIdentifierKeys>();
        var source=await (from b in db.BannedIdentifiers join c in db.ModerationCases on b.CaseId equals c.Id
            where b.Id==id select (Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token);
        if(source is null)return Results.NotFound();
        // Source lifecycle first; restoration, member writes and final deletion cannot race binding removal.
        await using var state=await AccountLifecycleLease.AcquireAsync(db,source.Value,token);
        await using var barrier=await PreservationLease.ExclusiveAsync(db,token);
        now=clock.GetUtcNow();
        (current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);
        if(denied is not null)return denied;
        var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);
        if(row is null)return Results.NotFound();
        if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
        if(row.IdentifierType!=BannedIdentifierType.Mobile||row.Status!=BanStatus.Revoked||row.RevokedAt is null||row.RevokedBy is null)
            return Problem(409,ProblemCodes.InvalidRequest,"Explicitly release the reviewed mobile restriction before retiring its binding.");
        var account=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==source.Value&&a.Realm==AccountRealm.Members,token);
        if(account is null||account.Status!=AccountStatus.Active
            ||!await db.ModerationCases.AnyAsync(c=>c.Id==row.CaseId&&c.SubjectAccountId==account.Id,token)
            ||!await db.Sanctions.AnyAsync(s=>s.AccountId==account.Id&&s.CaseId==row.CaseId&&s.Kind==SanctionKind.Termination
                &&s.LiftedAt==null&&s.StartsAt<=now&&(s.EndsAt==null||s.EndsAt>now),token))
            return Problem(409,ProblemCodes.InvalidRequest,"An existing member with the documented active source termination is required.");
        var phone=await db.MemberPhones.SingleOrDefaultAsync(p=>p.AccountId==account.Id,token);
        var completed=await db.BanTransitions.AnyAsync(t=>t.BannedIdentifierId==id&&t.Action=="mobile-binding-retired",token);
        // A previous completion never authorizes removing a later binding, even when the number was reused.
        if(completed)return phone is null?Results.Ok(new IdentifierWorkflowResult(0))
            :Problem(409,ProblemCodes.InvalidRequest,"A later phone binding requires a separate review.");
        if(phone is null)return Problem(409,ProblemCodes.InvalidRequest,"There is no current verified binding to retire.");
        if(!keys.HasVersion(row.KeyVersion))throw BannedIdentifierRegistry.Unavailable();
        if(!CryptographicOperations.FixedTimeEquals(keys.Fingerprint(BannedIdentifierType.Mobile,phone.Number,row.KeyVersion),row.IdentifierHash))
            return Problem(409,ProblemCodes.InvalidRequest,"The current phone binding does not match the reviewed restriction.");
        var matches=await accounts.Services.GetRequiredService<BannedIdentifierRegistry>().FindAsync(BannedIdentifierType.Mobile,phone.Number,now,token);
        if(await ExcludedAsync(db,current.Account.Id,matches,token))return StaffAccessEndpoints.Conflict();
        if(matches.Any(b=>BannedIdentifierPolicy.Blocks(b,now)))
            return Problem(409,ProblemCodes.InvalidRequest,"Other active restrictions require separate review before retiring this binding.");
        var previous=history.Record(account.Id,ContactType.Mobile,phone.Number,phone.VerifiedAt,now,current.Account.Id,"staff-binding-retirement");
        if(previous is null)return Problem(503,ProblemCodes.NotConfigured,"Restricted contact preservation is unavailable; the binding was retained.");
        // Remote logout is idempotent. Uncertainty never frees the unique binding. A later DB failure safely retries.
        if(!await identity.SignOutEverywhereAsync(account.Subject,token))
            return Problem(503,ProblemCodes.NotConfigured,"Session revocation did not complete; the binding was retained. Retry after provider recovery.");
        now=clock.GetUtcNow();previous.ValidUntil=now;previous.RetainUntil=now+ContactHistoryPolicy.Retention;
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        // Provider logout does not invalidate self-contained JWTs. Preserve a monotonic API auth-time cutoff too.
        var cutoff=SessionRevocationPolicy.Cutoff(now);
        if(account.SessionsRevokedAt is null||account.SessionsRevokedAt<cutoff)account.SessionsRevokedAt=cutoff;
        db.ContactHistories.Add(previous);db.MemberPhones.Remove(phone);
        // Preserve code history while making this old contact's pending challenges unusable after later recovery.
        await db.PhoneCodes.Where(c=>c.AccountId==account.Id&&c.Number==phone.Number&&c.UsedAt==null)
            .ExecuteUpdateAsync(update=>update.SetProperty(c=>c.UsedAt,now),token);
        var(review,note)=BannedIdentifierPolicy.RecordMobileRetirement(row,current.Account.Id,request.Reason,now);
        db.BanTransitions.Add(review);db.BanCaseNotes.Add(note);
        Audit.Record(db,now,current.Account.Id,"account","registry.mobile-binding-retired","registry-entry",id.ToString());
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        return Results.Ok(new IdentifierWorkflowResult(1));
    }
}
