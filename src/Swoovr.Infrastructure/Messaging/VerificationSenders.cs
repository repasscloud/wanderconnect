using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;

namespace Swoovr.Infrastructure.Messaging;

/// <summary>Twilio Programmable Messaging settings (D153, D154): account SID, auth token and the sender number.</summary>
public sealed record TwilioOptions(string? AccountSid, string? AuthToken, string? From, Uri BaseUrl)
{
    public static bool HostedEndpoint(Uri uri)=>uri.Scheme=="https"&&uri.UserInfo.Length==0&&uri.Query.Length==0&&uri.Fragment.Length==0&&uri.AbsolutePath=="/"&&(uri.Host=="api.twilio.com"||uri.Host.StartsWith("api.",StringComparison.Ordinal)&&uri.Host.EndsWith(".twilio.com",StringComparison.Ordinal));
    public bool Configured => !string.IsNullOrWhiteSpace(AccountSid) && !string.IsNullOrWhiteSpace(AuthToken) && !string.IsNullOrWhiteSpace(From);
}

/// <summary>
/// Twilio Programmable Messaging (not Twilio Verify: Swoovr makes and checks its own codes, D153): POST
/// /2010-04-01/Accounts/{AccountSid}/Messages.json with To, From and Body, basic authentication with the account SID and
/// auth token; a valid201 message acknowledgement means provider acceptance, never recipient delivery. Only fixed status/kind is logged, never the number or the code. Used only when `Sms:Provider`
/// is "twilio" (D174: nothing is sent until the owner says so).
/// </summary>
public sealed class TwilioMessageSender(HttpClient http, TwilioOptions options, ILogger<TwilioMessageSender> log) : IVerificationMessageSender
{
    public string Kind => "twilio";

    public Task<bool> SendCodeAsync(string number, string code, CancellationToken cancellationToken) => SendTextAsync(number, Body(code), cancellationToken);

    public Task<bool> SendNoticeAsync(string number, string text, CancellationToken cancellationToken) => SendTextAsync(number, text, cancellationToken);

    private async Task<bool> SendTextAsync(string number, string body, CancellationToken cancellationToken)
    {
        if (!options.Configured) return false;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(options.BaseUrl, $"2010-04-01/Accounts/{Uri.EscapeDataString(options.AccountSid!)}/Messages.json"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = number, ["From"] = options.From!, ["Body"] = body,
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{options.AccountSid}:{options.AuthToken}")));
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(response.StatusCode!=System.Net.HttpStatusCode.Created){log.LogWarning("Twilio message returned {Status}.",(int)response.StatusCode);return false;}
            if(response.Content.Headers.ContentLength is >8192)return false;
            using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);var bytes=new byte[8193];var length=0;
            while(length<bytes.Length){var read=await stream.ReadAsync(bytes.AsMemory(length),deadline.Token);if(read==0)break;length+=read;}
            if(length==0||length>8192)return false;
            using var json=JsonDocument.Parse(bytes.AsMemory(0,length));var root=json.RootElement;
            if(root.ValueKind!=JsonValueKind.Object||!root.TryGetProperty("sid",out var id)||id.ValueKind!=JsonValueKind.String||!root.TryGetProperty("status",out var state)||state.ValueKind!=JsonValueKind.String)return false;
            var sid=id.GetString();
            return sid is{Length:34}&&(sid.StartsWith("SM",StringComparison.Ordinal)||sid.StartsWith("MM",StringComparison.Ordinal))&&sid.AsSpan(2).ToArray().All(Uri.IsHexDigit)
                &&state.GetString() is "accepted" or "queued" or "sending" or "sent" or "delivered";
        }
        catch(Exception exception)when(exception is HttpRequestException or OperationCanceledException or JsonException or IOException)
        {
            // Unknown acknowledgement is not proof that no message was created. No automatic replay here.
            log.LogWarning("Twilio message acknowledgement unavailable: {Kind}.",exception.GetType().Name);return false;
        }
    }

    public static string Body(string code) => $"Your Swoovr code is {code}. It expires in 5 minutes. Don't share it with anyone.";
}

/// <summary>
/// The clearly labelled simulated sender (D154, D174): it never claims delivery. In local development it writes the
/// message to the local mail sink (Mailpit, which delivers nothing) addressed to sms-&lt;digits&gt;@simulated.swoovr.invalid
/// and marked SIMULATED, so a person or a test can read it; anywhere else it sends nothing.
/// </summary>
public sealed class SimulatedMessageSender(IEmailSender email, bool localSink) : IVerificationMessageSender
{
    public string Kind => "simulated";

    public Task<bool> SendCodeAsync(string number, string code, CancellationToken cancellationToken) => !localSink
        ? Task.FromResult(false)
        : email.SendAsync(new EmailMessage(Address(number), "SIMULATED SMS (not delivered)",
            "SIMULATED: no text message was sent. This is the local development sink.\n\n" + TwilioMessageSender.Body(code)), cancellationToken);

    public Task<bool> SendNoticeAsync(string number, string text, CancellationToken cancellationToken) => !localSink
        ? Task.FromResult(false)
        : email.SendAsync(new EmailMessage(Address(number), "SIMULATED SMS (not delivered)",
            "SIMULATED: no text message was sent. This is the local development sink.\n\n" + text), cancellationToken);

    public static string Address(string number) => $"sms-{number.TrimStart('+')}@simulated.swoovr.invalid";
}

/// <summary>Explicitly disabled: no outbound call and no simulated/live delivery claim.</summary>
public sealed class DisabledMessageSender:IVerificationMessageSender
{
    public string Kind=>"disabled";
    public Task<bool> SendCodeAsync(string number,string code,CancellationToken cancellationToken)=>Task.FromResult(false);
    public Task<bool> SendNoticeAsync(string number,string text,CancellationToken cancellationToken)=>Task.FromResult(false);
}
