namespace Swoovr.Domain;

// Conversations, group/assignment/state and article bodies are projections fetched from the provider, never EF entities.
public sealed record SupportCustomer(long Id, string Login, string? Email, bool Active);
public sealed record SupportTicket(long Id, string Number, string Title, long CustomerId, long GroupId, long OwnerId,
    long StateId, long PriorityId, DateTimeOffset UpdatedAt, string? OperationReference = null);
public sealed record SupportAttachment(long Id, string Filename, string ContentType, long Size);
public sealed record SupportArticle(long Id, long TicketId, string Body, string ContentType, bool Internal, string Sender,
    DateTimeOffset CreatedAt, IReadOnlyList<SupportAttachment> Attachments);
public sealed record SupportTicketDraft(string Title, string Body, long CustomerId, long GroupId, string OperationReference);
public sealed record SupportTicketChange(long? GroupId = null, long? OwnerId = null, long? StateId = null, long? PriorityId = null);

/// <summary>Implemented with a credential whose provider permissions match the resolved actor; never a universal agent token.</summary>
public interface ITicketingProvider
{
    Task<SupportCustomer?> FindCustomerAsync(string canonicalLogin, CancellationToken cancellationToken);
    Task<SupportCustomer> CreateCustomerAsync(string canonicalLogin, string? verifiedEmail, CancellationToken cancellationToken);
    Task<SupportCustomer> UpdateCustomerAsync(long id, string? verifiedEmail, bool active, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportTicket>> SearchAsync(string query, int page, int pageSize, CancellationToken cancellationToken);
    Task<SupportTicket?> GetTicketAsync(long id, CancellationToken cancellationToken);
    Task<SupportTicket> CreateTicketAsync(SupportTicketDraft draft, CancellationToken cancellationToken);
    Task<SupportTicket> ChangeTicketAsync(long id, SupportTicketChange change, CancellationToken cancellationToken);
    Task<IReadOnlyList<SupportArticle>> GetArticlesAsync(long ticketId, CancellationToken cancellationToken);
    Task<SupportArticle> ReplyAsync(long ticketId, string body, bool internalNote, bool customer, CancellationToken cancellationToken);
}

/// <summary>Writes may have reached Zammad despite a lost response. Callers must reconcile before attempting another write.</summary>
public sealed class TicketingProviderException(string code, bool uncertain = false) : Exception(code)
{
    public string Code { get; } = code;
    public bool Uncertain { get; } = uncertain;
}
