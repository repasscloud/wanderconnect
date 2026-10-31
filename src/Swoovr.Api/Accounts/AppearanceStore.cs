using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Retention;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Accounts;

public sealed record AppearanceStoreResult(string Outcome,AppearanceSnapshot? Snapshot);
public sealed class AppearanceStore(SwoovrDbContext db,TimeProvider clock)
{
    public async Task<AppearanceSnapshot?> LoadAsync(Guid account,CancellationToken token)=>View(await db.Set<MemberAppearance>().AsNoTracking().SingleOrDefaultAsync(a=>a.AccountId==account,token));
    private static AppearanceSnapshot? View(MemberAppearance? row)=>row is null?null:new(new(AppearanceValues.Valid(row.Scheme,row.Mode)?row.Scheme:"standard",AppearanceValues.Valid(row.Scheme,row.Mode)?row.Mode:"system"),row.Revision);
    public async Task<AppearanceStoreResult> SaveAsync(Guid account,AppearanceUpdate update,CancellationToken token)
    {
        if(update.Preference is null||!AppearanceValues.Valid(update.Preference.Scheme,update.Preference.Mode)||update.ExpectedRevision<0||update.ChangeId==Guid.Empty)return new("invalid",null);
        await using var lifecycle=await AccountLifecycleLease.AcquireAsync(db,account,token);
        await using var barrier=await PreservationLease.SharedAsync(db,token);
        await using var transaction=await db.Database.BeginTransactionAsync(token);
        if(!await db.Accounts.AsNoTracking().AnyAsync(a=>a.Id==account&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active,token))return new("unavailable",null);
        var row=await db.Set<MemberAppearance>().AsNoTracking().SingleOrDefaultAsync(a=>a.AccountId==account,token);
        if(row is not null&&row.LastChangeId==update.ChangeId)
            return row.Revision-1==update.ExpectedRevision&&row.Scheme==update.Preference.Scheme&&row.Mode==update.Preference.Mode?new("saved",View(row)):new("invalid",null);
        if(row?.Revision==long.MaxValue)return new("exhausted",View(row));
        if((row?.Revision??0)!=update.ExpectedRevision)return new("conflict",View(row));
        var now=clock.GetUtcNow();var next=update.ExpectedRevision+1;
        if(row is null)db.Add(new MemberAppearance{AccountId=account,Scheme=update.Preference.Scheme,Mode=update.Preference.Mode,Revision=next,LastChangeId=update.ChangeId,UpdatedAt=now});
        else await db.Set<MemberAppearance>().Where(a=>a.AccountId==account).ExecuteUpdateAsync(s=>s.SetProperty(a=>a.Scheme,update.Preference.Scheme).SetProperty(a=>a.Mode,update.Preference.Mode).SetProperty(a=>a.Revision,next).SetProperty(a=>a.LastChangeId,update.ChangeId).SetProperty(a=>a.UpdatedAt,now),token);
        Audit.Record(db,now,account,"account","appearance.changed","account",account.ToString());
        await db.SaveChangesAsync(token);await transaction.CommitAsync(token);
        return new("saved",new(update.Preference,next));
    }
}
