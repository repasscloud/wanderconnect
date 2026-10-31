using Microsoft.EntityFrameworkCore;
using Swoovr.Infrastructure.Persistence;
namespace Swoovr.Api;
public static class ApiReadiness
{
    public static void Map(WebApplication app)=>app.MapGet("/health/ready",async(HttpContext context,IServiceProvider services)=>
    {
        context.Response.Headers.CacheControl="no-store";var ready=false;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            if(services.GetService<SwoovrDbContext>() is{}db)
                ready=await db.Database.CanConnectAsync(timeout.Token)&&!(await db.Database.GetPendingMigrationsAsync(timeout.Token)).Any()
                    &&await Retention.RestoreReadiness.ReadyAsync(db,timeout.Token);
        }
        catch(Exception error)when(error is System.Data.Common.DbException or InvalidOperationException or OperationCanceledException){}
        return Results.Json(new{status=ready?"ready":"not-ready"},statusCode:ready?200:503);
    });
}
