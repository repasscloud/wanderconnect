using Swoovr.Contracts;

namespace Swoovr.Api;

/// <summary>D211/D222: select before authentication or any account side effects. Health and hubs have separate protocols.</summary>
public sealed class ApiVersionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")) { await next(context); return; }
        var supplied = context.Request.Headers.TryGetValue(ApiContract.Header, out var values);
        var selection = ApiContract.Select(supplied ? values.Count == 1 ? values[0] ?? "" : "" : null);
        context.Response.Headers.Append("Vary", ApiContract.Header);
        if (selection.Code is { } code)
        {
            context.Response.Headers.CacheControl = "no-store";
            await Results.Json(new { code, supportedVersions = ApiContract.Supported }, statusCode: 400,
                contentType: "application/problem+json").ExecuteAsync(context);
            return;
        }
        context.Response.Headers[ApiContract.Header] = selection.Version;
        await next(context);
    }
}
