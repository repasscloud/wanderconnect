using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Swoovr.Infrastructure.Email;

public sealed record EmailMessage(string To, string Subject, string Text);

/// <summary>Sends application email. Returns false when nothing was sent; callers record that.</summary>
public interface IEmailSender
{
    Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// Local development only: plain SMTP to the Mailpit sink, which delivers nothing. The
/// production sender is MailerSend's HTTP API (Keycloak design), not built yet.
/// </summary>
public sealed class SmtpEmailSender(string host, int port, string from, ILogger<SmtpEmailSender> log) : IEmailSender
{
    public async Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new SmtpClient(host, port) { EnableSsl = false, DeliveryMethod = SmtpDeliveryMethod.Network };
            using var mail = new MailMessage(from, message.To, message.Subject, message.Text);
            await client.SendMailAsync(mail, cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or IOException)
        {
            // No address or body in the log.
            log.LogWarning("Email not sent: {Error}", exception.GetType().Name);
            return false;
        }
    }
}

/// <summary>Used when no sender is configured: sends nothing and says so.</summary>
public sealed class UnconfiguredEmailSender : IEmailSender
{
    public Task<bool> SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.FromResult(false);
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
