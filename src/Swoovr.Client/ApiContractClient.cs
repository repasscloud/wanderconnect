using Swoovr.Contracts;

namespace Swoovr.Client;

public static class ApiContractClient
{
    public static HttpClient Configure(HttpClient http)
    {
        if (!http.DefaultRequestHeaders.Contains(ApiContract.Header))
            http.DefaultRequestHeaders.Add(ApiContract.Header, ApiContract.Current);
        return http;
    }
}
