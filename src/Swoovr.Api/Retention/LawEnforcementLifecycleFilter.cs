using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>Police closure/referral clocks and relationships cannot change during scheduled disposal.</summary>
public sealed class LawEnforcementLifecycleFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var current = await http.RequestServices.GetRequiredService<AccountResolver>().ResolveAsync(http.User, http.RequestAborted);
        if (current?.Allows(AccountRole.Legal) != true || http.RequestServices.GetService<SwoovrDbContext>() is not { } db)
            return await next(context);
        await using var barrier = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            ? await PreservationLease.SharedAsync(db, http.RequestAborted) : await PreservationLease.ExclusiveAsync(db, http.RequestAborted);
        return await next(context);
    }
}
