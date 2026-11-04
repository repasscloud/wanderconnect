using Microsoft.EntityFrameworkCore;
using Npgsql;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

internal static class TicketingPersistenceChecks
{
    public static async Task Run(DbContextOptions<SwoovrDbContext> options, Action<string, bool> check)
    {
        await using var db = new SwoovrDbContext(options);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var account = Guid.NewGuid();
        db.Add(new TicketingIdentity {AccountId = account, ProviderUserId = 123456789, State = TicketingOperationState.Applied, CreatedAt = now, UpdatedAt = now});
        var delivery = Guid.NewGuid().ToString();
        db.Add(new TicketingWebhookWork {Id = Guid.NewGuid(), DeliveryId = delivery, BodyDigest = new string('a',64), ProviderTicketId = 1, ReceivedAt = now, NextAttemptAt = now});
        await db.SaveChangesAsync();
        await using var second = new SwoovrDbContext(options);
        check("ticketing identity and ids-only inbox survive context loss", await second.Set<TicketingIdentity>().AnyAsync(row => row.AccountId == account)
            && await second.Set<TicketingWebhookWork>().AnyAsync(row => row.DeliveryId == delivery));
        second.Add(new TicketingWebhookWork {Id = Guid.NewGuid(), DeliveryId = delivery, BodyDigest = new string('a',64), ProviderTicketId = 1, ReceivedAt = now, NextAttemptAt = now});
        try { await second.SaveChangesAsync(); check("duplicate webhook delivery is uniquely constrained", false); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException {SqlState: "23505"}) {check("duplicate webhook delivery is uniquely constrained", true);}
        second.ChangeTracker.Clear();
        second.Add(new TicketingWebhookWork {Id = Guid.NewGuid(), DeliveryId = Guid.NewGuid().ToString(), BodyDigest = new string('a',64), ProviderTicketId = 1, ReceivedAt = now, NextAttemptAt = now});
        try { await second.SaveChangesAsync(); check("signed-body replay with changed delivery is constrained", false); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException {SqlState: "23505"}) {check("signed-body replay with changed delivery is constrained", true);}
        second.ChangeTracker.Clear();
        var caseId = Guid.NewGuid();
        db.Add(new TicketingCaseLink {Id = Guid.NewGuid(), CaseId = caseId, ProviderTicketId = 1, LinkedBy = account, LinkedAt = now});
        await db.SaveChangesAsync();
        second.Add(new TicketingCaseLink {Id = Guid.NewGuid(), CaseId = caseId, ProviderTicketId = 1, LinkedBy = account, LinkedAt = now});
        try { await second.SaveChangesAsync(); check("ordinary case links uniquely constrained despite null appeal", false); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException {SqlState: "23505"}) {check("ordinary case links uniquely constrained despite null appeal", true);}
        await db.Set<TicketingCaseLink>().Where(row => row.CaseId == caseId).ExecuteDeleteAsync();
        await db.Set<TicketingWebhookWork>().Where(row => row.DeliveryId == delivery).ExecuteDeleteAsync();
        await db.Set<TicketingIdentity>().Where(row => row.AccountId == account).ExecuteDeleteAsync();
    }
}
