using Swoovr.Contracts;
using Swoovr.Client;

static class ApiContractChecks
{
    public static void Run()
    {
        foreach (var requested in new string?[] { null, "2026-10-28" })
        {
            var result = ApiContract.Select(requested);
            if (result.Version != "2026-10-28" || result.Code is not null) throw new Exception("Contract default drifted.");
        }
        foreach (var requested in new[] { "", " ", "v1", "2026-02-30", "2026-1-28", "2026-10-28,2026-10-28", "2026-10-28 ", "２０２６-１０-２８" })
            if (ApiContract.Select(requested).Code != "invalid-api-version") throw new Exception("Invalid contract accepted.");
        foreach (var requested in new[] { "2024-02-29", "2026-10-29", "2020-01-01" })
            if (ApiContract.Select(requested).Code != "unsupported-api-version") throw new Exception("Unsupported contract accepted.");
        using var defaultClient = new HttpClient();
        ApiContractClient.Configure(defaultClient);
        ApiContractClient.Configure(defaultClient);
        if (defaultClient.DefaultRequestHeaders.GetValues(ApiContract.Header).Single() != ApiContract.Current)
            throw new Exception("Client default was duplicated or unpinned.");
        using var pinnedClient = new HttpClient();
        pinnedClient.DefaultRequestHeaders.Add(ApiContract.Header, "2020-01-01");
        ApiContractClient.Configure(pinnedClient);
        if (pinnedClient.DefaultRequestHeaders.GetValues(ApiContract.Header).Single() != "2020-01-01")
            throw new Exception("Explicit client date was silently replaced.");
        Console.WriteLine("PASS: 15 API contract date/client cases (fixed default, calendar/format, duplicate and unsupported dates).");
    }
}
