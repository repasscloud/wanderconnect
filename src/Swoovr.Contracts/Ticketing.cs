namespace Swoovr.Contracts;

public sealed record TicketSummary(long Id,string Reference,string Title,long StateId,long PriorityId,DateTimeOffset UpdatedAt);
public sealed record TicketAttachment(long Id,string Filename,string ContentType,long Size);
public sealed record TicketArticle(long Id,string Body,bool Private,string Sender,DateTimeOffset CreatedAt,IReadOnlyList<TicketAttachment> Attachments,string? Channel=null);
public sealed record TicketDetail(TicketSummary Ticket,IReadOnlyList<TicketArticle> Articles,TicketStaffFields? Staff=null,bool CanReply=true);
public sealed record TicketStaffFields(long GroupId,long OwnerId,Guid? CustomerAccountId,IReadOnlyDictionary<string,string> Classification,bool Guest=false);
public sealed record TicketPage(IReadOnlyList<TicketSummary> Items,int Page,int PageSize,bool HasMore);
/// <param name="Reference">Optional SW- report reference of the member's own report; the API links the request to that case only if it is theirs.</param>
public sealed record TicketCreate(Guid OperationId,string Title,string Body,string? Reference=null);
/// <summary>A safe reference from a support request to a Swoovr case or appeal. Swoovr stays authoritative for outcomes.</summary>
public sealed record TicketLink(Guid CaseId,Guid? AppealId=null);
public sealed record TicketLinkView(Guid CaseId,Guid? AppealId,string CaseStatus,DateTimeOffset LinkedAt);
public sealed record TicketUpload(string Filename,string ContentType,byte[] Data);
public sealed record TicketReply(Guid OperationId,string Body,bool Private=false,IReadOnlyList<TicketUpload>? Attachments=null);
public sealed record TicketChange(Guid OperationId,DateTimeOffset ExpectedUpdatedAt,long? GroupId=null,long? OwnerId=null,long? StateId=null,long? PriorityId=null,IReadOnlyDictionary<string,string>? Classification=null);
public sealed record TicketOnBehalf(Guid AccountId,TicketCreate Ticket);
public sealed record TicketCustomerLookup(Guid AccountId=default,bool Create=false,string? Query=null);
public sealed record TicketCustomerView(Guid AccountId,string State);
public sealed record TicketOption(long Id,string Name);
public sealed record TicketCapabilities(IReadOnlyList<TicketOption> Groups,IReadOnlyList<TicketOption> TransferGroups,IReadOnlyList<TicketOption> States,IReadOnlyList<TicketOption> Priorities,bool Attachments,string AttachmentPolicy,IReadOnlyList<TicketOwner> Owners,IReadOnlyList<TicketCategory> Categories,IReadOnlyList<string> Sensitivities,IReadOnlyList<string> Resolutions);
public sealed record TicketBulkItem(long Id,TicketChange Change);
public sealed record TicketBulk(IReadOnlyList<TicketBulkItem> Items);
public sealed record TicketBulkResult(long Id,int Status,string? Code,TicketSummary? Ticket);
public sealed record TicketTextModule(long Id,string Name,string Body);
public sealed record TicketMacro(long Id,string Name,bool Supported);
public sealed record TicketMacroApply(Guid OperationId,DateTimeOffset ExpectedUpdatedAt);
public sealed record TicketDiagnostics(string Provider,long Mappings,long UncertainMappings,long UncertainOperations,long PendingDeliveries,long FailedDeliveries);
public sealed record TicketDownload(string Filename,string ContentType,byte[] Data);

public sealed record TicketOwner(long Id,string Name,IReadOnlyList<long> GroupIds);
public sealed record TicketCategory(string Id,IReadOnlyList<string> Subcategories);
public sealed record TicketMemberOptions(IReadOnlyList<TicketOption> States,IReadOnlyList<TicketOption> Priorities,string AttachmentPolicy);

// Guest support (no membership): for the separately built public website. See docs/delivery/zammad/GUEST-API.md.
public sealed record GuestCodeRequest(string Email,string? BotToken=null);
public sealed record GuestCodeReceipt(string Status);
public sealed record GuestGrantRequest(string Email,string Code);
public sealed record GuestGrant(string Grant,DateTimeOffset ExpiresAt);
public sealed record GuestTicketCreate(Guid OperationId,string Category,string Title,string Body);
public sealed record GuestCategory(string Id,string Label);
