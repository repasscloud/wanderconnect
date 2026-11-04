using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Ticketing;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

internal static class TicketingOperationChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string,bool> check)
    {
        var fake = new Provider();
        var account = Guid.NewGuid();
        await using (var first = new SwoovrDbContext(options))
        {
            var service = new TicketingOperations(first, fake, fake, TimeProvider.System);
            fake.LoseCustomerResponse = true;
            try { await service.EnsureCustomerAsync(account, "fictional@example.test", default); check("lost customer response surfaced uncertain",false); }
            catch (TicketingProviderException e) when (e.Uncertain) {check("lost customer response surfaced uncertain",true);}
        }
        await using (var recovered = new SwoovrDbContext(options))
        {
            var service = new TicketingOperations(recovered, fake, fake, TimeProvider.System);
            var customer = await service.EnsureCustomerAsync(account, "changed@example.test", default);
            check("canonical login recovery avoids duplicate customer and preserves numeric id", fake.CustomerCreates == 1 && customer == 42);
            check("verified email sync does not change canonical login", fake.Customer!.Login == TicketingPolicy.MemberLogin(account) && fake.Customer.Email == "changed@example.test");
            var operation = Guid.NewGuid();
            var draft = new SupportTicketDraft("Fictional enquiry", "Fictional ordinary message", customer, 1, operation.ToString());
            fake.LoseTicketResponse = true;
            try { await service.CreateTicketAsync(account, operation, draft, default); check("lost ticket response surfaced uncertain",false); }
            catch (TicketingProviderException e) when (e.Uncertain) {check("lost ticket response surfaced uncertain",true);}
            fake.SearchVisible = false;
            try { await service.CreateTicketAsync(account, operation, draft, default); check("negative asynchronous search cannot cause write replay",false); }
            catch (TicketingProviderException e) when (e.Uncertain) {check("negative asynchronous search cannot cause write replay",fake.TicketCreates==1);}
            fake.SearchVisible = true;
            var result = await service.CreateTicketAsync(account, operation, draft, default);
            check("uncertain ticket reconciles to provider reference", result.Id==99 && fake.TicketCreates==1);
            try { await service.CreateTicketAsync(account, operation, draft with {Body="Changed request"}, default); check("idempotency key payload change denied",false); }
            catch (InvalidOperationException) {check("idempotency key payload change denied",true);}
        }
        var concurrentAccount = Guid.NewGuid();
        var concurrentProvider = new Provider(43);
        async Task<long> EnsureConcurrent()
        {
            await using var context = new SwoovrDbContext(options);
            return await new TicketingOperations(context, concurrentProvider, concurrentProvider, TimeProvider.System)
                .EnsureCustomerAsync(concurrentAccount, "concurrent@example.test", default);
        }
        var customers = await Task.WhenAll(EnsureConcurrent(), EnsureConcurrent());
        check("concurrent contexts serialize first customer creation", customers.All(id=>id==43) && concurrentProvider.CustomerCreates==1);
        var concurrentOperation = Guid.NewGuid();
        var concurrentDraft = new SupportTicketDraft("Concurrent fictional enquiry", "Ordinary fictional message",43,1,concurrentOperation.ToString());
        async Task<SupportTicket> CreateConcurrent()
        {
            await using var context = new SwoovrDbContext(options);
            return await new TicketingOperations(context, concurrentProvider, concurrentProvider, TimeProvider.System)
                .CreateTicketAsync(concurrentAccount,concurrentOperation,concurrentDraft,default);
        }
        var tickets = await Task.WhenAll(CreateConcurrent(),CreateConcurrent());
        check("concurrent identical ticket operation writes once", tickets.All(ticket=>ticket.Id==99) && concurrentProvider.TicketCreates==1);
        await using(var lifecycle=new SwoovrDbContext(options))
        {
            var now=DateTimeOffset.UtcNow;
            lifecycle.Add(new Account{Id=account,Issuer="https://fictional-lifecycle.invalid",Subject=account.ToString(),Realm=AccountRealm.Members,Email="changed@example.test",Status=AccountStatus.PendingDeletion,CreatedAt=now,LastSeenAt=now});
            await lifecycle.SaveChangesAsync();
            var service=new TicketingOperations(lifecycle,fake,fake,TimeProvider.System);
            await service.SyncLifecycleAsync(account,default);
            check("inactive account deactivates original provider customer and clears delivery address",fake.Customer is{Active:false,Email:null}&&await lifecycle.Set<TicketingIdentity>().AnyAsync(i=>i.AccountId==account&&i.DeactivatedAt!=null));
            var owner=await lifecycle.Accounts.SingleAsync(a=>a.Id==account);owner.Status=AccountStatus.Active;await lifecycle.SaveChangesAsync();
            var recovered=await service.SyncLifecycleAsync(account,default);
            check("account lifecycle recovery restores same canonical numeric mapping without creation",recovered==42&&fake.Customer is{Active:true,Email:"changed@example.test"}&&fake.CustomerCreates==1);
            lifecycle.Remove(owner);await lifecycle.SaveChangesAsync();
        }

    }

    private sealed class Provider(long customerId = 42) : ITicketingProvider
    {
        public SupportCustomer? Customer;
        public SupportTicket? Ticket;
        public int CustomerCreates, TicketCreates;
        public bool LoseCustomerResponse, LoseTicketResponse, SearchVisible;
        public Task<SupportCustomer?> FindCustomerAsync(string login,CancellationToken ct)=>Task.FromResult(Customer?.Login==login?Customer:null);
        public Task<SupportCustomer> CreateCustomerAsync(string login,string? email,CancellationToken ct)
        {
            CustomerCreates++; Customer=new(customerId,login,email,true);
            if(LoseCustomerResponse){LoseCustomerResponse=false;throw new TicketingProviderException("lost",true);}
            return Task.FromResult(Customer);
        }
        public Task<SupportCustomer> UpdateCustomerAsync(long id,string? email,bool active,CancellationToken ct)
        {Customer=Customer! with {Email=email,Active=active};return Task.FromResult(Customer);}
        public Task<SupportTicket> CreateTicketAsync(SupportTicketDraft draft,CancellationToken ct)
        {
            TicketCreates++; Ticket=new(99,"99001",draft.Title,draft.CustomerId,draft.GroupId,1,1,1,DateTimeOffset.UtcNow,draft.OperationReference);
            if(LoseTicketResponse){LoseTicketResponse=false;throw new TicketingProviderException("lost",true);}
            return Task.FromResult(Ticket);
        }
        public Task<IReadOnlyList<SupportTicket>> SearchAsync(string query,int page,int pageSize,CancellationToken ct)
            =>Task.FromResult<IReadOnlyList<SupportTicket>>(SearchVisible && Ticket is not null?[Ticket]:[]);
        public Task<SupportTicket?> GetTicketAsync(long id,CancellationToken ct)=>Task.FromResult(Ticket);
        public Task<SupportTicket> ChangeTicketAsync(long id,SupportTicketChange change,CancellationToken ct)=>throw new NotSupportedException();
        public Task<IReadOnlyList<SupportArticle>> GetArticlesAsync(long id,CancellationToken ct)=>throw new NotSupportedException();
        public Task<SupportArticle> ReplyAsync(long id,string body,bool internalNote,bool customer,CancellationToken ct)=>throw new NotSupportedException();
    }
}
