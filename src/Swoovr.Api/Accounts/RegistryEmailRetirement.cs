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
    private static EmailBindingRetirementView EmailView(EmailRetirementOperation operation)=>new(operation.Id,operation.State.ToString(),operation.RequestedAt,operation.UpdatedAt,operation.CompletedAt);
    private static async Task<IResult> RetireEmailAsync(Guid id,EmailBindingRetirementRequest request,ClaimsPrincipal user,
        AccountResolver accounts,IContactIdentityAdmin provider,ContactHistoryKeys history,EmailRetirementCommands commands,TimeProvider clock,CancellationToken token)
    {
        var now=clock.GetUtcNow();var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);
        if(denied is not null)return denied;
        if(!request.CircumstancesReviewed||!Reason(request.Reason))return Problem(400,ProblemCodes.InvalidRequest,"Confirm circumstances and ownership review and record a bounded explanation.");
        if(!provider.ContactRetirementConfigured)return Problem(503,ProblemCodes.NotConfigured,"Reviewed email retirement is not configured.");
        var db=accounts.Db!;
        var source=await (from b in db.BannedIdentifiers join c in db.ModerationCases on b.CaseId equals c.Id where b.Id==id select(Guid?)c.SubjectAccountId).SingleOrDefaultAsync(token);
        if(source is null)return Results.NotFound();
        await using var lifecycle=await AccountLifecycleLease.AcquireManyAsync(db,[source.Value,current!.Account!.Id],token);
        await using var authority=await PreservationLease.SharedAsync(db,token);
        now=clock.GetUtcNow();(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
        var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);if(row is null)return Results.NotFound();
        if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
        var account=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==source.Value&&a.Realm==AccountRealm.Members,token);
        if(account is null||account.Status!=AccountStatus.Active||account.Issuer!=provider.ContactIssuer||!Guid.TryParse(account.Subject,out var subject)
            ||row.IdentifierType!=BannedIdentifierType.Email||row.Status!=BanStatus.Revoked||row.RevokedAt is null||row.RevokedBy is null
            ||!await db.Sanctions.AnyAsync(s=>s.AccountId==account.Id&&s.CaseId==row.CaseId&&s.Kind==SanctionKind.Termination
                &&s.LiftedAt==null&&s.StartsAt<=now&&(s.EndsAt==null||s.EndsAt>now),token))
            return Problem(409,ProblemCodes.InvalidRequest,"A documented active source termination, explicitly released email restriction and matching member authority are required.");
        var existing=await db.EmailRetirementOperations.SingleOrDefaultAsync(o=>o.AccountId==account.Id,token);
        if(existing is not null)return existing.RegistryId==id?Results.Ok(EmailView(existing))
            :Problem(409,ProblemCodes.InvalidRequest,"An existing retirement needs its own restricted review.");
        if(account.Email is not{}email||account.EmailRetiredAt is not null)return Problem(409,ProblemCodes.InvalidRequest,"No current accepted email binding is available for this review.");
        var keys=accounts.Services.GetRequiredService<BannedIdentifierKeys>();if(!keys.HasVersion(row.KeyVersion))throw BannedIdentifierRegistry.Unavailable();
        if(!CryptographicOperations.FixedTimeEquals(keys.Fingerprint(BannedIdentifierType.Email,email,row.KeyVersion),row.IdentifierHash))
            return Problem(409,ProblemCodes.InvalidRequest,"The current email does not match the reviewed restriction.");
        var matches=await accounts.Services.GetRequiredService<BannedIdentifierRegistry>().FindAsync(BannedIdentifierType.Email,email,now,token);
        if(await ExcludedAsync(db,current.Account.Id,matches,token))return StaffAccessEndpoints.Conflict();
        if(matches.Any(b=>BannedIdentifierPolicy.Blocks(b,now)))return Problem(409,ProblemCodes.InvalidRequest,"Other active restrictions require separate review.");
        var read=await provider.ReadEmailBindingAsync(subject,token);
        if(read.State!=IdentityContactState.Available)return Problem(503,ProblemCodes.NotConfigured,"The provider binding cannot be confirmed. No binding was changed.");
        if(read.Binding is not{}binding||!binding.EmailVerified||!binding.Enabled
            ||binding.Email is null||EmailAddress.Normalise(binding.Email)!=email||string.IsNullOrWhiteSpace(binding.Username))
            return Problem(409,ProblemCodes.InvalidRequest,"The current provider binding could not be positively matched. No binding was changed.");
        // Provider read may outlast the human grant. Recheck before committing new durable authority.
        now=clock.GetUtcNow();(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,now,token);if(denied is not null)return denied;
        if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
        var grant=await StaffAccessEndpoints.GrantAsync(db,current.Account.Id,StaffAccessPolicy.IdentifierRelease,now,token);
        if(grant is null)return StaffAccessEndpoints.NeedsGrant(StaffAccessPolicy.IdentifierRelease);
        var operation=new EmailRetirementOperation{Id=Guid.NewGuid(),AccountId=account.Id,RegistryId=id,CaseId=row.CaseId,RequestedBy=current.Account.Id,
            AccessGrantId=grant.Id,RequestedAt=now,UpdatedAt=now,NextAttemptAt=now};
        var sealedCommand=commands.Seal(operation,account.Issuer,binding);
        var archived=history.Record(account.Id,ContactType.Email,email,account.EmailAcceptedAt,now,current.Account.Id,"staff-email-retirement");
        if(sealedCommand is null||archived is null)return Problem(503,ProblemCodes.NotConfigured,"Restricted contact preservation is unavailable. No binding was changed.");
        operation.EncryptedCommand=sealedCommand;
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        account.Email=null;account.EmailRetiredAt=now;account.EmailAcceptedAt=now;
        var cutoff=SessionRevocationPolicy.Cutoff(now);if(account.SessionsRevokedAt is null||account.SessionsRevokedAt<cutoff)account.SessionsRevokedAt=cutoff;
        db.ContactHistories.Add(archived);
        // No account FK: restricted command history may outlive source deletion. Establish guarded source state first
        // inside this same transaction; neither source refusal nor intent is externally visible before commit.
        await db.SaveChangesAsync(token);
        db.EmailRetirementOperations.Add(operation);
        var(review,note)=BannedIdentifierPolicy.RecordReinstatement(row,current.Account.Id,request.Reason,now);review.Action="email-retirement-requested";
        db.BanTransitions.Add(review);db.BanCaseNotes.Add(note);
        Audit.Record(db,now,current.Account.Id,"account","registry.email-retirement-requested","retirement",operation.Id.ToString());
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        // Durable intent, source refusal and mail suppression commit before the background provider attempt.
        return Results.Accepted(value:EmailView(operation));
    }
    private static async Task<IResult> EmailRetirementAsync(Guid id,ClaimsPrincipal user,AccountResolver accounts,TimeProvider clock,HttpContext http,CancellationToken token)
    {
        var(current,denied)=await AuthorizeAsync(user,accounts,StaffAccessPolicy.IdentifierRelease,clock.GetUtcNow(),token);if(denied is not null)return denied;
        var db=accounts.Db!;await using var authority=await PreservationLease.SharedAsync(db,token);
        var row=await db.BannedIdentifiers.SingleOrDefaultAsync(b=>b.Id==id,token);if(row is null)return Results.NotFound();
        if(await ExcludedAsync(db,current!.Account!.Id,[row],token))return StaffAccessEndpoints.Conflict();
        var operation=await db.EmailRetirementOperations.SingleOrDefaultAsync(o=>o.RegistryId==id,token);
        http.Response.Headers.CacheControl="no-store";return operation is null?Results.NotFound():Results.Ok(EmailView(operation));
    }
}
