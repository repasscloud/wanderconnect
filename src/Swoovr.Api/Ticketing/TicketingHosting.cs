using Microsoft.AspNetCore.Http.Features;
namespace Swoovr.Api.Ticketing;

public static class TicketingHosting
{
    public static void Use(WebApplication app)=>app.Use(async(c,next)=>
    {
        if(!await GuestSupportEndpoints.OriginAsync(c,app.Configuration))return;
        if(c.Request.Path.StartsWithSegments("/api/me/tickets")||c.Request.Path.StartsWithSegments("/api/admin/tickets")||c.Request.Path.StartsWithSegments(GuestSupportEndpoints.Prefix))
        {
            // Issue 220 (D330): the member upload routes take files up to the owner's limits; everything else stays at 300,000 bytes.
            var limit=TicketingEndpoints.IsUpload(c.Request.Path)&&HttpMethods.IsPost(c.Request.Method)?Swoovr.Contracts.SupportAttachmentPolicy.MaxUploadRequestBytes:300000;
            if(c.Features.Get<IHttpMaxRequestBodySizeFeature>() is {IsReadOnly:false} size)size.MaxRequestBodySize=limit;
            if(c.Request.ContentLength>limit){c.Response.StatusCode=413;return;}
        }
        await next();
    });
}
