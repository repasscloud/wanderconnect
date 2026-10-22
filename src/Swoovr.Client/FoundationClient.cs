using System.Net.Http.Json;
using Swoovr.Contracts;
namespace Swoovr.Client;

public sealed class FoundationClient(HttpClient http)
{
    public async Task<FoundationStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
        await http.GetFromJsonAsync<FoundationStatus>("api/v1/status", cancellationToken)
        ?? throw new InvalidOperationException("API returned an empty status.");
}
