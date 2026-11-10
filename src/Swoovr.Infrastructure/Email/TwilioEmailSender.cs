using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;

namespace Swoovr.Infrastructure.Email;

/// <summary>Twilio Email API settings (owner, #316). Resolved runtime secrets only; ToString never exposes them.</summary>
public sealed class TwilioEmailOptions
{
    internal string KeySid { get; }
    internal string KeySecret { get; }
    internal MailAddress From { get; }
    /// <summary>Twilio requires a sender name; an address without one is sent as "Swoovr".</summary>
    public string FromName => string.IsNullOrWhiteSpace(From.DisplayName) ? "Swoovr" : From.DisplayName;
    public TimeSpan Timeout { get; }

    public TwilioEmailOptions(string keySid, string keySecret, string from, TimeSpan? timeout = null)
    {
        static bool Credential(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256
            && !value.Any(char.IsWhiteSpace) && !value.Any(char.IsControl) && !value.Contains(':');
        if (!Credential(keySid) || !Credential(keySecret))
            throw new ArgumentException("Twilio email key SID and secret must be configured without whitespace, control characters or ':'.");
        if (!MailerSendOptions.ValidAddress(from) || !MailAddress.TryCreate(from, out var address))
            throw new ArgumentException("Twilio email sender must be a configured mailbox.");
        Timeout = timeout ?? TimeSpan.FromSeconds(15);
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromSeconds(60))
            throw new ArgumentException("Twilio email timeout must be positive and at most 60 seconds.");
        KeySid = keySid;
        KeySecret = keySecret;
        From = address;
    }

    public override string ToString() => "TwilioEmailOptions(redacted)";

    /// <summary>No environment/file access; the caller has already selected Email:Provider=twilio.</summary>
    public static TwilioEmailOptions ReadConfiguration(Func<string, string?> read)
    {
        var seconds = 15;
        if (read("Email:Twilio:TimeoutSeconds") is { } raw && (!int.TryParse(raw, out seconds) || seconds is < 1 or > 60))
            throw new InvalidOperationException("Email:Twilio:TimeoutSeconds must be 1–60.");
        return new TwilioEmailOptions(read("Email:Twilio:KeySid") ?? "", read("Email:Twilio:KeySecret") ?? "",
            read("Email:From") ?? "", TimeSpan.FromSeconds(seconds));
    }
}

/// <summary>
/// Twilio Email API: POST https://comms.twilio.com/v1/Emails with HTTP Basic (key SID and secret). Twilio processes
/// asynchronously and answers 202 with an operationId: that is provider acceptance, never delivery. Single attempt;
/// use a dedicated client with redirects/retries/loggers disabled. Swoovr's messages are plain text, so the required
/// HTML part is the escaped text.
/// </summary>
public sealed class TwilioEmailSender(HttpClient http, TwilioEmailOptions options) : IEmailSender
{
    public const string Kind = "twilio";
    internal static readonly Uri Endpoint = new("https://comms.twilio.com/v1/Emails");
    private const int ResponseLimit = 8192;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        (await SendOutcomeAsync(message, cancellationToken)).Status == "accepted";

    public async Task<EmailOutcome> SendOutcomeAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (!MailerSendOptions.ValidAddress(message.To) || message.Subject is null || message.Text is null
            || message.Subject.Length > 998 || message.Subject.Any(char.IsControl) || message.Text.Length > 65536)
            return new("rejected", "invalid-message");
        if (cancellationToken.IsCancellationRequested) return new("retry", "cancelled-before-send");
        var recipient = new MailAddress(message.To);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(System.Text.Encoding.ASCII.GetBytes(options.KeySid + ":" + options.KeySecret)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new
        {
            from = new { address = options.From.Address, name = options.FromName },
            to = new[] { new { address = recipient.Address } },
            content = new { subject = message.Subject, text = message.Text, html = Html(message.Text) }
        });
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode == HttpStatusCode.Accepted)
                return await OperationIdAsync(response.Content, deadline.Token) ? new("accepted", "provider-accepted") : new("unknown", "missing-acknowledgement");
            return response.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden or HttpStatusCode.NotFound
                    or HttpStatusCode.MethodNotAllowed or HttpStatusCode.RequestEntityTooLarge or HttpStatusCode.UnprocessableEntity => new("rejected", "provider-refused"),
                // Twilio asks callers to honour Retry-After; EmailOutcome cannot carry it, so no blind outbox resend.
                HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable => new("unknown", "rate-limited-review"),
                _ => new("unknown", "unconfirmed")
            };
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
        {
            // Exception text, provider body, destination, content and credentials are never logged or returned.
            return new("unknown", "unconfirmed");
        }
    }

    /// <summary>Plain text as minimal HTML: escaped, paragraphs on blank lines, line breaks kept.</summary>
    internal static string Html(string text)
    {
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n")
            .Select(part => "<p>" + WebUtility.HtmlEncode(part).Replace("\n", "<br>") + "</p>");
        return "<!doctype html><html><body>" + string.Concat(paragraphs) + "</body></html>";
    }

    private static async Task<bool> OperationIdAsync(HttpContent content, CancellationToken token)
    {
        if (content.Headers.ContentLength > ResponseLimit) return false;
        await using var stream = await content.ReadAsStreamAsync(token);
        var bytes = new byte[ResponseLimit + 1];
        var count = 0;
        while (count < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(count, bytes.Length - count), token);
            if (read == 0) break;
            count += read;
        }
        if (count == 0 || count > ResponseLimit) return false;
        using var document = JsonDocument.Parse(bytes.AsMemory(0, count));
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("operationId", out var id) && id.ValueKind == JsonValueKind.String
            && id.GetString() is { Length: > 0 and <= 128 } value && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    }
}
