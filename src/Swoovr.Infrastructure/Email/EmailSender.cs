using System.Net.Mail;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Swoovr.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string Text);

/// <summary>Accepted means provider/sink acknowledgement, never delivery or read. Unknown must not be blindly retried.</summary>
public sealed record EmailOutcome(string Status, string? Code = null);

/// <summary>Sends application email. The compatibility bool confirms acceptance only; false can include uncertainty, never proof of non-delivery.</summary>
public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
    async Task<EmailOutcome> SendOutcomeAsync(EmailMessage message, CancellationToken cancellationToken) =>
        new(await SendAsync(message, cancellationToken) ? "accepted" : "unknown");
}

/// <summary>
/// Local development only: plain SMTP to the Mailpit sink, which delivers nothing. The
/// hosted application sender is MailerSend's HTTP API, selected separately through configuration.
/// </summary>
public sealed class SmtpEmailSender(string host, int port, string from, ILogger<SmtpEmailSender> log, TimeSpan? sendTimeout = null) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) => (await SendOutcomeAsync(message, cancellationToken)).Status == "accepted";

    public async Task<EmailOutcome> SendOutcomeAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(host, port) { EnableSsl = false, DeliveryMethod = SmtpDeliveryMethod.Network };
            using var mail = new MailMessage(from, message.To, message.Subject, message.Text);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(sendTimeout ?? TimeSpan.FromSeconds(30));
            await client.SendMailAsync(mail, deadline.Token);
            return new("accepted");
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or IOException or OperationCanceledException)
        {
            // No address or body in the log.
            log.LogWarning("Email outcome not confirmed: {Error}", exception.GetType().Name);
            if (exception is SmtpException smtp && (int)smtp.StatusCode is >= 400 and < 500) return new("retry", "smtp-transient");
            if (exception is SmtpException permanent && (int)permanent.StatusCode >= 500) return new("rejected", "smtp-rejected");
            for (Exception? inner = exception; inner is not null; inner = inner.InnerException)
                if (inner is SocketException socket && socket.SocketErrorCode == SocketError.ConnectionRefused) return new("retry", "connection-refused");
            return new("unknown", "unconfirmed");
        }
    }
}

/// <summary>Used when no sender is configured: sends nothing and says so.</summary>
public sealed class UnconfiguredEmailSender : IEmailSender
{
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<EmailOutcome> SendOutcomeAsync(EmailMessage message, CancellationToken cancellationToken) => Task.FromResult(new EmailOutcome("retry", "unconfigured"));
}

public static class EmailSetup
{
    /// <summary>
    /// SMTP only to a loopback host (the local Mailpit sink); anything else stays unconfigured
    /// so no environment can send real mail through this development sender by accident.
    /// </summary>
    public static void AddSwoovrEmail(this IServiceCollection services, string? host, int port, string from)
    {
        if (host is "127.0.0.1" or "localhost" or "mailpit" && port > 0)
            services.AddSingleton<IEmailSender>(provider => new SmtpEmailSender(host, port, from,
                provider.GetRequiredService<ILogger<SmtpEmailSender>>()));
        else
            services.AddSingleton<IEmailSender, UnconfiguredEmailSender>();
    }
}
