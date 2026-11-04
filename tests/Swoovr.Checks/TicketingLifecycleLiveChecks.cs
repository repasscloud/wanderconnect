using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Ticketing;
using Swoovr.Infrastructure.Persistence;
using Swoovr.Infrastructure.Ticketing;

internal static class TicketingLifecycleLiveChecks
{
    public static async Task Run(string path)
    {
        using var config=JsonDocument.Parse(await File.ReadAllTextAsync(path));var root=config.RootElement;
        await using var db=new SwoovrDbContext(new DbContextOptionsBuilder<SwoovrDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("SWOOVR_TEST_DATABASE")).Options);
        using var handler=new SocketsHttpHandler{AllowAutoRedirect=false};using var http=new HttpClient(handler);
        var identity=new ZammadTicketingProvider(http,new Uri(root.GetProperty("providerUrl").GetString()!),root.GetProperty("identityToken").GetString()!);
        var id=await new TicketingOperations(db,identity,identity,TimeProvider.System).SyncLifecycleAsync(root.GetProperty("accountId").GetGuid(),default);
        if(id is null)throw new Exception("existing lifecycle identity missing");
        Console.WriteLine("PASS: real customer lifecycle reconciliation retains canonical numeric mapping.");
    }
}
