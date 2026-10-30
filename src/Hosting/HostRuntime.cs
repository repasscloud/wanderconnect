using Microsoft.AspNetCore.HttpOverrides;
namespace Swoovr.Hosting;

/// <summary>Server-only common composition; no UI-to-API/Infrastructure project reference.</summary>
public static class HostRuntime
{
    public static void Configure(WebApplicationBuilder builder,HostKind host,bool serving=true)
    {
        var values=builder.Configuration.AsEnumerable().ToDictionary(p=>p.Key,p=>p.Value,StringComparer.OrdinalIgnoreCase);
        var errors=HostingPolicy.Validate(host,builder.Environment.EnvironmentName,values,serving);
        if(errors.Count>0)throw new InvalidOperationException("Hosted configuration rejected: "+string.Join(", ",errors));
        if(!serving)return;
        if(!builder.Environment.IsDevelopment())builder.Services.Configure<ForwardedHeadersOptions>(options=>
        {
            options.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
            options.ForwardLimit=3;
            options.KnownProxies.Clear();options.KnownIPNetworks.Clear();
            foreach(var network in builder.Configuration["Hosting:TrustedProxyNetworks"]!.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        });
        builder.Services.AddHttpClient("swoovr-readiness",http=>http.Timeout=TimeSpan.FromSeconds(3))
            .ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler{AllowAutoRedirect=false}).RemoveAllLoggers();
    }
    public static void Use(WebApplication app,HostKind host)
    {
        if(!app.Environment.IsDevelopment())app.UseForwardedHeaders();
        var connect="'self'";
        if(app.Environment.IsDevelopment())connect+=" ws: wss:";
        else if(Uri.TryCreate(app.Configuration["Hosting:PublicOrigin"],UriKind.Absolute,out var origin))connect+=" wss://"+origin.Authority;
        var forms="'self'";
        if(Uri.TryCreate(app.Configuration["Keycloak:Authority"],UriKind.Absolute,out var authority))forms+=" "+authority.GetLeftPart(UriPartial.Authority);
        var csp=host==HostKind.Api?"default-src 'none'; frame-ancestors 'none'; base-uri 'none'":
            "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; form-action "+forms+"; script-src 'self'"+(host==HostKind.Web?" 'wasm-unsafe-eval'":"")+"; style-src 'self' 'unsafe-inline'; img-src 'self' data: blob:; font-src 'self'; worker-src 'self' blob:; connect-src "+connect;
        app.Use(async(context,next)=>
        {
            if(!app.Environment.IsDevelopment())context.Items["swoovr.forwarded-address-processed"]=true;
            context.Response.Headers.ContentSecurityPolicy=csp;
            context.Response.Headers.XContentTypeOptions="nosniff";
            context.Response.Headers["Referrer-Policy"]="strict-origin-when-cross-origin";
            await next(context);
        });
    }
    public static void MapPortalHealth(WebApplication app)
    {
        app.MapGet("/health/live",()=>Results.Ok(new{status="ok"}));
        app.MapGet("/health/ready",async(HttpContext context,IHttpClientFactory clients)=>
        {
            var ready=false;context.Response.Headers.CacheControl="no-store";
            if(!string.IsNullOrWhiteSpace(app.Configuration["Keycloak:ClientSecret"])&&Uri.TryCreate(app.Configuration["Api:BaseUrl"],UriKind.Absolute,out var api))
            {
                try{using var response=await clients.CreateClient("swoovr-readiness").GetAsync(new Uri(api,"health/ready"),HttpCompletionOption.ResponseHeadersRead,context.RequestAborted);ready=response.StatusCode==System.Net.HttpStatusCode.OK;}
                catch(Exception error)when(error is HttpRequestException or TaskCanceledException){}
            }
            return Results.Json(new{status=ready?"ready":"not-ready"},statusCode:ready?200:503);
        });
    }
}
