using Swoovr.Contracts;
namespace Swoovr.Client;
public sealed partial class AdminClient
{
    public Task<ApiResult<StaffAccountPage>> StaffAccountsAsync(string? search = null, string? role = null, string? status = null, int page = 1, CancellationToken token = default, string? mfa = null, DateTimeOffset? signedInAfter = null, DateTimeOffset? signedInBefore = null) =>
        SendAsync<StaffAccountPage>(HttpMethod.Get, $"api/admin/staff/accounts?search={Uri.EscapeDataString(search ?? "")}&role={Uri.EscapeDataString(role ?? "")}&status={Uri.EscapeDataString(status ?? "")}&page={page}&mfa={Uri.EscapeDataString(mfa??"")}&signedInAfter={Uri.EscapeDataString(signedInAfter?.ToString("O")??"")}&signedInBefore={Uri.EscapeDataString(signedInBefore?.ToString("O")??"")}", null, token);
    public Task<ApiResult<StaffAccountDetail>> StaffAccountAsync(Guid id, CancellationToken token = default) => SendAsync<StaffAccountDetail>(HttpMethod.Get, $"api/admin/staff/accounts/{id}", null, token);
    public Task<ApiResult<StaffAccountSummary>> CreateStaffAsync(StaffAccountCreate request, CancellationToken token = default) => SendAsync<StaffAccountSummary>(HttpMethod.Post, "api/admin/staff/accounts", request, token);
    public Task<ApiResult<StaffAccountSummary>> UpdateStaffAsync(Guid id, StaffAccountUpdate request, CancellationToken token = default) => SendAsync<StaffAccountSummary>(HttpMethod.Put, $"api/admin/staff/accounts/{id}", request, token);
    public Task<ApiResult<StaffAccountSummary>> StaffSecurityAsync(Guid id, StaffSecurityAction request, CancellationToken token = default) => SendAsync<StaffAccountSummary>(HttpMethod.Post, $"api/admin/staff/accounts/{id}/security", request, token);
}
