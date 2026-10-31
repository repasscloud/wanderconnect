using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Policies;
public sealed class PolicyAcceptanceService(SwoovrDbContext db,ApprovedPolicyCatalogue catalogue,IConfiguration configuration,TimeProvider clock)
{
    public PolicyRequirement Requirement()
    {
        var result=catalogue.Resolve(configuration["Policies:RequiredVersion"],clock.GetUtcNow());
        if(result.State==PolicyRequirementState.ConfigurationInvalid)throw new InvalidOperationException("Selected approved policy is unavailable; drafts cannot be activated.");
        return result;
    }
    public async Task<MemberPolicyView> StateAsync(Guid account,CancellationToken token)
    {
        var requirement=Requirement();
        if(requirement.State==PolicyRequirementState.ExistingAccessUnchanged)return new(null,null,null,true,null,null);
        var document=catalogue.Find(requirement.Version!,clock.GetUtcNow())!;
        var accepted=await db.Set<PolicyAcceptance>().AsNoTracking().SingleOrDefaultAsync(a=>a.AccountId==account&&a.Version==requirement.Version,token);
        if(accepted!=null&&accepted.ContentSha256!=document.Metadata.ContentSha256)throw new InvalidOperationException("Accepted policy source integrity unavailable.");
        return new(requirement.Version,document.Metadata.ContentSha256,document.Metadata.EffectiveAt,accepted!=null,accepted?.AcceptedAt,"/policies/"+Uri.EscapeDataString(requirement.Version!));
    }
    public async Task<bool> RequiredAsync(Guid account,CancellationToken token)=>!(await StateAsync(account,token)).Accepted;
    public async Task<MemberPolicyView?> AcceptAsync(Guid account,PolicyAcceptanceRequest request,CancellationToken token)
    {
        var requirement=Requirement();if(!PolicyAcceptancePolicy.CanRecord(requirement,request.Version??"",request.Confirm))return null;
        await using var owner=await AccountLifecycleLease.AcquireAsync(db,account,token);
        await using var authority=await PreservationLease.SharedAsync(db,token);
        if(!await db.Accounts.AnyAsync(a=>a.Id==account&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,token))return null;
        var state=await StateAsync(account,token);if(state.Accepted)return state;
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        db.Set<PolicyAcceptance>().Add(new(){AccountId=account,Version=state.RequiredVersion!,ContentSha256=state.ContentSha256!,AcceptedAt=clock.GetUtcNow()});
        Audit.Record(db,clock.GetUtcNow(),account,"account","policy.accepted","policy-version",state.RequiredVersion);
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);return await StateAsync(account,token);
    }
}
