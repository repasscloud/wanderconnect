using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Accounts;

/// <summary>Durable provider reconciliation. A receipt can finish without authorizing new effects after access is revoked.</summary>
public sealed class EmailRetirementProcessor(SwoovrDbContext db,IContactIdentityAdmin provider,EmailRetirementCommands commands,TimeProvider clock,BannedIdentifierRegistry registry)
{
    public async Task ProcessAsync(Guid id,CancellationToken token)
    {
        if(!provider.ContactRetirementConfigured)return;
        var source=await db.EmailRetirementOperations.Where(o=>o.Id==id).Select(o=>(Guid?)o.AccountId).SingleOrDefaultAsync(token);
        if(source is null)return;
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,source.Value,token);
        await using var authority=await PreservationLease.ExclusiveAsync(db,token);
        var operation=await db.EmailRetirementOperations.SingleOrDefaultAsync(o=>o.Id==id,token);
        if(operation is null)return;await db.Entry(operation).ReloadAsync(token);
        if(operation.State==EmailRetirementState.Completed)return;
        var account=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==operation.AccountId,token);
        if(account is not null)await db.Entry(account).ReloadAsync(token);
        if(account is null||account.EmailRetiredAt is null||account.Issuer!=provider.ContactIssuer||!Guid.TryParse(account.Subject,out var subject))
        { await ObserveAsync(operation,IdentityContactState.Conflict,token);return; }
        // This read has no new destructive effect. Even revoked/expired approval may reconcile an already committed command.
        var state=await provider.EmailRetirementStatusAsync(subject,id,token);
        if(state==IdentityContactState.Retired){await ObserveAsync(operation,state,token);return;}
        if(state!=IdentityContactState.NotRecorded||operation.State==EmailRetirementState.NeedsReview)
        {await ObserveAsync(operation,state,token);return;}
        var now=clock.GetUtcNow();
        var grant=await db.AccessGrants.SingleOrDefaultAsync(g=>g.Id==operation.AccessGrantId&&g.AccountId==operation.RequestedBy
            &&g.Capability==StaffAccessPolicy.IdentifierRelease&&g.RevokedAt==null&&g.ExpiresAt>now,token);
        var staff=await db.Accounts.SingleOrDefaultAsync(a=>a.Id==operation.RequestedBy&&a.Realm==AccountRealm.Staff&&a.Status==AccountStatus.Active,token);
        var admin=staff is not null&&(staff.IsRootAdmin||await db.RoleGrants.AnyAsync(r=>r.AccountId==staff.Id&&r.Role==AccountRole.Admin,token));
        var people=await SafetyRetention.CaseAccountsAsync(db,operation.CaseId,token);
        if(grant is null||!admin||await StaffAccessEndpoints.ExcludedAsync(db,operation.RequestedBy,people,token)
            ||!await db.ModerationCases.AnyAsync(c=>c.Id==operation.CaseId&&c.SubjectAccountId==account.Id,token)
            ||!await db.Sanctions.AnyAsync(s=>s.AccountId==account.Id&&s.CaseId==operation.CaseId&&s.Kind==SanctionKind.Termination
                &&s.LiftedAt==null&&s.StartsAt<=now&&(s.EndsAt==null||s.EndsAt>now),token)
            ||!await db.BannedIdentifiers.AnyAsync(b=>b.Id==operation.RegistryId&&b.CaseId==operation.CaseId&&b.IdentifierType==BannedIdentifierType.Email
                &&b.Status==BanStatus.Revoked&&b.RevokedAt!=null&&b.RevokedBy!=null,token))
        {await ObserveAsync(operation,IdentityContactState.Conflict,token);return;}
        IdentityEmailBinding expected;
        try{expected=commands.Open(operation,provider.ContactIssuer);}
        catch(Exception error)when(error is System.Security.Cryptography.CryptographicException or KeyNotFoundException or ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {await ObserveAsync(operation,IdentityContactState.Unavailable,token);return;}
        if(expected.Subject!=subject){await ObserveAsync(operation,IdentityContactState.Conflict,token);return;}
        List<BannedIdentifier> matches;
        try{matches=await registry.FindAsync(BannedIdentifierType.Email,expected.Email!,now,token);}
        catch(IdentifierCheckException){await ObserveAsync(operation,IdentityContactState.Unavailable,token);return;}
        var related=new List<Guid>();
        foreach(var review in matches.Select(b=>b.CaseId).Distinct())related.AddRange(await SafetyRetention.CaseAccountsAsync(db,review,token));
        if(!matches.Any(b=>b.Id==operation.RegistryId)||matches.Any(b=>BannedIdentifierPolicy.Blocks(b,now))
            ||await StaffAccessEndpoints.ExcludedAsync(db,operation.RequestedBy,related,token))
        {await ObserveAsync(operation,IdentityContactState.Conflict,token);return;}
        state=await provider.RetireEmailBindingAsync(expected,id,token);
        await ObserveAsync(operation,state,token);
    }
    private async Task ObserveAsync(EmailRetirementOperation operation,IdentityContactState state,CancellationToken token)
    {
        var now=clock.GetUtcNow();
        if(state==IdentityContactState.Retired)
        {
            // Pending can outlast an access-token lifetime. Refuse tokens minted anywhere in that uncertainty window.
            var source=await db.Accounts.SingleAsync(a=>a.Id==operation.AccountId,token);
            var cutoff=SessionRevocationPolicy.Cutoff(now);
            if(source.SessionsRevokedAt is null||source.SessionsRevokedAt<cutoff)source.SessionsRevokedAt=cutoff;
        }
        var previous=operation.State;EmailRetirementPolicy.Observe(operation,state,now);
        if(previous!=operation.State)Audit.Record(db,clock.GetUtcNow(),null,"account","registry.email-retirement-state","retirement",operation.Id.ToString(),operation.State.ToString());
        await db.SaveChangesAsync(token);
    }
    public async Task<int> RunAsync(CancellationToken token)
    {
        var now=clock.GetUtcNow();
        var ids=await db.EmailRetirementOperations.Where(o=>o.State!=EmailRetirementState.Completed&&o.NextAttemptAt<=now)
            .OrderBy(o=>o.NextAttemptAt).ThenBy(o=>o.Id).Select(o=>o.Id).Take(100).ToListAsync(token);
        foreach(var id in ids){await ProcessAsync(id,token);db.ChangeTracker.Clear();}
        return ids.Count;
    }
}
