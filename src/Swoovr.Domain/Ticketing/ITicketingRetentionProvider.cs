namespace Swoovr.Domain;

/// <summary>Ticket facts the retention ledger needs: IDs, group, state type and Zammad's last close time. No content.</summary>
public sealed record RetentionTicket(long Id, long CustomerId, long GroupId, long StateId, DateTimeOffset? LastCloseAt, DateTimeOffset UpdatedAt);

/// <summary>A data privacy task's progress: "in process", "completed" or "failed" (Zammad's own states).</summary>
public sealed record RetentionTask(long Id, string State);

/// <summary>
/// D337 (Q136): the dedicated retention identity. It reads ticket metadata in every group and deletes only through Zammad's
/// data privacy tasks (`admin.data_privacy`), which remove the ticket's articles, attachments and search entries.
/// </summary>
public interface ITicketingRetentionProvider
{
    Task<IReadOnlyList<RetentionTicket>> TicketsAsync(int page, int pageSize, CancellationToken ct);
    Task<RetentionTicket?> TicketAsync(long id, CancellationToken ct);
    Task<IReadOnlyDictionary<long, string>> GroupNamesAsync(CancellationToken ct);
    /// <summary>State IDs whose type is closed or merged.</summary>
    Task<IReadOnlySet<long>> ClosedStatesAsync(CancellationToken ct);
    Task<RetentionTask> DeleteTicketAsync(long ticketId, CancellationToken ct);
    Task<RetentionTask> DeleteCustomerAsync(long userId, CancellationToken ct);
    Task<RetentionTask?> TaskAsync(long taskId, CancellationToken ct);
    Task<bool> CustomerHasTicketsAsync(long userId, CancellationToken ct);
}
