using System.Net.Http.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

public sealed class FoundationClient(HttpClient transport)
{
    private readonly HttpClient http = ApiContractClient.Configure(transport);
    public async Task<FoundationStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<FoundationStatus>("api/status", cancellationToken)
        ?? throw new InvalidOperationException("API returned an empty status.");
}
