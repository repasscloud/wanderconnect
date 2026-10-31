using Microsoft.EntityFrameworkCore;
using Swoovr.Api.Accounts;
using Swoovr.Infrastructure.Persistence;
using Swoovr.Domain;

namespace Swoovr.Api.Retention;

public sealed class LegalHoldLifecycleFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        if (http.RequestServices.GetService<SwoovrDbContext>() is not { } db
            || !http.Request.Path.StartsWithSegments("/api/admin/holds")
            || !Guid.TryParse(http.Request.RouteValues["id"]?.ToString(), out var id)) return await next(context);
        var current = await http.RequestServices.GetRequiredService<AccountResolver>().ResolveAsync(http.User, http.RequestAborted);
        if (current?.Allows(AccountRole.Legal) != true) return await next(context);
        var hold = await db.LegalHolds.AsNoTracking().SingleOrDefaultAsync(h => h.Id == id, http.RequestAborted);
        if (hold is null) return await next(context);
        await using var barrier = await PreservationLease.ExclusiveAsync(db, http.RequestAborted);
        await using var holdMutex = await PreservationLease.HoldAsync(db, hold.Id, http.RequestAborted);
        return await next(context);
    }
}
