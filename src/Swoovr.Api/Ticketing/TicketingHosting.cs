using System.Security.Cryptography;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Https;
namespace Swoovr.Api.Ticketing;

public static class TicketingHosting
{
    public static void Configure(WebApplicationBuilder builder)
    {
        var pins=builder.Configuration.GetSection("Ticketing:Zammad:Staff").GetChildren().SelectMany(s=>s.GetSection("ClientCertificatePins").Get<string[]>()??[]).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(pins.Count>0)builder.WebHost.ConfigureKestrel(k=>k.ConfigureHttpsDefaults(h=>
        {
            h.ClientCertificateMode=ClientCertificateMode.AllowCertificate;
            // Explicit certificate pinning requires TLS proof of private-key possession; no header forwarding is accepted.
            h.ClientCertificateValidation=(cert,chain,errors)=>cert.NotBefore.ToUniversalTime()<=DateTime.UtcNow&&cert.NotAfter.ToUniversalTime()>DateTime.UtcNow&&pins.Contains(cert.GetCertHashString(HashAlgorithmName.SHA256));
        }));
    }
    public static void Use(WebApplication app)=>app.Use(async(c,next)=>
    {
        if(c.Request.Path.StartsWithSegments("/api/me/tickets")||c.Request.Path.StartsWithSegments("/api/admin/tickets"))
        {
            if(c.Features.Get<IHttpMaxRequestBodySizeFeature>() is {IsReadOnly:false} size)size.MaxRequestBodySize=300000;
            if(c.Request.ContentLength>300000){c.Response.StatusCode=413;return;}
        }
        await next();
    });
}
