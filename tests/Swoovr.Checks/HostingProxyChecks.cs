using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Swoovr.Hosting;
using System.Net;
static class HostingProxyChecks
{
    public static async Task Run()
    {
        var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName="Uat"});builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{
            ["Hosting:PublicOrigin"]="https://uat.swoovr.app",["AllowedHosts"]="uat.swoovr.app",["Hosting:TrustedProxyNetworks"]="10.42.0.0/23",
            ["Keycloak:Authority"]="https://auth.uat.swoovr.app/realms/swoovr-members",["Keycloak:ClientId"]="swoovr-web",["Keycloak:ClientSecret"]="fictional",["Api:BaseUrl"]="https://api.internal.example.test/"});
        HostRuntime.Configure(builder,HostKind.Web);await using var app=builder.Build();var options=app.Services.GetRequiredService<IOptions<ForwardedHeadersOptions>>();
        var middleware=new ForwardedHeadersMiddleware(_=>Task.CompletedTask,NullLoggerFactory.Instance,options);var count=0;
        foreach(var remote in new[]{"10.42.0.5","198.51.100.2","127.0.0.1"})
        {
            var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=IPAddress.Parse(remote);context.Request.Scheme="http";
            context.Request.Headers["X-Forwarded-For"]="203.0.113.4, 10.42.0.6";context.Request.Headers["X-Forwarded-Proto"]="https, https";
            await middleware.Invoke(context);var trusted=remote=="10.42.0.5";
            if(context.Connection.RemoteIpAddress!.ToString()!=(trusted?"203.0.113.4":remote)||context.Request.Scheme!=(trusted?"https":"http"))throw new Exception("Trusted proxy chain / untrusted spoof refusal failed.");count++;
        }
        string? address=null;
        HostRuntime.Use(app,HostKind.Web);
        app.Run(context=>{address=Swoovr.Api.Accounts.PhoneEndpoints.ClientAddress(context,builder.Configuration);return Task.CompletedTask;});
        var pipeline=((IApplicationBuilder)app).Build();var forwarded=new DefaultHttpContext{RequestServices=app.Services};
        forwarded.Connection.RemoteIpAddress=IPAddress.Parse("10.42.0.5");forwarded.Request.Scheme="http";
        forwarded.Request.Headers["X-Forwarded-For"]="192.0.2.99, 127.0.0.1";forwarded.Request.Headers["X-Forwarded-Proto"]="https, https";
        await pipeline(forwarded);if(address!="127.0.0.1")throw new Exception("Phone budget reparsed a leftover untrusted chain.");count++;
        if(!forwarded.Response.Headers.ContentSecurityPolicy.ToString().Contains("form-action 'self' https://auth.uat.swoovr.app;",StringComparison.Ordinal))throw new Exception("Configured Keycloak logout origin is missing from CSP.");count++;
        Console.WriteLine($"PASS trusted-proxy middleware: {count} in-process request checks; no real ingress/network claim.");
    }
}
