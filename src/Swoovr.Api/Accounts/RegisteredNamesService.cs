using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

public static class RegisteredNamesService
{
    /// <summary>Called only after issuer/signature/session validation, never from member-supplied JSON.</summary>
    public static void SuggestFromTrustedClaims(Account account,ClaimsPrincipal user)
    {
        if(account.Realm!=AccountRealm.Members||account.RegisteredNamesConfirmedAt is not null)return;
        account.RegisteredFirstName??=RegisteredNamePolicy.Part(user.FindFirstValue("given_name"));
        account.RegisteredLastName??=RegisteredNamePolicy.Part(user.FindFirstValue("family_name"));
        if(user.FindFirstValue("swoovr_mononym")=="true"&&account.RegisteredLastName is null)account.RegisteredMononym=true;
    }
    public static RegisteredNamesView View(Account account)=>new(account.RegisteredFirstName,account.RegisteredLastName,account.RegisteredMononym,
        RegisteredNamePolicy.Complete(account),account.RegisteredNamesRevision,account.RegisteredNamesMatchId);
    public static async Task<(string? Error,RegisteredNamesView? Value)> SaveAsync(SwoovrDbContext db,Guid id,RegisteredNamesUpdate update,DateTimeOffset now,CancellationToken token)
    {
        if(!update.Confirm||update.ExpectedRevision<0||update.ExpectedRevision==long.MaxValue||RegisteredNamePolicy.Validate(update.FirstName,update.LastName,update.Mononym)is not{}names)return("invalid-registered-names",null);
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,id,token);
        await using var barrier=await Swoovr.Api.Retention.PreservationLease.SharedAsync(db,token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        var account=await db.Accounts.SingleAsync(a=>a.Id==id,token);await db.Entry(account).ReloadAsync(token);
        if(account.Status!=AccountStatus.Active||account.Realm!=AccountRealm.Members)return("account-locked",null);
        if(account.RegisteredNamesRevision!=update.ExpectedRevision)return("record-changed",null);
        var profile=await db.MemberProfiles.SingleAsync(p=>p.AccountId==id,token);await db.Entry(profile).ReloadAsync(token);
        bool changed=account.RegisteredFirstName!=names.FirstName||account.RegisteredLastName!=names.LastName||account.RegisteredMononym!=names.Mononym;
        if(!changed&&RegisteredNamePolicy.Complete(account)){await transaction.CommitAsync(token);return(null,View(account));}
        account.RegisteredFirstName=names.FirstName;account.RegisteredLastName=names.LastName;account.RegisteredMononym=names.Mononym;
        account.RegisteredNamesConfirmedAt=now;account.RegisteredNamesRevision++;account.RegisteredNamesMatchId=null;
        profile.DisplayName=RegisteredNamePolicy.InitialPublicName(profile.DisplayName,names);
        // Q125 A (D317): an ID-verified member keeps the badge while staff compare the new name with the verified identity
        // record; one open review per account, moved to the latest revision if the member changes it again meanwhile.
        if(changed&&profile.IdStatus==VerificationStatus.Verified)
        {
            var review=await db.NameChangeReviews.SingleOrDefaultAsync(r=>r.AccountId==id&&r.ResolvedAt==null,token);
            if(review is null)db.NameChangeReviews.Add(new NameChangeReview{Id=Guid.NewGuid(),AccountId=id,Revision=account.RegisteredNamesRevision,CreatedAt=now});
            else review.Revision=account.RegisteredNamesRevision;
            Audit.Record(db,now,id,"account","verification.name-change-review","account",id.ToString(),"revision:"+account.RegisteredNamesRevision);
        }
        // An in-flight result belongs to the old declarations. A fresh name check must use a new provider session.
        var open=await db.IdVerificationAttempts.Where(a=>a.AccountId==id&&a.CompletedAt==null).ToListAsync(token);
        foreach(var attempt in open){attempt.State=IdAttemptState.Expired;attempt.CompletedAt=now;attempt.UpdatedAt=now;attempt.Url=null;profile.CanRequestIdVerification=true;}
        Audit.Record(db,now,id,"account","account.registered-names-confirmed","account",id.ToString(),"revision:"+account.RegisteredNamesRevision);
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        return(null,View(account));
    }
}
