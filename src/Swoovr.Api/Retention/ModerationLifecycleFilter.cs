using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>Authorised moderation writes commit before disposal; external notification effects release the request barrier.</summary>
public sealed class ModerationLifecycleFilter : IEndpointFilter
{
    public static PreservationLease? Current(HttpContext context) => context.Items[typeof(ModerationLifecycleFilter)] as PreservationLease;
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var resolver = http.RequestServices.GetRequiredService<AccountResolver>();
        var (_, denied) = await AdminEndpoints.StaffAsync(http.User, resolver, AccountRole.Support, http.RequestAborted);
        if (denied is not null) return denied;
        if (http.RequestServices.GetService<SwoovrDbContext>() is not { } db) return await next(context);
        await using var barrier = HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
            ? await PreservationLease.SharedAsync(db, http.RequestAborted) : await PreservationLease.ExclusiveAsync(db, http.RequestAborted);
        http.Items[typeof(ModerationLifecycleFilter)] = barrier;
        try { return await next(context); }
        finally { http.Items.Remove(typeof(ModerationLifecycleFilter)); }
    }
}
