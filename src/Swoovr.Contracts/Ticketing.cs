namespace Swoovr.Contracts;

public sealed record TicketSummary(long Id,string Reference,string Title,long StateId,long PriorityId,DateTimeOffset UpdatedAt);
public sealed record TicketAttachment(long Id,string Filename,string ContentType,long Size);
public sealed record TicketArticle(long Id,string Body,bool Private,string Sender,DateTimeOffset CreatedAt,IReadOnlyList<TicketAttachment> Attachments,string? Channel=null)
{
    /// <summary>D336: files of this message still in quarantine ("checking") or refused ("blocked"); released files are in a later article.</summary>
    public IReadOnlyList<TicketHeldFile> Held { get; init; } = [];
}
/// <summary>D336: Status "checking" or "blocked". Detection (signature name) is shown to staff only.</summary>
public sealed record TicketHeldFile(string Filename,long Size,string Status,string? Detection=null);
public sealed record TicketDetail(TicketSummary Ticket,IReadOnlyList<TicketArticle> Articles,TicketStaffFields? Staff=null,bool CanReply=true);
public sealed record TicketStaffFields(long GroupId,long OwnerId,Guid? CustomerAccountId,IReadOnlyDictionary<string,string> Classification,bool Guest=false);
public sealed record TicketPage(IReadOnlyList<TicketSummary> Items,int Page,int PageSize,bool HasMore);
/// <param name="Reference">Optional SW- report reference of the member's own report; the API links the request to that case only if it is theirs.</param>
/// <param name="Attachments">Issue 220: files with the first message (SupportAttachmentPolicy); members send them through the upload route.</param>
public sealed record TicketCreate(Guid OperationId,string Title,string Body,string? Reference=null,IReadOnlyList<TicketUpload>? Attachments=null);
/// <summary>Issue 220: the member closes their own request. A closed request can't be reopened.</summary>
public sealed record TicketClose(Guid OperationId);
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
public sealed record TicketDiagnostics(string Provider,long Mappings,long UncertainMappings,long UncertainOperations,long PendingDeliveries,long FailedDeliveries,
    TicketStaffSyncStatus? Staff=null)
{
    /// <summary>D336: the malware scanner and quarantined files.</summary>
    public TicketScanStatus? Scanning { get; init; }
    /// <summary>D337: helpdesk retention.</summary>
    public TicketRetentionStatus? Retention { get; init; }
}
public sealed record TicketScanStatus(string Scanner,bool Healthy,long Waiting,long Retrying,long Blocked,long Released);
public sealed record TicketRetentionStatus(bool Configured,long Tracked,long Open,long Held,long Failed,long Disposed,long RetirementsWaiting);
/// <summary>Staff permission mirroring into the helpdesk. UnmanagedAgents counts helpdesk agents Swoovr doesn't control (null if not checked).</summary>
public sealed record TicketStaffSyncStatus(bool Configured,long Mirrored,long WebSignIn,long Failed,DateTimeOffset? LastSyncedAt,DateTimeOffset? LastVerifiedAt,long? UnmanagedAgents);
public sealed record TicketStaffSyncRun(int Checked,int Changed,int Failed,int Drifted);
public sealed record TicketDownload(string Filename,string ContentType,byte[] Data);

public sealed record TicketOwner(long Id,string Name,IReadOnlyList<long> GroupIds);
public sealed record TicketCategory(string Id,IReadOnlyList<string> Subcategories);
/// <summary>Issue 223: open support requests by status for the Admin overview, within the caller's queues. More: past the first 100 checked.</summary>
public sealed record TicketStateCount(long StateId,string Name,int Count);
public sealed record TicketOverview(IReadOnlyList<TicketStateCount> States,bool More);
public sealed record TicketMemberOptions(IReadOnlyList<TicketOption> States,IReadOnlyList<TicketOption> Priorities,string AttachmentPolicy);

// Guest support (no membership): for the separately built public website. See docs/delivery/zammad/GUEST-API.md.
public sealed record GuestCodeRequest(string Email,string? BotToken=null);
public sealed record GuestCodeReceipt(string Status);
public sealed record GuestGrantRequest(string Email,string Code);
public sealed record GuestGrant(string Grant,DateTimeOffset ExpiresAt);
public sealed record GuestTicketCreate(Guid OperationId,string Category,string Title,string Body);
public sealed record GuestCategory(string Id,string Label);

/// <summary>D337 (#224): a ticket's helpdesk retention. Categories: General, Billing, FinancialRecord, TrustAndSafety, Legal, Privacy, Security.</summary>
public sealed record TicketRetentionView(string Category,string GroupCategory,string? OverrideCategory,string? OverrideReason,DateTimeOffset? ClosedAt,DateTimeOffset? RetainUntil,string State,string? KeptFor,bool CanChange);
/// <summary>Null Category clears the override. A reason is always required (audited).</summary>
public sealed record TicketRetentionChange(string? Category,string Reason);
