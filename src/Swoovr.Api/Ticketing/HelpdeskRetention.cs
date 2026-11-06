using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Evidence;
using Swoovr.Api.Retention;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;
using Swoovr.Infrastructure.Ticketing;

namespace Swoovr.Api.Ticketing;

public sealed record HelpdeskRetentionRun(int Retired, int Observed, int DeletionsRequested, int Disposed, int Held, int Failed, int CustomersDeleted,
    int LogsPurged, int LedgerPruned, bool TicketsSkipped, bool HoldStatusUnavailable = false);

/// <summary>
/// D337 (#224, Q136 136.1 A): helpdesk retention as one step of the hourly retention run.
/// 1. Retires the Zammad customer of every finally deleted member (requested in the deletion transaction).
/// 2. Observes every ticket's group, state and last close time into the ledger (reopening recalculates; 136.2 2a).
/// 3. Deletes due tickets through Zammad data privacy tasks, after rechecking holds, linked cases and the live ticket.
/// 4. Deletes retired or unmapped customers once none of their tickets remain.
/// 5. Purges Swoovr's own helpdesk integration records after 90 days, and disposed ledger rows after 60.
/// Nothing here stores or logs conversation content.
/// </summary>
public sealed class HelpdeskRetention(SwoovrDbContext db, TicketingActorFactory actors, EvidencePreservation evidence, ILogger<HelpdeskRetention> log)
{
    private const int Batch = 50;

    public async Task<HelpdeskRetentionRun> RunAsync(DateTimeOffset now, CancellationToken ct, bool reconcile = false)
    {
        var logs = await PurgeLogsAsync(now, ct);
        if (!actors.Configured) return new(0, 0, 0, 0, 0, 0, 0, logs, 0, true);
        var retired = await RetireAsync(now, ct, reconcile);
        if (actors.Retention() is not { } retention)
        {
            log.LogWarning("Helpdesk ticket retention is not configured (Ticketing:Zammad:RetentionToken); tickets are kept.");
            return new(retired, 0, 0, 0, 0, 0, 0, logs, 0, true);
        }
        var observed = await ObserveAsync(retention, now, ct);
        var (disposedByTasks, failedTasks) = await PollTicketTasksAsync(retention, now, ct);
        var restored = reconcile ? await ReconcileTicketsAsync(retention, now, ct) : 0;
        var (requested, held, failed, unavailable) = await DisposeDueAsync(retention, now, ct);
        requested += restored;
        var customers = unavailable ? 0 : await DeleteCustomersAsync(retention, now, ct);
        var pruned = await db.Set<HelpdeskTicketRetention>().Where(r => r.State == HelpdeskRetentionState.Disposed && r.DisposedAt < now - HelpdeskRetentionPolicy.DisposedLedgerRetention)
            .ExecuteDeleteAsync(ct);
        return new(retired, observed, requested, disposedByTasks, held, failed + failedTasks, customers, logs, pruned, false, unavailable);
    }

    /// <summary>D333: Swoovr's helpdesk integration records (event inbox, operation markers) are kept 90 days.</summary>
    private async Task<int> PurgeLogsAsync(DateTimeOffset now, CancellationToken ct)
    {
        var cutoff = now - HelpdeskRetentionPolicy.IntegrationLogRetention;
        var events = await db.Set<TicketingWebhookWork>().Where(w => w.CompletedAt != null && w.CompletedAt < cutoff || w.CompletedAt == null && w.ReceivedAt < cutoff).ExecuteDeleteAsync(ct);
        var operations = await db.Set<TicketingOperation>().Where(o => o.State != TicketingOperationState.Pending && o.State != TicketingOperationState.Uncertain && o.UpdatedAt < cutoff).ExecuteDeleteAsync(ct);
        return events + operations;
    }

    /// <summary>D333 retirement: inactive, `retired-…` login (136.3 3a), "Retired Swoovr Member", personal fields cleared. Idempotent.</summary>
    private async Task<int> RetireAsync(DateTimeOffset now, CancellationToken ct, bool reconcile)
    {
        var identity = actors.Identity();
        var query = db.Set<TicketingIdentity>().Where(i => i.RetirementRequestedAt != null);
        query = reconcile ? query.Where(i => i.CustomerDeletedAt == null) : query.Where(i => i.RetiredAt == null && (i.NextRetirementAt == null || i.NextRetirementAt <= now));
        var due = await query.OrderBy(i => i.RetirementRequestedAt).Take(reconcile ? 1000 : Batch).ToListAsync(ct);
        var retired = 0;
        foreach (var row in due)
        {
            if (row.ProviderUserId is not long user) { row.RetiredAt ??= now; await db.SaveChangesAsync(ct); continue; }
            row.RetiredLogin ??= HelpdeskRetentionPolicy.RetiredLogin(Guid.NewGuid());
            await db.SaveChangesAsync(ct); // the login is fixed before Zammad is asked, so a retry never invents another
            try
            {
                var actor = await identity.UserAsync(user, ct);
                if (actor.Roles.Count != 1 || actor.Roles[0] != "Customer" || actor.Groups.Count != 0
                    || actor.Login != TicketingPolicy.MemberLogin(row.AccountId) && actor.Login != row.RetiredLogin)
                    throw new TicketingProviderException("provider-identity-mismatch");
                if (reconcile && row.RetiredAt is not null && actor.Login == row.RetiredLogin && !actor.Active) continue;
                var result = await identity.RetireCustomerAsync(user, row.RetiredLogin, ct);
                if (result.Id != user || result.Login != row.RetiredLogin || result.Active || !string.IsNullOrEmpty(result.Email))
                    throw new TicketingProviderException("provider-identity-mismatch", true);
                (row.RetiredAt, row.DeactivatedAt, row.RetirementFailure, row.NextRetirementAt) = (now, row.DeactivatedAt ?? now, null, null);
                Audit.Record(db, now, null, "system", reconcile ? "helpdesk.customer.re-retired" : "helpdesk.customer.retired", "account", row.AccountId.ToString());
                retired++;
            }
            catch (TicketingProviderException e) when (e.Code == "provider-not-found")
            {
                (row.RetiredAt, row.CustomerDeletedAt, row.RetirementFailure) = (row.RetiredAt ?? now, now, null); // already gone from Zammad
                Audit.Record(db, now, null, "system", "helpdesk.customer.absent", "account", row.AccountId.ToString());
            }
            catch (Exception e) when (e is TicketingProviderException or TicketingAccessException or HttpRequestException)
            {
                row.RetirementAttempts++;
                row.RetirementFailure = e switch { TicketingProviderException p => p.Code, TicketingAccessException a => a.Code, _ => "provider-unreachable" };
                row.NextRetirementAt = now + HelpdeskRetentionPolicy.Backoff(row.RetirementAttempts);
                log.LogWarning("Helpdesk customer retirement retried later ({Code}, attempt {Attempt}).", row.RetirementFailure, row.RetirementAttempts);
            }
            await db.SaveChangesAsync(ct);
        }
        return retired;
    }

    /// <summary>Every ticket the retention identity can read, into the ledger. Closing sets the period; reopening clears it.</summary>
    private async Task<int> ObserveAsync(ITicketingRetentionProvider retention, DateTimeOffset now, CancellationToken ct)
    {
        var groups = await retention.GroupNamesAsync(ct);
        var closed = await retention.ClosedStatesAsync(ct);
        var observed = 0;
        for (var page = 1; page <= 10000; page++)
        {
            var tickets = await retention.TicketsAsync(page, 100, ct);
            var ids = tickets.Select(t => t.Id).ToArray();
            var rows = await db.Set<HelpdeskTicketRetention>().Where(r => ids.Contains(r.ProviderTicketId)).ToDictionaryAsync(r => r.ProviderTicketId, ct);
            foreach (var ticket in tickets)
            {
                if (!rows.TryGetValue(ticket.Id, out var row))
                {
                    row = new HelpdeskTicketRetention { ProviderTicketId = ticket.Id, FirstSeenAt = now };
                    db.Add(row);
                }
                HelpdeskRetentionPolicy.Observe(row, ticket.CustomerId, ticket.GroupId, groups.GetValueOrDefault(ticket.GroupId), closed.Contains(ticket.StateId), ticket.LastCloseAt, now);
                observed++;
            }
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            if (tickets.Count < 100) break;
        }
        return observed;
    }

    private async Task<(int Disposed, int Failed)> PollTicketTasksAsync(ITicketingRetentionProvider retention, DateTimeOffset now, CancellationToken ct)
    {
        var (disposed, failed) = (0, 0);
        foreach (var row in await db.Set<HelpdeskTicketRetention>().Where(r => r.State == HelpdeskRetentionState.Disposing).Take(Batch * 4).ToListAsync(ct))
        {
            var task = row.DeletionTaskId is long id ? await retention.TaskAsync(id, ct) : null;
            if (task?.State == "completed" || task is null && await retention.TicketAsync(row.ProviderTicketId, ct) is null)
            {
                (row.State, row.DisposedAt, row.FailureCode) = (HelpdeskRetentionState.Disposed, now, null);
                Audit.Record(db, now, null, "system", "helpdesk.ticket.deleted", "helpdesk-ticket", row.ProviderTicketId.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpdeskRetentionPolicy.Effective(row).ToString());
                disposed++;
            }
            else if (task?.State == "failed" || task is null)
            {
                row.Attempts++;
                (row.State, row.DeletionTaskId, row.FailureCode) = (HelpdeskRetentionState.Failed, null, "deletion-task-failed");
                row.NextAttemptAt = row.Attempts >= HelpdeskRetentionPolicy.MaximumAttempts ? DateTimeOffset.MaxValue.AddYears(-1) : now + HelpdeskRetentionPolicy.Backoff(row.Attempts);
                failed++;
            }
        }
        await db.SaveChangesAsync(ct);
        return (disposed, failed);
    }

    /// <summary>After a Zammad restore (D333): tickets Swoovr already disposed are deleted again before staff use the helpdesk.</summary>
    private async Task<int> ReconcileTicketsAsync(ITicketingRetentionProvider retention, DateTimeOffset now, CancellationToken ct)
    {
        var requested = 0;
        foreach (var row in await db.Set<HelpdeskTicketRetention>().Where(r => r.State == HelpdeskRetentionState.Disposed).ToListAsync(ct))
        {
            if (await retention.TicketAsync(row.ProviderTicketId, ct) is null) continue;
            var task = await retention.DeleteTicketAsync(row.ProviderTicketId, ct);
            (row.State, row.DeletionTaskId, row.DisposedAt) = (HelpdeskRetentionState.Disposing, task.Id, null);
            Audit.Record(db, now, null, "system", "helpdesk.ticket.restored-deletion", "helpdesk-ticket", row.ProviderTicketId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await db.SaveChangesAsync(ct);
            requested++;
        }
        return requested;
    }

    private async Task<(int Requested, int Held, int Failed, bool Unavailable)> DisposeDueAsync(ITicketingRetentionProvider retention, DateTimeOffset now, CancellationToken ct)
    {
        var (requested, held, failed) = (0, 0, 0);
        var candidates = await db.Set<HelpdeskTicketRetention>().AsNoTracking()
            .Where(r => (r.State == HelpdeskRetentionState.Retained || r.State == HelpdeskRetentionState.Failed) && r.RetainUntil <= now && (r.NextAttemptAt == null || r.NextAttemptAt <= now))
            .OrderBy(r => r.RetainUntil).Select(r => r.ProviderTicketId).Take(Batch).ToListAsync(ct);
        if (candidates.Count == 0) return (0, 0, 0, false);
        var groups = await retention.GroupNamesAsync(ct);
        var closed = await retention.ClosedStatesAsync(ct);
        foreach (var ticketId in candidates)
        {
            // Holds are rechecked immediately before each deletion, under the shared preservation barrier (D333).
            await using var barrier = await PreservationLease.SharedAsync(db, ct);
            var row = await db.Set<HelpdeskTicketRetention>().SingleAsync(r => r.ProviderTicketId == ticketId, ct);
            var live = await retention.TicketAsync(ticketId, ct);
            if (live is null) { (row.State, row.DisposedAt) = (HelpdeskRetentionState.Disposed, now); await db.SaveChangesAsync(ct); continue; }
            HelpdeskRetentionPolicy.Observe(row, live.CustomerId, live.GroupId, groups.GetValueOrDefault(live.GroupId), closed.Contains(live.StateId), live.LastCloseAt, now);
            if (!HelpdeskRetentionPolicy.Due(row, now)) { await db.SaveChangesAsync(ct); continue; }
            var reason = await PreservedAsync(row, ct);
            if (reason == "unavailable") { await db.SaveChangesAsync(ct); return (requested, held, failed, true); }
            if (reason is not null)
            {
                // Kept while the hold, case or appeal lasts; reassessed daily, never deleted indiscriminately when released.
                (row.FailureCode, row.NextAttemptAt) = (reason, now + TimeSpan.FromDays(1));
                held++;
                await db.SaveChangesAsync(ct);
                continue;
            }
            try
            {
                var task = await retention.DeleteTicketAsync(ticketId, ct);
                (row.State, row.DeletionTaskId, row.FailureCode) = (HelpdeskRetentionState.Disposing, task.Id, null);
                Audit.Record(db, now, null, "system", "helpdesk.ticket.deletion-requested", "helpdesk-ticket", ticketId.ToString(System.Globalization.CultureInfo.InvariantCulture), HelpdeskRetentionPolicy.Effective(row).ToString());
                requested++;
            }
            catch (TicketingProviderException e)
            {
                row.Attempts++;
                (row.State, row.FailureCode) = (HelpdeskRetentionState.Failed, e.Code);
                row.NextAttemptAt = row.Attempts >= HelpdeskRetentionPolicy.MaximumAttempts ? DateTimeOffset.MaxValue.AddYears(-1) : now + HelpdeskRetentionPolicy.Backoff(row.Attempts);
                failed++;
                log.LogWarning("Helpdesk ticket deletion retried later ({Code}, attempt {Attempt}).", e.Code, row.Attempts);
            }
            await db.SaveChangesAsync(ct);
        }
        return (requested, held, failed, false);
    }

    /// <summary>Why a due ticket must stay: a held member, a linked case under hold or still open, or an undecided linked appeal.</summary>
    private async Task<string?> PreservedAsync(HelpdeskTicketRetention row, CancellationToken ct)
    {
        var heldAccounts = await evidence.TryHeldAccountsAsync(ct);
        var live = await evidence.TryLiveAsync(ct);
        if (heldAccounts is null || live is null) return "unavailable";
        var member = await db.Set<TicketingIdentity>().AsNoTracking().Where(i => i.ProviderUserId == row.ProviderCustomerId).Select(i => (Guid?)i.AccountId).SingleOrDefaultAsync(ct);
        if (member is Guid account && heldAccounts.Contains(account)) return "held-account";
        var links = await db.Set<TicketingCaseLink>().AsNoTracking().Where(l => l.ProviderTicketId == row.ProviderTicketId).ToListAsync(ct);
        foreach (var link in links)
        {
            if (live.Value.Holds.Any(h => h.Status != LegalHoldStatus.Released && (h.CaseId == link.CaseId || h.RetentionCaseIds != null && h.RetentionCaseIds.Contains(link.CaseId)))) return "held-case";
            if (await db.ModerationCases.AsNoTracking().AnyAsync(c => c.Id == link.CaseId && c.Status != CaseStatus.Closed, ct)) return "open-case";
            if (link.AppealId is Guid appeal && await db.Appeals.AsNoTracking().AnyAsync(a => a.Id == appeal && a.Status == AppealStatus.Pending, ct)) return "open-appeal";
        }
        return null;
    }

    /// <summary>
    /// A customer goes once none of its tickets remain: a retired member always; any other Zammad customer only if it is a
    /// plain Customer that Swoovr doesn't map to an active member, guest or staff identity (email-only senders).
    /// </summary>
    private async Task<int> DeleteCustomersAsync(ITicketingRetentionProvider retention, DateTimeOffset now, CancellationToken ct)
    {
        var deleted = 0;
        var identity = actors.Identity();
        foreach (var row in await db.Set<TicketingIdentity>().Where(i => i.CustomerDeletionTaskId != null && i.CustomerDeletedAt == null).ToListAsync(ct))
        {
            var task = await retention.TaskAsync(row.CustomerDeletionTaskId!.Value, ct);
            if (task?.State == "completed") { row.CustomerDeletedAt = now; Audit.Record(db, now, null, "system", "helpdesk.customer.deleted", "account", row.AccountId.ToString()); deleted++; }
            else if (task?.State == "failed" || task is null) row.CustomerDeletionTaskId = null;
        }
        await db.SaveChangesAsync(ct);
        var retired = await db.Set<TicketingIdentity>().Where(i => i.RetiredAt != null && i.CustomerDeletedAt == null && i.CustomerDeletionTaskId == null && i.ProviderUserId != null)
            .Take(Batch).ToListAsync(ct);
        foreach (var row in retired)
        {
            var user = row.ProviderUserId!.Value;
            if (await db.Set<HelpdeskTicketRetention>().AnyAsync(r => r.ProviderCustomerId == user && r.State != HelpdeskRetentionState.Disposed, ct)) continue;
            if (await retention.CustomerHasTicketsAsync(user, ct)) continue;
            var actor = await identity.UserAsync(user, ct);
            if (actor.Login != row.RetiredLogin || actor.Active || actor.Roles.Count != 1 || actor.Roles[0] != "Customer") continue;
            row.CustomerDeletionTaskId = (await retention.DeleteCustomerAsync(user, ct)).Id;
            await db.SaveChangesAsync(ct);
        }
        // Email-only senders: customers of disposed tickets that nothing in Swoovr maps. Each is reviewed once.
        var orphans = await db.Set<HelpdeskTicketRetention>().AsNoTracking().Where(r => r.State == HelpdeskRetentionState.Disposed && r.CustomerReviewedAt == null)
            .Select(r => r.ProviderCustomerId).Distinct()
            .Where(c => !db.Set<HelpdeskTicketRetention>().Any(r => r.ProviderCustomerId == c && r.State != HelpdeskRetentionState.Disposed))
            .Take(Batch).ToListAsync(ct);
        foreach (var customer in orphans)
        {
            var mapped = await db.Set<TicketingIdentity>().AnyAsync(i => i.ProviderUserId == customer, ct) || await db.Set<TicketingGuest>().AnyAsync(g => g.ProviderUserId == customer, ct)
                || await db.Set<TicketingStaff>().AnyAsync(s => s.ProviderUserId == customer || s.WebProviderUserId == customer, ct);
            var remove = false;
            if (!mapped)
            {
                try
                {
                    var actor = await identity.UserAsync(customer, ct);
                    remove = actor.Roles.Count == 1 && actor.Roles[0] == "Customer" && actor.Groups.Count == 0 && !actor.Login.StartsWith("swoovr-", StringComparison.Ordinal)
                        && !await retention.CustomerHasTicketsAsync(customer, ct);
                }
                catch (TicketingProviderException e) when (e.Code == "provider-not-found") { }
            }
            if (remove)
            {
                await retention.DeleteCustomerAsync(customer, ct);
                Audit.Record(db, now, null, "system", "helpdesk.customer.deleted", "helpdesk-customer", customer.ToString(System.Globalization.CultureInfo.InvariantCulture));
                deleted++;
            }
            await db.Set<HelpdeskTicketRetention>().Where(r => r.ProviderCustomerId == customer).ExecuteUpdateAsync(u => u.SetProperty(r => r.CustomerReviewedAt, now), ct);
            await db.SaveChangesAsync(ct);
        }
        return deleted;
    }
}
