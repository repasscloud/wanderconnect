using Microsoft.Extensions.Logging;
using Swoovr.Infrastructure.Email;
namespace Swoovr.Api;

/// <summary>Explicit provider selection shared by API and one-shot commands. No configured-provider fallback.
/// Email:Provider: disabled | twilio (Twilio Email API, #316) | smtp (STARTTLS relay with username/password, or the
/// Development Mailpit sink) | mailpit (the Mailpit sink, Development or UAT only) | mailersend (D39).</summary>
public static class ApplicationEmailSetup
{
    public static void AddSwoovrApplicationEmail(this IServiceCollection services,IConfiguration configuration,bool development,string? environment=null)
    {
        // Preserve the existing local Mailpit workflow only when no provider was selected.
        var selected=configuration["Email:Provider"]??(development&&!string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"])?"smtp":"disabled");
        switch(selected)
        {
            case "disabled":services.AddSingleton<IEmailSender,UnconfiguredEmailSender>();return;
            case "smtp" when !string.IsNullOrEmpty(configuration["Email:Smtp:Username"]):
                services.AddSingleton<IEmailSender>(Relay(configuration));return;
            case "smtp":
                if(!development||configuration["Email:Smtp:Host"] is not("127.0.0.1" or "localhost" or "mailpit")
                    ||configuration.GetValue("Email:Smtp:Port",0) is <1 or >65535)
                    throw new InvalidOperationException("SMTP without a username is restricted to an explicitly configured Development Mailpit sink.");
                services.AddSwoovrEmail(configuration["Email:Smtp:Host"],configuration.GetValue<int>("Email:Smtp:Port"),configuration["Email:From"]??"Swoovr <no-reply@swoovr.local>");return;
            case "mailpit":
                var sink=MailpitSink(configuration,development,environment,configuration["Email:From"]);
                services.AddSingleton<IEmailSender>(provider=>sink(provider.GetRequiredService<ILogger<SmtpEmailSender>>()));return;
            case "twilio":
                var twilio=TwilioEmailOptions.ReadConfiguration(key=>configuration[key]);
                services.AddSingleton(twilio);
                services.AddHttpClient<IEmailSender,TwilioEmailSender>(http=>http.Timeout=Timeout.InfiniteTimeSpan)
                    .ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler{AllowAutoRedirect=false,MaxResponseHeadersLength=16})
                    .RemoveAllLoggers();return;
            case "mailersend":
                var options=MailerSendOptions.ReadConfiguration(key=>configuration[key])!;
                services.AddSingleton(options);
                services.AddHttpClient<IEmailSender,MailerSendEmailSender>(http=>http.Timeout=Timeout.InfiniteTimeSpan)
                    .ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler{AllowAutoRedirect=false,MaxResponseHeadersLength=16})
                    .RemoveAllLoggers();return;
            default:throw new InvalidOperationException("Email:Provider must be disabled, twilio, smtp, mailpit or mailersend.");
        }
    }

    /// <summary>Hosted SMTP provider: STARTTLS always, explicit username and password, never a local sink host.</summary>
    static Func<IServiceProvider,IEmailSender> Relay(IConfiguration configuration)
    {
        var host=configuration["Email:Smtp:Host"];var port=configuration.GetValue("Email:Smtp:Port",0);
        var username=configuration["Email:Smtp:Username"]!;var password=configuration["Email:Smtp:Password"];var from=configuration["Email:From"];
        if(string.IsNullOrWhiteSpace(host)||host is "127.0.0.1" or "localhost" or "mailpit"||Uri.CheckHostName(host)==UriHostNameType.Unknown||port is <1 or >65535)
            throw new InvalidOperationException("Email:Smtp:Host must be the SMTP provider's host name and Email:Smtp:Port 1–65535.");
        if(string.IsNullOrEmpty(password)||username.Any(char.IsControl)||password.Any(char.IsControl))
            throw new InvalidOperationException("Email:Smtp:Username and Email:Smtp:Password must both be configured.");
        if(string.IsNullOrWhiteSpace(from)||!System.Net.Mail.MailAddress.TryCreate(from,out _))
            throw new InvalidOperationException("Email:From must be a configured mailbox.");
        var relay=new SmtpRelay(username,password);
        return provider=>new SmtpEmailSender(host,port,from,provider.GetRequiredService<ILogger<SmtpEmailSender>>(),TimeSpan.FromSeconds(30),relay);
    }

    /// <summary>The Mailpit sink (delivers nothing): Development, or UAT while a provider is being verified. Never Production.
    /// Mailpit:Host must be the sink's service name or loopback.</summary>
    public static Func<ILogger<SmtpEmailSender>,SmtpEmailSender> MailpitSink(IConfiguration configuration,bool development,string? environment,string? from)
    {
        if(!development&&!string.Equals(environment,"Uat",StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The Mailpit sink is permitted only in Development and UAT.");
        var host=configuration["Mailpit:Host"];var port=configuration.GetValue("Mailpit:Port",1025);
        if(host is not("mailpit" or "127.0.0.1" or "localhost")||port is <1 or >65535)
            throw new InvalidOperationException("Mailpit:Host must be mailpit or loopback, and Mailpit:Port 1–65535.");
        var sender=string.IsNullOrWhiteSpace(from)?"Swoovr <no-reply@swoovr.local>":from;
        return log=>new SmtpEmailSender(host,port,sender,log);
    }
}
