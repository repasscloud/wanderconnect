using Swoovr.Contracts;
namespace Swoovr.Client;
public sealed partial class AdminClient
{
    public Task<ApiResult<MfaView>> MfaAsync(Guid id,CancellationToken ct=default)=>SendAsync<MfaView>(HttpMethod.Get,$"api/admin/mfa/{id}",null,ct);
    public Task<ApiResult<MfaView>> RequireMfaAsync(Guid id,MfaChange request,CancellationToken ct=default)=>SendAsync<MfaView>(HttpMethod.Put,$"api/admin/mfa/{id}/requirement",request,ct);
    public Task<ApiResult<MfaView>> ResetMfaAsync(Guid id,MfaReset request,CancellationToken ct=default)=>SendAsync<MfaView>(HttpMethod.Post,$"api/admin/mfa/{id}/reset",request,ct);
    public Task<ApiResult<MfaElevationChallenge>> BeginMfaElevationAsync(CancellationToken ct=default)=>SendAsync<MfaElevationChallenge>(HttpMethod.Post,"api/admin/mfa/elevation/begin",new{},ct);
    public Task<ApiResult<MfaElevationView>> CompleteMfaElevationAsync(string challenge,CancellationToken ct=default)=>SendAsync<MfaElevationView>(HttpMethod.Post,"api/admin/mfa/elevation/complete",new MfaElevationComplete(challenge),ct);
    public Task<ApiResult<MfaElevationView>> MfaElevationAsync(CancellationToken ct=default)=>SendAsync<MfaElevationView>(HttpMethod.Get,"api/admin/mfa/elevation",null,ct);
}
public sealed partial class MemberClient
{
    public Task<ApiResult<MfaView>> MfaAsync(CancellationToken ct=default)=>SendAsync<MfaView>(new HttpRequestMessage(HttpMethod.Get,"api/me/mfa"),ct);
    public Task<ApiResult<MfaView>> DisableMfaAsync(CancellationToken ct=default)=>SendAsync<MfaView>(new HttpRequestMessage(HttpMethod.Post,"api/me/mfa/disable"){Content=System.Net.Http.Json.JsonContent.Create(new{})},ct);
}
