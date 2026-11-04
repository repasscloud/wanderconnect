namespace Swoovr.Domain;

public sealed record SupportActor(long Id,string Login,bool Active,IReadOnlyList<string> Roles,IReadOnlyDictionary<long,IReadOnlyList<string>> Groups,string? Email=null);
public sealed record SupportOption(long Id,string Name,string? Kind=null,string? FollowUpPolicy=null);
public sealed record SupportDirectory(IReadOnlyList<SupportOption> Groups,IReadOnlyList<SupportOption> States,IReadOnlyList<SupportOption> Priorities);
public sealed record SupportTextModule(long Id,string Name,string Body);
public sealed record SupportMacro(long Id,string Name,SupportTicketChange Change,bool Supported);
public sealed record SupportUpload(string Filename,string ContentType,byte[] Data);
public sealed record SupportDownload(byte[] Data,string ContentType);

/// <summary>Actor-bound day-to-day workspace; provider facts remain live projections.</summary>
public interface ITicketingWorkspaceProvider : ITicketingProvider
{
    Task<IReadOnlyList<SupportActor>> AgentsAsync(CancellationToken ct);
    Task<SupportActor> ActorAsync(CancellationToken ct);
    Task<SupportActor> UserAsync(long id,CancellationToken ct);
    Task<SupportDirectory> DirectoryAsync(CancellationToken ct);
    Task<IReadOnlyList<SupportTicket>> SearchAsync(string query,int page,int pageSize,string sort,bool descending,CancellationToken ct);
    Task<SupportTicket> ClassifyAsync(long id,IReadOnlyDictionary<string,string> attributes,CancellationToken ct);
    Task<SupportArticle> ReplyWithOperationAsync(long id,string body,bool internalNote,bool customer,IReadOnlyList<SupportUpload> attachments,string operationReference,CancellationToken ct);
    Task<SupportArticle> ReplyAsync(long id,string body,bool internalNote,bool customer,IReadOnlyList<SupportUpload> attachments,CancellationToken ct);
    Task<SupportDownload> DownloadAsync(long ticket,long article,long attachment,CancellationToken ct);
    Task<IReadOnlyList<SupportTextModule>> TextModulesAsync(CancellationToken ct);
    Task<IReadOnlyList<SupportMacro>> MacrosAsync(CancellationToken ct);
}
