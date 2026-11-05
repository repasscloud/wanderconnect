using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Ticketing;

namespace Swoovr.Api.Ticketing;

public sealed class TicketingAccessException(int status,string code):Exception(code)
{
    public int Status {get;}=status;
    public string Code {get;}=code;
}

/// <summary>No runtime provisioning token or arbitrary client-selected From. Each staff member acts through their own issued token.</summary>
public sealed class TicketingActorFactory(IConfiguration configuration,TimeProvider clock):IDisposable
{
    private readonly List<HttpClient> clients=[];
    public ZammadTicketingProvider Identity()=>Provider(configuration["Ticketing:Zammad:IdentityToken"]);
    private ZammadTicketingProvider Provider(string? token,long? from=null)
    {
        if(!Uri.TryCreate(configuration["Ticketing:Zammad:BaseUrl"],UriKind.Absolute,out var url)||string.IsNullOrWhiteSpace(token))throw new TicketingAccessException(503,"ticketing-unavailable");
        var http=new HttpClient(new SocketsHttpHandler{AllowAutoRedirect=false,ConnectTimeout=TimeSpan.FromSeconds(5)});
        clients.Add(http);return new(http,url,token,from);
    }
    public async Task<ITicketingWorkspaceProvider> MemberAsync(AccountResolver accounts,CurrentAccount current,bool create,CancellationToken ct)
    {
        var id=current.Account!.Id;
        var identity=Identity();
        var mapping=await accounts.Db!.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(m=>m.AccountId==id,ct);
        if(mapping?.ProviderUserId is not null)
        {
            await new TicketingOperations(accounts.Db!,identity,identity,clock).SyncLifecycleAsync(id,ct);
            mapping=await accounts.Db!.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(m=>m.AccountId==id,ct);
        }
        else if(create)
        {
            await new TicketingOperations(accounts.Db!,identity,identity,clock).EnsureCustomerAsync(id,current.VerifiedEmail,ct);
            mapping=await accounts.Db!.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(m=>m.AccountId==id,ct);
        }
        if(mapping is not{State:TicketingOperationState.Applied,DeactivatedAt:null,ProviderUserId:long providerId})throw new TicketingAccessException(404,"ticket-customer-unavailable");
        var user=await identity.UserAsync(providerId,ct);
        ValidateCustomer(user,id);
        var actor=Provider(configuration["Ticketing:Zammad:IdentityToken"],providerId);
        var effective=await actor.ActorAsync(ct);ValidateCustomer(effective,id);
        if(effective.Id!=providerId)throw new TicketingAccessException(403,"ticket-actor-mismatch");
        return actor;
    }
    public static void ValidateCustomer(SupportActor actor,Guid id,bool requireActive=true)
    {
        if(requireActive&&!actor.Active||actor.Login!=TicketingPolicy.MemberLogin(id)||actor.Roles.Count!=1||actor.Roles[0]!="Customer"||actor.Groups.Count!=0)throw new TicketingAccessException(403,"ticket-customer-role-invalid");
    }
    /// <summary>A mapped customer's own view, for background re-fetching. The numeric ID comes only from Swoovr's mapping.</summary>
    public async Task<ITicketingWorkspaceProvider> CustomerAsync(long providerId,Guid owner,bool guest,CancellationToken ct)
    {
        var user=await Identity().UserAsync(providerId,ct);
        if(guest)ValidateGuest(user,owner);else ValidateCustomer(user,owner);
        var actor=Provider(configuration["Ticketing:Zammad:IdentityToken"],providerId);
        var effective=await actor.ActorAsync(ct);
        if(guest)ValidateGuest(effective,owner);else ValidateCustomer(effective,owner);
        if(effective.Id!=providerId)throw new TicketingAccessException(403,"ticket-actor-mismatch");
        return actor;
    }
    public static void ValidateGuest(SupportActor actor,Guid id)
    {
        if(!actor.Active||actor.Login!=TicketingGuestPolicy.Login(id)||actor.Roles.Count!=1||actor.Roles[0]!="Customer"||actor.Groups.Count!=0)throw new TicketingAccessException(403,"ticket-customer-role-invalid");
    }
    /// <summary>A verified guest acts only as their own mapped customer, through the identity token and a server-chosen From.</summary>
    public async Task<ITicketingWorkspaceProvider> GuestAsync(Guid guestId,long providerId,CancellationToken ct)
    {
        ValidateGuest(await Identity().UserAsync(providerId,ct),guestId);
        var actor=Provider(configuration["Ticketing:Zammad:IdentityToken"],providerId);
        var effective=await actor.ActorAsync(ct);ValidateGuest(effective,guestId);
        if(effective.Id!=providerId)throw new TicketingAccessException(403,"ticket-actor-mismatch");
        return actor;
    }
    /// <summary>The staff-directory credential (admin.user and admin.session only), used solely by the staff synchronisation.</summary>
    public ITicketingStaffDirectory StaffDirectory()=>Provider(configuration["Ticketing:Zammad:StaffDirectoryToken"]);
    /// <summary>
    /// The signed-in staff member's own Zammad API identity. Its token was issued to that identity by the staff
    /// synchronisation and is stored encrypted; a staff member not yet provisioned is synchronised first, once.
    /// </summary>
    public async Task<ITicketingWorkspaceProvider> StaffAsync(AccountResolver accounts,CurrentAccount current,CancellationToken ct)
    {
        var db=accounts.Db!;var keys=accounts.Services.GetRequiredService<Swoovr.Api.Accounts.ContactHistoryKeys>();
        var credential=await TicketingStaffSync.TokenAsync(db,keys,current.Account!.Id,ct);
        if(credential is null&&!string.IsNullOrWhiteSpace(configuration["Ticketing:Zammad:StaffDirectoryToken"]))
        {
            await accounts.Services.GetRequiredService<TicketingStaffSync>().RunAsync(false,ct,current.Account.Id);
            credential=await TicketingStaffSync.TokenAsync(db,keys,current.Account.Id,ct);
        }
        if(credential is not var(providerId,token))throw new TicketingAccessException(503,"ticketing-unavailable");
        var actor=Provider(token);
        var effective=await actor.ActorAsync(ct);
        if(effective.Id!=providerId||!effective.Active||effective.Login!=TicketingPolicy.StaffLogin(current.Account.Id)||effective.Roles.Count!=1||effective.Roles[0]!="Agent")throw new TicketingAccessException(403,"ticket-staff-actor-invalid");
        return actor;
    }
    public void Dispose(){foreach(var client in clients)client.Dispose();}
}
