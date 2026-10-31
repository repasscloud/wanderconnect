using Swoovr.Infrastructure.Email;
namespace Swoovr.Api;

/// <summary>Explicit provider selection shared by API and one-shot commands. No configured-provider fallback.</summary>
public static class ApplicationEmailSetup
{
    public static void AddSwoovrApplicationEmail(this IServiceCollection services,IConfiguration configuration,bool development)
    {
        // Preserve the existing local Mailpit workflow only when no provider was selected.
        var selected=configuration["Email:Provider"]??(development&&!string.IsNullOrWhiteSpace(configuration["Email:Smtp:Host"])?"smtp":"disabled");
        switch(selected)
        {
            case "disabled":services.AddSingleton<IEmailSender,UnconfiguredEmailSender>();return;
            case "smtp":
                if(!development||configuration["Email:Smtp:Host"] is not("127.0.0.1" or "localhost" or "mailpit")
                    ||configuration.GetValue("Email:Smtp:Port",0) is <1 or >65535)
                    throw new InvalidOperationException("SMTP is restricted to an explicitly configured Development Mailpit sink.");
                services.AddSwoovrEmail(configuration["Email:Smtp:Host"],configuration.GetValue<int>("Email:Smtp:Port"),configuration["Email:From"]??"Swoovr <no-reply@swoovr.local>");return;
            case "mailersend":
                var options=MailerSendOptions.ReadConfiguration(key=>configuration[key])!;
                services.AddSingleton(options);
                services.AddHttpClient<IEmailSender,MailerSendEmailSender>(http=>http.Timeout=Timeout.InfiniteTimeSpan)
                    .ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler{AllowAutoRedirect=false,MaxResponseHeadersLength=16})
                    .RemoveAllLoggers();return;
            default:throw new InvalidOperationException("Email:Provider must be disabled, smtp or mailersend.");
        }
    }
}
