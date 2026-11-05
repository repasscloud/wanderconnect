using Microsoft.AspNetCore.Http.Features;
namespace Swoovr.Api.Ticketing;

public static class TicketingHosting
{
    public static void Use(WebApplication app)=>app.Use(async(c,next)=>
    {
        if(!await GuestSupportEndpoints.OriginAsync(c,app.Configuration))return;
        if(c.Request.Path.StartsWithSegments("/api/me/tickets")||c.Request.Path.StartsWithSegments("/api/admin/tickets")||c.Request.Path.StartsWithSegments(GuestSupportEndpoints.Prefix))
        {
            if(c.Features.Get<IHttpMaxRequestBodySizeFeature>() is {IsReadOnly:false} size)size.MaxRequestBodySize=300000;
            if(c.Request.ContentLength>300000){c.Response.StatusCode=413;return;}
        }
        await next();
    });
}
