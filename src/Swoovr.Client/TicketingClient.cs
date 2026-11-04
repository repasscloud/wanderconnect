using System.Net.Http.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

public sealed partial class MemberClient
{
    public Task<ApiResult<TicketMemberOptions>> TicketOptionsAsync(CancellationToken token=default)=>SendAsync<TicketMemberOptions>(new(HttpMethod.Get,"api/me/tickets/options"),token);
    public Task<ApiResult<TicketPage>> TicketsAsync(int page=1,int pageSize=25,CancellationToken token=default)=>SendAsync<TicketPage>(new(HttpMethod.Get,$"api/me/tickets?page={page}&pageSize={pageSize}"),token);
    public Task<ApiResult<TicketDetail>> TicketAsync(long id,CancellationToken token=default)=>SendAsync<TicketDetail>(new(HttpMethod.Get,$"api/me/tickets/{id}"),token);
    public Task<ApiResult<TicketSummary>> CreateTicketAsync(TicketCreate request,CancellationToken token=default)=>SendAsync<TicketSummary>(new(HttpMethod.Post,"api/me/tickets"){Content=JsonContent.Create(request)},token);
    public Task<ApiResult<TicketDetail>> ReplyTicketAsync(long id,TicketReply request,CancellationToken token=default)=>SendAsync<TicketDetail>(new(HttpMethod.Post,$"api/me/tickets/{id}/replies"){Content=JsonContent.Create(request)},token);
    public Task<ApiResult<TicketDownload>> TicketAttachmentAsync(long id,long article,long attachment,CancellationToken token=default)=>SendAsync<TicketDownload>(new(HttpMethod.Get,$"api/me/tickets/{id}/articles/{article}/attachments/{attachment}"),token);
}
public sealed partial class AdminClient
{
    public Task<ApiResult<TicketPage>> TicketsAsync(int page=1,int pageSize=25,string? query=null,long? group=null,long? state=null,long? priority=null,long? owner=null,string sort="updated_at",bool descending=true,CancellationToken token=default)=>SendAsync<TicketPage>(HttpMethod.Get,$"api/admin/tickets?page={page}&pageSize={pageSize}&query={Uri.EscapeDataString(query??"")}&group={group}&state={state}&priority={priority}&owner={owner}&sort={Uri.EscapeDataString(sort)}&descending={descending}",null,token);
    public Task<ApiResult<TicketDetail>> TicketAsync(long id,CancellationToken token=default)=>SendAsync<TicketDetail>(HttpMethod.Get,$"api/admin/tickets/{id}",null,token);
    public Task<ApiResult<TicketSummary>> CreateTicketAsync(TicketOnBehalf request,CancellationToken token=default)=>SendAsync<TicketSummary>(HttpMethod.Post,"api/admin/tickets",request,token);
    public Task<ApiResult<TicketDetail>> ReplyTicketAsync(long id,TicketReply request,CancellationToken token=default)=>SendAsync<TicketDetail>(HttpMethod.Post,$"api/admin/tickets/{id}/replies",request,token);
    public Task<ApiResult<TicketSummary>> ChangeTicketAsync(long id,TicketChange request,CancellationToken token=default)=>SendAsync<TicketSummary>(HttpMethod.Put,$"api/admin/tickets/{id}",request,token);
    public Task<ApiResult<List<TicketBulkResult>>> BulkTicketsAsync(TicketBulk request,CancellationToken token=default)=>SendAsync<List<TicketBulkResult>>(HttpMethod.Post,"api/admin/tickets/bulk",request,token);
    public Task<ApiResult<TicketCapabilities>> TicketCapabilitiesAsync(CancellationToken token=default)=>SendAsync<TicketCapabilities>(HttpMethod.Get,"api/admin/tickets/capabilities",null,token);
    public Task<ApiResult<TicketCustomerView>> TicketCustomerAsync(TicketCustomerLookup request,CancellationToken token=default)=>SendAsync<TicketCustomerView>(HttpMethod.Post,"api/admin/tickets/customers",request,token);
    public Task<ApiResult<List<TicketTextModule>>> TicketTextModulesAsync(CancellationToken token=default)=>SendAsync<List<TicketTextModule>>(HttpMethod.Get,"api/admin/tickets/text-modules",null,token);
    public Task<ApiResult<List<TicketMacro>>> TicketMacrosAsync(CancellationToken token=default)=>SendAsync<List<TicketMacro>>(HttpMethod.Get,"api/admin/tickets/macros",null,token);
    public Task<ApiResult<TicketSummary>> ApplyTicketMacroAsync(long id,long macro,TicketMacroApply request,CancellationToken token=default)=>SendAsync<TicketSummary>(HttpMethod.Post,$"api/admin/tickets/{id}/macros/{macro}",request,token);
    public Task<ApiResult<TicketDiagnostics>> TicketDiagnosticsAsync(CancellationToken token=default)=>SendAsync<TicketDiagnostics>(HttpMethod.Get,"api/admin/tickets/diagnostics",null,token);
    public Task<ApiResult<TicketDownload>> TicketAttachmentAsync(long id,long article,long attachment,CancellationToken token=default)=>SendAsync<TicketDownload>(HttpMethod.Get,$"api/admin/tickets/{id}/articles/{article}/attachments/{attachment}",null,token);
    public Task<ApiResult<List<TicketLinkView>>> TicketLinksAsync(long id,CancellationToken token=default)=>SendAsync<List<TicketLinkView>>(HttpMethod.Get,$"api/admin/tickets/{id}/links",null,token);
    public Task<ApiResult<List<TicketLinkView>>> LinkTicketAsync(long id,TicketLink request,CancellationToken token=default)=>SendAsync<List<TicketLinkView>>(HttpMethod.Post,$"api/admin/tickets/{id}/links",request,token);
}
