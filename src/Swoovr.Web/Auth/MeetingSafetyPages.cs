using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Swoovr.Contracts;
namespace Swoovr.Web.Auth;
/// <summary>Anonymous, tracker-free contact capabilities. Never log their path/body or render another member's details.</summary>
public static class MeetingSafetyPages
{
    public static void UsePrivacy(WebApplication app)=>app.Use(async(context,next)=>
    {
        if(!context.Request.Path.StartsWithSegments("/safety/meeting")&&!context.Request.Path.StartsWithSegments("/safety/contact")){await next();return;}
        Headers(context);
        try{await next();}finally{context.Request.Path=RedactedPath(context);if(context.Request.RouteValues.ContainsKey("token"))context.Request.RouteValues["token"]="[redacted]";}
    });
    public static void Map(WebApplication app)
    {
        app.MapGet("/safety/meeting/{token}",async(string token,HttpContext context,IHttpClientFactory clients,IDataProtectionProvider protection)=>
        {
            Headers(context);if(!Shape(token))return Page(Unavailable(),404);
            try
            {
                var client=clients.CreateClient(Bff.ApiClient);
                if(!await Available(client,token,context.RequestAborted))return Page(Unavailable(),404);
                var grant=ReadGrant(context,protection,token);
                if(grant is null)return Page(CodeForm(token));
                using var response=await client.PostAsJsonAsync("api/safety/meeting-contact/summary",new MeetingSafetyTokenRequest(token,grant),context.RequestAborted);
                if((int)response.StatusCode==428){ClearGrant(context,token);return Page(CodeForm(token));}
                if(!response.IsSuccessStatusCode){ClearGrant(context,token);return Page(Unavailable(),404);}
                var meeting=await response.Content.ReadFromJsonAsync<SharedMeetingSafetyView>(context.RequestAborted);if(meeting is null)return Page(Unavailable(),404);
                var status=meeting.Status switch{"home_safe"=>"Member marked home safe","ended"=>"Monitoring ended; safety was not asserted","cancelled"=>"Meeting cancelled","missed_check_in"=>"Missed / unconfirmed check-in — not proof of an emergency","help_requested"=>"The member requested help","check_in_due"=>"Check-in due","expired"=>"Monitoring expired",_=>meeting.Status};
                var closed=meeting.Status is "home_safe" or "ended" or "cancelled" or "expired" or "revoked";
                var deadline=closed?"Original expected end":"Check in by";
                var checkInHelp=closed?"<p>Monitoring has ended. No further check-ins are scheduled.</p>":"<p>A 30-minute grace period follows the check-in time before selected contacts are alerted. Never wait for an alert if you are concerned.</p>";
                return Page($"<div class=\"page-heading\"><p>Swoovr meeting safety</p><h1>Meeting status</h1></div><section class=\"card account-section stack\" data-meeting-status=\"{E(meeting.Status)}\"><h2>{E(meeting.PreferredFirstName)}</h2><p><strong>{E(status)}</strong></p><p>Details are member-provided and not independently verified.</p><dl><dt>Meeting</dt><dd>{E(meeting.StartDisplay)}</dd><dt>{deadline}</dt><dd>{E(meeting.DeadlineDisplay)}</dd>{(meeting.VenueOrArea is{}place?$"<dt>Public venue or general area</dt><dd>{E(place)}</dd>":"")}<dt>Last update (UTC)</dt><dd>{E(meeting.UpdatedAt.ToString("u"))}</dd></dl><p>This is a read-only page. Only the member can confirm their own safety.</p><p><a class=\"link\" href=\"/safety/contact/{E(token)}\">Manage trusted-contact consent</a></p>{checkInHelp}<p>A missed check-in does not prove an emergency. Try contacting the member directly using your usual contact details.</p><p>Swoovr does not monitor emergencies or dispatch responders. Alerts are not guaranteed to arrive; absence of an alert is not proof of safety.</p><p><a class=\"link\" href=\"/safety\">Country-specific Safety &amp; Support</a> · <a class=\"link\" href=\"https://swoovr.com/support\" rel=\"noreferrer\">Swoovr support</a></p></section>");
            }
            catch(Exception error)when(error is HttpRequestException or TaskCanceledException or JsonException){return Page(Unavailable(),404);}
        }).RequireRateLimiting("meeting-safety-pages");
        app.MapPost("/safety/meeting/request-code",async(HttpContext context,IHttpClientFactory clients)=>
        {
            Headers(context);if(!ValidForm(context))return Page(Unavailable(),400);
            var form=await context.Request.ReadFormAsync(context.RequestAborted);var link=form["link"].ToString();
            if(!Shape(link))return Page(Unavailable(),404);
            try
            {
                var client=clients.CreateClient(Bff.ApiClient);
                if(!await Available(client,link,context.RequestAborted))return Page(Unavailable(),404);
                using var response=await client.PostAsJsonAsync("api/safety/meeting-contact/code",new MeetingSafetyContactCodeRequest(link),context.RequestAborted);
                var receipt=response.IsSuccessStatusCode?await response.Content.ReadFromJsonAsync<MeetingSafetyContactCodeReceipt>(context.RequestAborted):null;
                if(receipt is null||receipt.Status=="unavailable")return Page(Unavailable(),404);
                var message=receipt.Status=="try-later"?"Please wait before requesting another code.":"A code was requested using the confirmed contact details. Acceptance and arrival are not guaranteed. If a code arrives, enter it below. It expires after five minutes.";
                return Page(CodeForm(link,message));
            }
            catch(Exception error)when(error is HttpRequestException or TaskCanceledException or JsonException){return Page(CodeForm(link,"The request could not be confirmed. Please wait before trying again."));}
        }).RequireRateLimiting("meeting-safety-pages");
        app.MapPost("/safety/meeting/verify",async(HttpContext context,IHttpClientFactory clients,IDataProtectionProvider protection,IHostEnvironment environment)=>
        {
            Headers(context);if(!ValidForm(context))return Page(Unavailable(),400);
            var form=await context.Request.ReadFormAsync(context.RequestAborted);var link=form["link"].ToString();var code=form["code"].ToString();
            if(!Shape(link))return Page(Unavailable(),404);
            try
            {
                var client=clients.CreateClient(Bff.ApiClient);
                if(!await Available(client,link,context.RequestAborted))return Page(Unavailable(),404);
                if(code.Length!=6||!code.All(char.IsAsciiDigit))return Page(CodeForm(link,"Enter the six-digit code sent to the confirmed contact details."));
                using var response=await client.PostAsJsonAsync("api/safety/meeting-contact/verify",new MeetingSafetyContactCodeCheck(link,code),context.RequestAborted);
                if((int)response.StatusCode==404)return Page(Unavailable(),404);
                var receipt=response.IsSuccessStatusCode?await response.Content.ReadFromJsonAsync<MeetingSafetyContactGrantReceipt>(context.RequestAborted):null;
                var now=DateTimeOffset.UtcNow;
                if(receipt is not{Verified:true,Grant:not null,ExpiresAt:not null}||!Shape(receipt.Grant)||receipt.ExpiresAt<=now)
                    return Page(CodeForm(link,"This code could not be verified. Check the code or request another when available."));
                var expiry=receipt.ExpiresAt.Value<now.AddHours(4)?receipt.ExpiresAt.Value:now.AddHours(4);
                var payload=JsonSerializer.Serialize(new GrantCookie(receipt.Grant,expiry));
                var protectedValue=Protector(protection,link).Protect(payload);
                context.Response.Cookies.Append(CookieName(link),protectedValue,new CookieOptions{HttpOnly=true,Secure=!environment.IsDevelopment(),SameSite=SameSiteMode.Lax,Path="/safety",Expires=expiry,MaxAge=expiry-now,IsEssential=true});
                context.Response.StatusCode=303;context.Response.Headers.Location="/safety/meeting/"+link;
                return Results.Empty;
            }
            catch(Exception error)when(error is HttpRequestException or TaskCanceledException or JsonException or CryptographicException){return Page(CodeForm(link,"Verification could not be confirmed. Please try again."));}
        }).RequireRateLimiting("meeting-safety-pages");
        app.MapGet("/safety/contact/{token}",async(string token,HttpContext context,IHttpClientFactory clients)=>
        {
            Headers(context);if(!Shape(token))return Page(Unavailable(),404);
            try
            {
                using var response=await clients.CreateClient(Bff.ApiClient).PostAsJsonAsync("api/safety/meeting-contact/status",new MeetingSafetyTokenRequest(token),context.RequestAborted);
                var state=response.IsSuccessStatusCode?await response.Content.ReadFromJsonAsync<MeetingSafetyConsentView>(context.RequestAborted):null;
                if(state is not{Available:true})return Page(Unavailable(),404);
                var buttons=state.Status=="pending"?"<button class=\"button\" name=\"action\" value=\"confirm\">I agree to be a trusted contact</button><button class=\"button button-secondary\" name=\"action\" value=\"decline\">Decline</button>":"<button class=\"button button-secondary\" name=\"action\" value=\"withdraw\">Withdraw consent / unsubscribe</button>";
                return Page($"<div class=\"page-heading\"><p>Swoovr meeting safety</p><h1>Trusted contact consent</h1></div><section class=\"card account-section stack\"><p>Only agree if you know the person who invited you and want to receive their voluntary meeting-safety updates. Details are member-provided, not independently verified. Your contact details are not used for marketing.</p><p>You may receive their preferred first name, planned meeting times and chosen public venue or general area, check-in deadlines and safety-session status. You do not receive the other participant's profile, photographs, messages or live location.</p><p>Swoovr does not monitor emergencies or dispatch responders. Alerts are not guaranteed to arrive. Agree with the member about what you should do if a check-in is missed.</p><form class=\"stack\" method=\"post\" action=\"/safety/contact/respond\"><input type=\"hidden\" name=\"token\" value=\"{E(token)}\"/><div class=\"account-actions\">{buttons}</div></form><p>You can withdraw using the unsubscribe link in your invitation. Contact <a class=\"link\" href=\"https://swoovr.com/support\" rel=\"noreferrer\">Swoovr support</a> if needed.</p></section>");
            }
            catch(Exception error)when(error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException){return Page(Unavailable(),404);}
        }).RequireRateLimiting("meeting-safety-pages");
        app.MapPost("/safety/contact/respond",async(HttpContext context,IHttpClientFactory clients)=>
        {
            Headers(context);
            if(context.Request.ContentLength is >4096||!context.Request.HasFormContentType)return Page(Unavailable(),400);
            var form=await context.Request.ReadFormAsync(context.RequestAborted);var token=form["token"].ToString();var action=form["action"].ToString();
            if(!Shape(token)||action is not("confirm" or "decline" or "withdraw"))return Page(Unavailable(),404);
            try
            {
                using var response=await clients.CreateClient(Bff.ApiClient).PostAsJsonAsync("api/safety/meeting-contact/consent",new MeetingSafetyConsentRequest(token,action),context.RequestAborted);
                var result=response.IsSuccessStatusCode?await response.Content.ReadFromJsonAsync<MeetingSafetyConsentView>(context.RequestAborted):null;
                return result is{Available:true}?Page($"<div class=\"page-heading\"><h1>Consent updated</h1></div><section class=\"card account-section\"><p data-consent-result=\"{E(result.Status)}\">{(result.Status=="confirmed"?"You agreed to receive this member's meeting-safety updates. You can withdraw using the unsubscribe link in your invitation.":"You will not receive further meeting-safety updates for this contact invitation.")}</p><p>Swoovr does not monitor emergencies or dispatch responders.</p></section>"):Page(Unavailable(),404);
            }
            catch(Exception error)when(error is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException){return Page(Unavailable(),404);}
        }).RequireRateLimiting("meeting-safety-pages");
    }
    private sealed record GrantCookie(string Grant,DateTimeOffset ExpiresAt);
    private static string CookieName(string link)=>"swoovr.safety."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(link))).ToLowerInvariant();
    private static IDataProtector Protector(IDataProtectionProvider protection,string link)=>protection.CreateProtector("Swoovr.MeetingSafety.ExternalRead.v1",CookieName(link));
    private static string? ReadGrant(HttpContext context,IDataProtectionProvider protection,string link)
    {
        if(!context.Request.Cookies.TryGetValue(CookieName(link),out var value))return null;
        try
        {
            var payload=JsonSerializer.Deserialize<GrantCookie>(Protector(protection,link).Unprotect(value));
            if(payload is not null&&Shape(payload.Grant)&&payload.ExpiresAt>DateTimeOffset.UtcNow)return payload.Grant;
        }
        catch(Exception error)when(error is CryptographicException or JsonException or FormatException){ }
        ClearGrant(context,link);return null;
    }
    private static void ClearGrant(HttpContext context,string link)=>context.Response.Cookies.Delete(CookieName(link),new CookieOptions{Path="/safety",HttpOnly=true,SameSite=SameSiteMode.Lax});
    private static async Task<bool> Available(HttpClient client,string link,CancellationToken token)
    {
        using var response=await client.PostAsJsonAsync("api/safety/meeting-contact/scope",new MeetingSafetyContactCodeRequest(link),token);
        return response.IsSuccessStatusCode&&(await response.Content.ReadFromJsonAsync<MeetingSafetyContactAccessScope>(token)) is{Available:true};
    }
    private static bool ValidForm(HttpContext context)
    {
        if(context.Request.ContentLength is >4096||!context.Request.HasFormContentType||context.Request.Headers["Sec-Fetch-Site"]=="cross-site")return false;
        var origin=context.Request.Headers.Origin.ToString();
        // Chromium sends Origin:null for same-origin forms under our no-referrer policy.
        // Accept that privacy case only with browser-provided same-origin fetch metadata.
        return origin.Length==0||origin=="null"&&context.Request.Headers["Sec-Fetch-Site"]=="same-origin"
            ||Uri.TryCreate(origin,UriKind.Absolute,out var uri)&&uri.GetLeftPart(UriPartial.Authority)==context.Request.Scheme+"://"+context.Request.Host;
    }
    private static string CodeForm(string link,string? message=null)=>$"<div class=\"page-heading\"><p>Swoovr meeting safety</p><h1>Verify contact access</h1></div><section class=\"card account-section stack\"><p>Before viewing this private meeting summary, enter a one-time code sent to the confirmed trusted-contact details. This gives read-only access and does not sign you in to Swoovr.</p>{(message is null?"":$"<p role=\"status\">{E(message)}</p>")}<form class=\"stack\" method=\"post\" action=\"/safety/meeting/request-code\"><input type=\"hidden\" name=\"link\" value=\"{E(link)}\"/><button class=\"button button-secondary\" type=\"submit\">Request access code</button></form><form class=\"stack\" method=\"post\" action=\"/safety/meeting/verify\"><input type=\"hidden\" name=\"link\" value=\"{E(link)}\"/><label for=\"meeting-access-code\">Six-digit access code</label><input id=\"meeting-access-code\" name=\"code\" inputmode=\"numeric\" autocomplete=\"one-time-code\" pattern=\"[0-9]{{6}}\" minlength=\"6\" maxlength=\"6\" required/><button class=\"button\" type=\"submit\">View meeting status</button></form><p>Codes expire after five minutes. Alerts are not guaranteed to arrive. Swoovr does not monitor emergencies or dispatch responders.</p></section>";
    private static bool Shape(string? token)=>token is{Length:43}&&token.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private static string E(string value)=>HtmlEncoder.Default.Encode(value);
    private static string Unavailable()=>"<div class=\"page-heading\"><h1>Link unavailable or expired</h1></div><section class=\"card account-section\"><p>This link is unavailable or expired. Ask the person who sent it if you need an update.</p><p>Swoovr does not monitor emergencies or dispatch responders.</p></section>";
    private static IResult Page(string body,int status=200)=>Results.Content("<!doctype html><html lang=\"en\" data-scheme=\"standard\" data-theme=\"light\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><meta name=\"robots\" content=\"noindex,nofollow,noarchive\"><title>Meeting safety — Swoovr</title><link rel=\"stylesheet\" href=\"/themes.css\"><link rel=\"stylesheet\" href=\"/app.css\"></head><body><a class=\"skip-link\" href=\"#main-content\">Skip to content</a><main id=\"main-content\" class=\"column\">"+body+"</main></body></html>","text/html; charset=utf-8",statusCode:status);
    private static string RedactedPath(HttpContext context)=>context.Request.Path.StartsWithSegments("/safety/meeting")?"/safety/meeting/[redacted]":context.Request.Path.StartsWithSegments("/safety/contact")&&context.Request.Path!="/safety/contact/respond"?"/safety/contact/[redacted]":context.Request.Path.Value??"/safety/contact/respond";
    private static void Headers(HttpContext context)
    {
        var redacted=RedactedPath(context);
        var activity=System.Diagnostics.Activity.Current;activity?.SetTag("url.path",redacted);activity?.SetTag("http.target",redacted);activity?.SetTag("url.full",redacted);activity?.SetTag("http.url",redacted);
        context.Response.Headers.CacheControl="no-store";context.Response.Headers["Referrer-Policy"]="no-referrer";context.Response.Headers["X-Robots-Tag"]="noindex, nofollow, noarchive";context.Response.Headers["Content-Security-Policy"]="default-src 'none'; style-src 'self'; font-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'";}
}
