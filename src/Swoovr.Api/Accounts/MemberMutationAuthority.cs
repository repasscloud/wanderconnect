using Microsoft.EntityFrameworkCore;
using Swoovr.Domain;

namespace Swoovr.Api.Accounts;

/// <summary>Member writes cannot recreate personal rows after final deletion. Long media screening has a separate mutex.</summary>
public sealed class MemberMutationAuthority(RequestDelegate next)
{
    public static async ValueTask ReleaseForEffectsAsync(IServiceProvider services)
    {
        var context = services.GetService<IHttpContextAccessor>()?.HttpContext;
        if (context?.Items[typeof(MemberMutationAuthority)] is AccountLifecycleLease lease) await lease.DisposeAsync();
    }
    public async Task InvokeAsync(HttpContext context, AccountResolver resolver)
    {
        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method)
            || !context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/api/internal")
            || context.User.Identity?.IsAuthenticated != true) { await next(context); return; }
        var current = await resolver.ResolveAsync(context.User, context.RequestAborted);
        if (current is null) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        if (current.Facts.Realm != AccountRealm.Members || current.Account is null || resolver.Db is not { } db) { await next(context); return; }
        await using var lease = await AccountLifecycleLease.AcquireAsync(db, current.Account.Id, context.RequestAborted);
        await db.Entry(current.Account).ReloadAsync(context.RequestAborted);
        if (db.Entry(current.Account).State == EntityState.Detached) { context.Response.StatusCode = StatusCodes.Status401Unauthorized; return; }
        context.Items[typeof(MemberMutationAuthority)] = lease;
        try { await next(context); }
        finally { context.Items.Remove(typeof(MemberMutationAuthority)); }
    }
}
