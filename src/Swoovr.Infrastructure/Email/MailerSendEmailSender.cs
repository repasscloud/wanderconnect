using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text.Json;

namespace Swoovr.Infrastructure.Email;

/// <summary>Resolved runtime secrets only. ToString deliberately never exposes token/address.</summary>
public sealed class MailerSendOptions
{
    internal string ApiToken { get; }
    internal MailAddress From { get; }
    public TimeSpan Timeout { get; }

    public MailerSendOptions(string apiToken, string from, TimeSpan? timeout = null)
    {
        if (string.IsNullOrWhiteSpace(apiToken) || apiToken.Any(char.IsWhiteSpace) || apiToken.Any(char.IsControl))
            throw new ArgumentException("MailerSend token must be configured without whitespace/control characters.");
        if (!ValidAddress(from) || !MailAddress.TryCreate(from, out var address))
            throw new ArgumentException("MailerSend sender must be a configured mailbox.");
        Timeout = timeout ?? TimeSpan.FromSeconds(15);
        if (Timeout <= TimeSpan.Zero || Timeout > TimeSpan.FromSeconds(60))
            throw new ArgumentException("MailerSend timeout must be positive and at most 60 seconds.");
        ApiToken = apiToken;
        From = address;
    }

    public override string ToString() => "MailerSendOptions(redacted)";

    /// <summary>MailerSend-specific configuration: disabled by default; no SMTP/simulation fallback.
    /// A null result means disabled, not a configured sender. No environment/file access.</summary>
    public static MailerSendOptions? ReadConfiguration(Func<string, string?> read)
    {
        var provider = read("Email:Provider") ?? "disabled";
        if (provider == "disabled") return null;
        if (provider != "mailersend") throw new InvalidOperationException("Email:Provider must be disabled or mailersend.");
        var seconds = 15;
        if (read("Email:MailerSend:TimeoutSeconds") is { } raw && (!int.TryParse(raw, out seconds) || seconds is < 1 or > 60))
            throw new InvalidOperationException("Email:MailerSend:TimeoutSeconds must be 1–60.");
        return new MailerSendOptions(read("Email:MailerSend:ApiToken") ?? "", read("Email:From") ?? "", TimeSpan.FromSeconds(seconds));
    }

    internal static bool ValidAddress(string? address) => !string.IsNullOrWhiteSpace(address) && address.Length <= 320
        && !address.Any(char.IsControl) && MailAddress.TryCreate(address, out _);
}

/// <summary>Single-attempt HTTP adapter. Use a dedicated client with redirects/retries/loggers disabled.
/// Provider acceptance is not delivery. Uncertain sends are never declared retryable.</summary>
public sealed class MailerSendEmailSender(HttpClient http, MailerSendOptions options) : IEmailSender
{
    public const string Kind = "mailersend";
    private static readonly Uri Endpoint = new("https://api.mailersend.com/v1/email");
    private const int ResponseLimit = 8192;

    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) =>
        (await SendOutcomeAsync(message, cancellationToken)).Status == "accepted";

    public async Task<EmailOutcome> SendOutcomeAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        // Local bounded-input guard, not a claim about the provider's full message limits.
        if (!MailerSendOptions.ValidAddress(message.To) || message.Subject is null || message.Text is null
            || message.Subject.Length > 998 || message.Subject.Any(char.IsControl) || message.Text.Length > 65536)
            return new("rejected", "invalid-message");
        if (cancellationToken.IsCancellationRequested) return new("retry", "cancelled-before-send");
        var recipient = new MailAddress(message.To);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(options.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = JsonContent.Create(new
        {
            from = new { email = options.From.Address, name = options.From.DisplayName },
            to = new[] { new { email = recipient.Address } },
            subject = message.Subject,
            text = message.Text
        });
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (response.StatusCode == HttpStatusCode.Accepted)
            {
                if (response.Headers.TryGetValues("x-message-id", out var ids))
                {
                    var values = ids.Take(2).ToArray();
                    if (values.Length == 1 && values[0].Length is > 0 and <= 128
                        && values[0].All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_'))
                        return new("accepted", response.Headers.TryGetValues("x-send-paused", out var paused) && paused.Contains("true", StringComparer.OrdinalIgnoreCase) ? "provider-paused" : "provider-accepted");
                    return new("unknown", "invalid-acknowledgement");
                }
                // Documented ALL_SUPPRESSED is202 with no message-id; never call it accepted.
                if (await AllSuppressedAsync(response.Content, deadline.Token)) return new("rejected", "all-suppressed");
                return new("unknown", "missing-acknowledgement");
            }
            return response.StatusCode switch
            {
                HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    or HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.UnprocessableEntity => new("rejected", "provider-refused"),
                // Existing EmailOutcome cannot carry Retry-After: do not let an outbox
                // resend on its own shorter schedule after rate/quota limiting.
                HttpStatusCode.TooManyRequests => new("unknown", "rate-limited-review"),
                _ => new("unknown", "unconfirmed")
            };
        }
        catch (Exception error) when (error is HttpRequestException or IOException or OperationCanceledException or JsonException)
        {
            // Exception text, provider body, destination, content and token never logged/returned.
            return new("unknown", "unconfirmed");
        }
    }

    private static async Task<bool> AllSuppressedAsync(HttpContent content, CancellationToken token)
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
            && document.RootElement.TryGetProperty("warnings", out var warnings) && warnings.ValueKind == JsonValueKind.Array
            && warnings.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.Object
                && item.TryGetProperty("type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "ALL_SUPPRESSED");
    }
}
