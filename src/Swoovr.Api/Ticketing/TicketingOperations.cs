using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Ticketing;

/// <summary>Minimal durable integration orchestration. Caller must resolve current account/actor authority before use.
/// Conversation bodies are sent to Zammad and are never persisted here. This class is not an HTTP authorization boundary.</summary>
public sealed class TicketingOperations(SwoovrDbContext db, ITicketingProvider identityProvider, ITicketingProvider actorProvider, TimeProvider clock)
{
    public async Task<long> EnsureCustomerAsync(Guid accountId, string? verifiedEmail, CancellationToken ct)
    {
        if (accountId == Guid.Empty) throw new ArgumentException("Missing account identity.");
        await using var lease = await AccountLifecycleLease.AcquireAsync(db, accountId, ct);
        var row = await db.Set<TicketingIdentity>().SingleOrDefaultAsync(item => item.AccountId == accountId, ct);
        var fresh = row is null;
        var now = clock.GetUtcNow();
        if (row is null)
        {
            row = new TicketingIdentity {AccountId = accountId, State = TicketingOperationState.Pending, CreatedAt = now, UpdatedAt = now};
            db.Add(row);
            await db.SaveChangesAsync(ct);
        }
        if (row.DeactivatedAt is not null) throw new InvalidOperationException("Customer mapping is inactive.");
        if (row.ProviderUserId is long mapped)
        {
            // Numeric mapping is identity; a changed/matching address never relinks a customer.
            var updated = await identityProvider.UpdateCustomerAsync(mapped, verifiedEmail, true, ct);
            if (updated.Id != mapped || updated.Login != TicketingPolicy.MemberLogin(accountId)) throw new TicketingProviderException("provider-identity-mismatch");
            return mapped;
        }
        try
        {
            var login = TicketingPolicy.MemberLogin(accountId);
            var customer = await identityProvider.FindCustomerAsync(login, ct);
            if (customer is null)
            {
                // A previous process may have created it before crashing. A negative indexed search is not absence proof.
                if (!fresh) throw new TicketingProviderException("customer-reconciliation-required", true);
                customer = await identityProvider.CreateCustomerAsync(login, verifiedEmail, ct);
            }
            if (customer.Login != login || !customer.Active) throw new TicketingProviderException("provider-identity-mismatch");
            if (customer.Email != verifiedEmail) customer = await identityProvider.UpdateCustomerAsync(customer.Id, verifiedEmail, true, ct);
            row.ProviderUserId = customer.Id;
            row.State = TicketingOperationState.Applied;
            row.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return customer.Id;
        }
        catch (TicketingProviderException error)
        {
            row.State = error.Uncertain ? TicketingOperationState.Uncertain : TicketingOperationState.Rejected;
            row.UpdatedAt = clock.GetUtcNow();
            await SaveOutcomeAsync();
            throw;
        }
    }

    public async Task<SupportTicket> CreateTicketAsync(Guid accountId, Guid operationId, SupportTicketDraft draft, CancellationToken ct)
    {
        if (accountId == Guid.Empty || operationId == Guid.Empty || draft.OperationReference != operationId.ToString("D")) throw new ArgumentException("Invalid operation identity.");
        await using var lease = await AccountLifecycleLease.AcquireAsync(db, accountId, ct);
        var identity = await db.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(row => row.AccountId == accountId, ct);
        if (identity is null || identity.State != TicketingOperationState.Applied || identity.DeactivatedAt is not null || identity.ProviderUserId != draft.CustomerId)
            throw new InvalidOperationException("Customer identity is not available.");
        var digest = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(draft)));
        var operation = await db.Set<TicketingOperation>().SingleOrDefaultAsync(row => row.Id == operationId, ct);
        if (operation is not null)
        {
            if (operation.AccountId != accountId || operation.Kind != "create-ticket" || operation.RequestDigest != digest)
                throw new InvalidOperationException("Idempotency key does not match the request.");
            if (operation.State == TicketingOperationState.Rejected) throw new InvalidOperationException("Operation was rejected.");
            if (operation.ProviderTicketId is long id)
                return await actorProvider.GetTicketAsync(id, ct) ?? throw new TicketingProviderException("provider-not-found");
            var matches = await actorProvider.SearchAsync("swoovr_operation:" + operationId.ToString("D"), 1, 2, ct);
            if (matches.Count != 1 || matches[0].CustomerId != draft.CustomerId || matches[0].OperationReference != operationId.ToString("D")) throw new TicketingProviderException("ticket-reconciliation-required", true);
            operation.ProviderTicketId = matches[0].Id;
            operation.State = TicketingOperationState.Applied;
            operation.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return matches[0];
        }
        var now = clock.GetUtcNow();
        operation = new TicketingOperation {Id = operationId, AccountId = accountId, Kind = "create-ticket", RequestDigest = digest,
            State = TicketingOperationState.Pending, CreatedAt = now, UpdatedAt = now};
        db.Add(operation);
        await db.SaveChangesAsync(ct);
        try
        {
            var ticket = await actorProvider.CreateTicketAsync(draft, ct);
            if (ticket.CustomerId != draft.CustomerId) throw new TicketingProviderException("provider-identity-mismatch", true);
            operation.ProviderTicketId = ticket.Id;
            operation.State = TicketingOperationState.Applied;
            operation.UpdatedAt = clock.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return ticket;
        }
        catch (TicketingProviderException error)
        {
            operation.State = error.Uncertain ? TicketingOperationState.Uncertain : TicketingOperationState.Rejected;
            operation.UpdatedAt = clock.GetUtcNow();
            await SaveOutcomeAsync();
            throw;
        }
    }

    private async Task SaveOutcomeAsync()
    {
        // A disconnected caller cannot erase knowledge that an outbound mutation may already have applied.
        using var recovery = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await db.SaveChangesAsync(recovery.Token);
    }
}
