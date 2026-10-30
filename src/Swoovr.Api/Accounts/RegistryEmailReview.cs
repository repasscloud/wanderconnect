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
    private static async Task<IResult> ReviewEmailAsync(Guid id,EmailBindingRetirementRequest request,ClaimsPrincipal user,
        AccountResolver accounts,IContactIdentityAdmin provider,EmailRetirementCommands commands,TimeProvider clock,CancellationToken token)
    {
        var now=clock.GetUtcNow();var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);
        if(denied is not null)return denied;
        if(!request.CircumstancesReviewed||!Reason(request.Reason))return Problem(400,ProblemCodes.InvalidRequest,"Review the case, current circumstances and ownership and record a bounded explanation.");
        if(!provider.ContactRetirementConfigured)return Problem(503,ProblemCodes.NotConfigured,"Reviewed email retirement is not configured.");
        var db=accounts.Db!;
        var source=await db.EmailRetirementOperations.Where(o=>o.RegistryId==id).Select(o=>(Guid?)o.AccountId).SingleOrDefaultAsync(token);
        if(source is null)return Results.NotFound();
        await using var lifecycle=await AccountLifecycleLease.AcquireManyAsync(db,[source.Value,current!.Account!.Id],token);
        await using var authority=await PreservationLease.SharedAsync(db,token);
        now=clock.GetUtcNow();(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
        var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);if(row is null)return Results.NotFound();
        if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
        var operation=await db.EmailRetirementOperations.SingleOrDefaultAsync(o=>o.RegistryId==id,token);
        if(operation is null)return Results.NotFound();await db.Entry(operation).ReloadAsync(token);
        if(operation.State==EmailRetirementState.Completed)return Results.Ok(EmailView(operation));
        var account=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==source.Value,token);
        if(account is null||account.Realm!=AccountRealm.Members||account.Status!=AccountStatus.Active||account.EmailRetiredAt is null||account.Email is not null
            ||account.Issuer!=provider.ContactIssuer||!Guid.TryParse(account.Subject,out var subject)||operation.CaseId!=row.CaseId
            ||row.IdentifierType!=BannedIdentifierType.Email||row.Status!=BanStatus.Revoked||row.RevokedAt is null||row.RevokedBy is null
            ||!await db.ModerationCases.AnyAsync(c=>c.Id==row.CaseId&&c.SubjectAccountId==account.Id,token)
            ||!await db.Sanctions.AnyAsync(s=>s.AccountId==account.Id&&s.CaseId==row.CaseId&&s.Kind==SanctionKind.Termination
                &&s.LiftedAt==null&&s.StartsAt<=now&&(s.EndsAt==null||s.EndsAt>now),token))
            return Problem(409,ProblemCodes.InvalidRequest,"The documented source termination and reviewed released binding are required.");
        var status=await provider.EmailRetirementStatusAsync(subject,operation.Id,token);
        if(status==IdentityContactState.Retired)
        {
            // A confirmed old effect needs reconciliation, not a new approval or another provider write.
            now=clock.GetUtcNow();(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
            await using var reconciliation=await db.Database.BeginTransactionAsync(token);
            // Keep new effects disabled if the observed receipt later becomes unavailable or inconsistent.
            operation.State=EmailRetirementState.NeedsReview;operation.NextAttemptAt=now;operation.UpdatedAt=now;operation.Outcome="reconcile-requested";
            var(reconciled,record)=BannedIdentifierPolicy.RecordReinstatement(row,current!.Account!.Id,request.Reason,now);reconciled.Action="email-reconcile-requested";
            db.BanTransitions.Add(reconciled);db.BanCaseNotes.Add(record);
            Audit.Record(db,now,current.Account.Id,"account","registry.email-reconcile-requested","retirement",operation.Id.ToString());
            await db.SaveChangesAsync(token);await reconciliation.CommitAsync(token);return Results.Accepted(value:EmailView(operation));
        }
        if(status!=IdentityContactState.NotRecorded)return Problem(503,ProblemCodes.NotConfigured,"Provider completion cannot be confirmed. No new attempt was approved.");
        IdentityEmailBinding expected;
        try{expected=commands.Open(operation,provider.ContactIssuer);}
        catch(Exception error)when(error is CryptographicException or KeyNotFoundException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {return Problem(503,ProblemCodes.NotConfigured,"The restricted command cannot be read. No new attempt was approved.");}
        if(expected.Subject!=subject||expected.Email is null)return Problem(409,ProblemCodes.InvalidRequest,"The documented source binding needs separate restricted review.");
        var keys=accounts.Services.GetRequiredService<BannedIdentifierKeys>();if(!keys.HasVersion(row.KeyVersion))throw BannedIdentifierRegistry.Unavailable();
        if(!CryptographicOperations.FixedTimeEquals(keys.Fingerprint(BannedIdentifierType.Email,expected.Email,row.KeyVersion),row.IdentifierHash))
            return Problem(409,ProblemCodes.InvalidRequest,"The command no longer matches the reviewed restriction.");
        var matches=await accounts.Services.GetRequiredService<BannedIdentifierRegistry>().FindAsync(BannedIdentifierType.Email,expected.Email,clock.GetUtcNow(),token);
        if(await ExcludedAsync(db,current.Account.Id,matches,token))return StaffAccessEndpoints.Conflict();
        if(matches.Any(b=>BannedIdentifierPolicy.Blocks(b,clock.GetUtcNow())))return Problem(409,ProblemCodes.InvalidRequest,"Other active restrictions require separate review.");
        var read=await provider.ReadEmailBindingAsync(subject,token);
        if(read.State!=IdentityContactState.Available)return Problem(503,ProblemCodes.NotConfigured,"The provider binding cannot be confirmed. No new attempt was approved.");
        if(read.Binding!=expected)return Problem(409,ProblemCodes.InvalidRequest,"The provider binding changed. Separate restricted recovery review is required; no new attempt was approved.");
        now=clock.GetUtcNow();(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
        if(await ExcludedAsync(db,current!.Account!.Id,matches.Append(row),token))return StaffAccessEndpoints.Conflict();
        var grant=await StaffAccessEndpoints.GrantAsync(db,current.Account.Id,StaffAccessPolicy.IdentifierRelease,now,token);
        if(grant is null)return StaffAccessEndpoints.NeedsGrant(StaffAccessPolicy.IdentifierRelease);
        var review=new EmailRetirementReview{Id=Guid.NewGuid(),OperationId=operation.Id,ActorId=current.Account.Id,AccessGrantId=grant.Id,ReviewedAt=now,Reason=request.Reason.Trim()};
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        db.EmailRetirementReviews.Add(review);await db.SaveChangesAsync(token);
        EmailRetirementPolicy.ApproveResume(operation,review,now);
        var(transition,note)=BannedIdentifierPolicy.RecordReinstatement(row,current.Account.Id,request.Reason,now);transition.Action="email-retirement-reviewed";
        db.BanTransitions.Add(transition);db.BanCaseNotes.Add(note);
        Audit.Record(db,now,current.Account.Id,"account","registry.email-retirement-reviewed","retirement",operation.Id.ToString(),$"grant:{grant.Id};review:{review.Id}");
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return Results.Accepted(value:EmailView(operation));
    }
}
