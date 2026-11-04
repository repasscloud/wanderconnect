using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Connections;

/// <summary>Existing hub membership is not authority to receive new message content after beta revocation or a sign-in lock (D323).</summary>
public static class BetaMessageAudience
{
    public static async Task<bool> AllowsAsync(IServiceProvider services,Guid recipient,CancellationToken token)
    {
        var db=services.GetRequiredService<SwoovrDbContext>();
        var policy=await Swoovr.Api.Accounts.RegistrationAuthority.ReadAsync(db,token);
        if(policy is null)return false;
        var betaOnly=policy.BetaEnabled&&!policy.PublicEnabled;
        var now=(services.GetService<TimeProvider>()??TimeProvider.System).GetUtcNow();
        return await db.Accounts.AsNoTracking().AnyAsync(a=>a.Id==recipient&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active
            &&!db.AccountLocks.Any(l=>l.AccountId==recipient&&l.RemovedAt==null&&(l.ExpiresAt==null||l.ExpiresAt>now))
            &&(!betaOnly||db.BetaEnrolments.Any(e=>e.RevokedAt==null&&(e.AccountId==recipient||e.AccountId==null&&a.Email!=null&&a.EmailRetiredAt==null&&e.Email==a.Email))),token);
    }
    public static async Task SendAsync(IServiceProvider services,Guid recipient,MessageView message,CancellationToken token)
    {
        if(!await AllowsAsync(services,recipient,token))return;
        await services.GetRequiredService<IHubContext<MessagesHub>>().Clients.Group(MessagesHub.Group(recipient)).SendAsync("message",message,token);
    }
}
