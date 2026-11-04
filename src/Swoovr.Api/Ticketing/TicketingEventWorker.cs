using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Api.Notifications;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Ticketing;

/// <summary>
/// Turns accepted helpdesk events (ids only) into Swoovr notices. The webhook body is never content or permission: each event
/// is re-fetched from Zammad as the ticket's own mapped customer, so only what that customer may see can trigger anything,
/// and private notes (invisible to customers) never do. One public staff reply gives at most one notice (dedupe on the
/// article). Swoovr writes nothing back to the helpdesk here, so there are no loops. A periodic sweep catches missed events.
/// </summary>
public sealed class TicketingEventProcessor(SwoovrDbContext db, TicketingActorFactory actors, Notifier notifier, IEmailSender email,
    ContactHistoryKeys keys, IConfiguration configuration, TimeProvider clock, ILogger<TicketingEventProcessor> log)
{
    public const int MaximumAttempts = 10;

    /// <summary>Processes up to <paramref name="limit"/> due events. Returns how many were handled.</summary>
    public async Task<int> ProcessDueAsync(int limit, CancellationToken ct)
    {
        var handled = 0;
        for (var i = 0; i < limit; i++)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var now = clock.GetUtcNow();
            // Each replica claims different rows; a crash releases the lock and the row is retried.
            var work = await db.Set<TicketingWebhookWork>().FromSqlInterpolated($@"SELECT * FROM ticketing_webhook_work
                WHERE completed_at IS NULL AND next_attempt_at <= {now} ORDER BY received_at LIMIT 1 FOR UPDATE SKIP LOCKED").SingleOrDefaultAsync(ct);
            if (work is null) { await transaction.CommitAsync(ct); break; }
            try
            {
                await HandleAsync(work.ProviderTicketId, work.ProviderArticleId, work.ProviderCustomerId, ct);
                (work.CompletedAt, work.FailureCode) = (clock.GetUtcNow(), null);
            }
            catch (Exception error) when (error is TicketingProviderException or TicketingAccessException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                work.Attempts++;
                var code = error switch { TicketingProviderException p => p.Code, TicketingAccessException a => a.Code, _ => "provider-unreachable" };
                // Bounded exponential backoff; after the last attempt the event is a dead letter, visible in Admin diagnostics.
                (work.FailureCode, work.NextAttemptAt) = work.Attempts >= MaximumAttempts
                    ? ("dead-letter:" + code, DateTimeOffset.MaxValue.AddYears(-1))
                    : (code, now + TimeSpan.FromSeconds(Math.Min(3600, 15 * Math.Pow(2, work.Attempts))));
                log.LogWarning("Helpdesk event {Event} not processed (attempt {Attempt}): {Code}.", work.Id, work.Attempts, work.FailureCode);
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            handled++;
        }
        return handled;
    }

    /// <summary>
    /// Missed-event reconciliation: recently updated tickets of each mapped customer are re-read and any public staff reply
    /// in the window gets its (deduplicated) notice. Bounded by the window and the number of mapped customers.
    /// </summary>
    public async Task<int> ReconcileAsync(TimeSpan window, CancellationToken ct)
    {
        var members = await db.Set<TicketingIdentity>().AsNoTracking().Where(m => m.State == TicketingOperationState.Applied && m.DeactivatedAt == null && m.ProviderUserId != null)
            .Select(m => m.ProviderUserId!.Value).ToListAsync(ct);
        var guests = await db.Set<TicketingGuest>().AsNoTracking().Where(g => g.State == TicketingOperationState.Applied && g.DeactivatedAt == null && g.ProviderUserId != null)
            .Select(g => g.ProviderUserId!.Value).ToListAsync(ct);
        var minutes = Math.Max(1, (int)window.TotalMinutes);
        var notices = 0;
        foreach (var customer in members.Concat(guests))
        {
            try
            {
                var actor = await ActorAsync(customer, ct);
                if (actor is null) continue;
                foreach (var ticket in await actor.Provider.SearchAsync($"customer_id:{customer} AND updated_at:>now-{minutes}m", 1, 50, "updated_at", true, ct))
                    foreach (var article in await actor.Provider.GetArticlesAsync(ticket.Id, ct))
                        if (article.TicketId == ticket.Id && article.CreatedAt > clock.GetUtcNow() - window && await NotifyAsync(actor, ticket, article, ct)) notices++;
            }
            catch (Exception error) when (error is TicketingProviderException or TicketingAccessException or HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            { log.LogWarning("Helpdesk reconciliation skipped one customer: {Error}.", error.GetType().Name); }
        }
        return notices;
    }

    private sealed record Actor(ITicketingWorkspaceProvider Provider, long Customer, Guid? Member, TicketingGuest? Guest);

    private async Task<Actor?> ActorAsync(long customer, CancellationToken ct)
    {
        var member = await db.Set<TicketingIdentity>().AsNoTracking().SingleOrDefaultAsync(m => m.ProviderUserId == customer && m.State == TicketingOperationState.Applied && m.DeactivatedAt == null, ct);
        var guest = member is null ? await db.Set<TicketingGuest>().AsNoTracking().SingleOrDefaultAsync(g => g.ProviderUserId == customer && g.State == TicketingOperationState.Applied && g.DeactivatedAt == null, ct) : null;
        if (member is null && guest is null) return null;
        try { return new(await actors.CustomerAsync(customer, member?.AccountId ?? guest!.Id, guest is not null, ct), customer, member?.AccountId, guest); }
        // A deliberately suppressed or changed customer (for example during account deletion) is a final state: no notice, no retry.
        catch (TicketingAccessException) { return null; }
    }

    private async Task HandleAsync(long ticketId, long? articleId, long? customer, CancellationToken ct)
    {
        // Article-free changes (status, queue, owner) carry nothing to tell the member: the ticket page shows them when opened.
        if (articleId is not long id || customer is not long owner) return;
        var actor = await ActorAsync(owner, ct);
        if (actor is null) return;
        var ticket = await actor.Provider.GetTicketAsync(ticketId, ct);
        if (ticket is null || ticket.CustomerId != owner) return;
        var article = (await actor.Provider.GetArticlesAsync(ticketId, ct)).SingleOrDefault(a => a.Id == id && a.TicketId == ticketId);
        if (article is not null) await NotifyAsync(actor, ticket, article, ct);
    }

    private async Task<bool> NotifyAsync(Actor actor, SupportTicket ticket, SupportArticle article, CancellationToken ct)
    {
        // Only a public reply from support staff. The customer's own messages and anything internal never notify.
        if (article.Internal || article.Sender != "Agent" || ticket.CustomerId != actor.Customer || ticket.OperationReference is null) return false;
        var now = clock.GetUtcNow();
        if (actor.Member is Guid member)
        {
            var dedupe = SupportNotice.Dedupe(article.Id);
            if (await db.Notifications.AnyAsync(n => n.AccountId == member && n.DedupeKey == dedupe, ct)) return false;
            // Notifier applies the member's current status, barring and push settings; it never emails this kind.
            await notifier.NotifyAsync(member, NotificationKind.Support, null, null, SupportNotice.Subject(ticket.Id), now, ct, dedupe: dedupe);
            return true;
        }
        if (actor.Guest is not { } guest || guest.EmailProtected.Length == 0) return false;
        // Guests have no app: one content-free email per reply, recorded first so a retry or a second replica never resends.
        var marker = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("guest-notice:" + article.Id)).AsSpan(0, 16));
        if (await db.Set<TicketingOperation>().AnyAsync(o => o.Id == marker, ct)) return false;
        db.Add(new TicketingOperation { Id = marker, AccountId = guest.Id, Kind = "guest-notice", RequestDigest = "", ProviderTicketId = ticket.Id, ProviderArticleId = article.Id, State = TicketingOperationState.Pending, CreatedAt = now, UpdatedAt = now });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); return false; }
        var address = ContactHistoryPolicy.Decrypt(keys.Encryption, guest.EmailProtected);
        var page = configuration["Ticketing:Guest:FrontendUrl"] is { Length: > 0 } url ? $" at {url}" : "";
        var body = $"Swoovr support replied to your request {TicketingProjection.Reference(ticket.Id)}. To read it, request a code on the Swoovr support page{page}. For your privacy, the reply isn't included in this email.";
        var outcome = "unknown";
        try { outcome = (await email.SendOutcomeAsync(new(address, "Swoovr support replied to your request", body), ct)).Status; }
        catch (Exception error) when (error is HttpRequestException or IOException or InvalidOperationException) { }
        // Provider acceptance is recorded as such; it is not proof that the email was delivered.
        var row = await db.Set<TicketingOperation>().SingleAsync(o => o.Id == marker, ct);
        (row.State, row.UpdatedAt) = (outcome == "accepted" ? TicketingOperationState.Applied : TicketingOperationState.Uncertain, clock.GetUtcNow());
        await db.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>Runs the processor every few seconds and the reconciliation sweep every few minutes (configurable).</summary>
public sealed class TicketingEventWorker(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<TicketingEventWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var poll = TimeSpan.FromSeconds(Math.Clamp(configuration.GetValue("Ticketing:Events:PollSeconds", 5), 1, 300));
        var sweep = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("Ticketing:Events:ReconcileMinutes", 10), 1, 1440));
        var lastSweep = DateTimeOffset.MinValue;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<TicketingEventProcessor>();
                await processor.ProcessDueAsync(20, stoppingToken);
                if (clock.GetUtcNow() - lastSweep >= sweep)
                {
                    // The sweep looks back twice its interval, so a missed event is caught by at least one sweep.
                    await processor.ReconcileAsync(sweep * 2, stoppingToken);
                    lastSweep = clock.GetUtcNow();
                }
            }
            catch (Exception error) when (error is DbUpdateException or InvalidOperationException or Npgsql.NpgsqlException && !stoppingToken.IsCancellationRequested)
            { log.LogWarning("Helpdesk event worker pass failed: {Error}.", error.GetType().Name); }
            try { await Task.Delay(poll, clock, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
