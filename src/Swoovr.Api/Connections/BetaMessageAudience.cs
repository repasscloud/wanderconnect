using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api.Connections;

/// <summary>Existing hub membership is not authority to receive new message content after beta revocation.</summary>
public static class BetaMessageAudience
{
    public static async Task<bool> AllowsAsync(IServiceProvider services,Guid recipient,CancellationToken token)
    {
        if(!services.GetRequiredService<IConfiguration>().GetValue("Beta:Enabled",true))return true;
        var db=services.GetRequiredService<SwoovrDbContext>();
        return await db.Accounts.AsNoTracking().AnyAsync(a=>a.Id==recipient&&a.Realm==AccountRealm.Members&&a.Status==AccountStatus.Active
            &&db.BetaEnrolments.Any(e=>e.RevokedAt==null&&(e.AccountId==recipient||e.AccountId==null&&a.Email!=null&&a.EmailRetiredAt==null&&e.Email==a.Email)),token);
    }
    public static async Task SendAsync(IServiceProvider services,Guid recipient,MessageView message,CancellationToken token)
    {
        if(!await AllowsAsync(services,recipient,token))return;
        await services.GetRequiredService<IHubContext<MessagesHub>>().Clients.Group(MessagesHub.Group(recipient)).SendAsync("message",message,token);
    }
}
