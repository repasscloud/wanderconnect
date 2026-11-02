using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

internal static class AdviceAcknowledgementPersistenceChecks
{
    public static async Task Run(SwoovrDbContext db,Action<string,bool> check)
    {
        var id=Guid.NewGuid();var other=Guid.NewGuid();var now=DateTimeOffset.UtcNow;
        db.Accounts.AddRange(new Account{Id=id,Issuer="fictional-advice",Subject=id.ToString(),Realm=AccountRealm.Members,Email="advice@example.test",Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now},new Account{Id=other,Issuer="fictional-advice",Subject=other.ToString(),Realm=AccountRealm.Members,Email="advice-other@example.test",Status=AccountStatus.Active,CreatedAt=now,LastSeenAt=now});await db.SaveChangesAsync();
        check("advice initially unacknowledged",await AdviceAcknowledgements.ReadAsync(db,id,default) is null);
        check("unknown advice revision refused",!await AdviceAcknowledgements.SaveAsync(db,id,"unknown",true,now,default));
        check("deliberate advice acknowledgement required",!await AdviceAcknowledgements.SaveAsync(db,id,Swoovr.Contracts.AdviceAcknowledgementView.CurrentRevision,false,now,default));
        check("current advice revision saved",await AdviceAcknowledgements.SaveAsync(db,id,Swoovr.Contracts.AdviceAcknowledgementView.CurrentRevision,true,now,default));
        check("other account does not inherit dismissal",await AdviceAcknowledgements.ReadAsync(db,other,default) is null);
        var first=await AdviceAcknowledgements.ReadAsync(db,id,default);
        await AdviceAcknowledgements.SaveAsync(db,id,Swoovr.Contracts.AdviceAcknowledgementView.CurrentRevision,true,now.AddDays(1),default);
        check("exact acknowledgement retry retains original server time",(await AdviceAcknowledgements.ReadAsync(db,id,default))?.AcknowledgedAt==first?.AcknowledgedAt);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM safety_advice_acknowledgements WHERE account_id={id}");
        check("ordinary deletion can erase acknowledgement",await AdviceAcknowledgements.ReadAsync(db,id,default) is null);
        await db.Accounts.Where(a=>a.Id==id||a.Id==other).ExecuteDeleteAsync();
    }
}
