using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Messaging;
namespace Swoovr.Api;
/// <summary>Sms:Provider: disabled | twilio | simulated (Development, via the application email sink) |
/// mailpit (the Mailpit sink in Development or UAT while Twilio SMS is being verified; never Production).
/// Both sinks are the labelled simulated sender: nothing is texted and nothing claims delivery (D174, D321).</summary>
public static class SmsProviderSetup
{
    public static void AddSwoovrSms(this IServiceCollection services,IConfiguration configuration)
    {
        // Explicit selection: an incomplete live provider never falls back to simulation.
        var smsChoice=configuration["Sms:Provider"];
        if(smsChoice is not(null or "disabled" or "simulated" or "mailpit" or "twilio"))throw new InvalidOperationException("Sms:Provider must be disabled, simulated, mailpit or twilio.");
        if(smsChoice=="twilio")
        {
            var twilio=new TwilioOptions(configuration["Sms:Twilio:AccountSid"],configuration["Sms:Twilio:AuthToken"],configuration["Sms:Twilio:From"],new Uri(configuration["Sms:Twilio:BaseUrl"]??"https://api.twilio.com/"));
            if(!twilio.Configured)throw new InvalidOperationException("Configured Twilio requires AccountSid, AuthToken and From.");
            services.AddSingleton(twilio);
            services.AddHttpClient<IVerificationMessageSender,TwilioMessageSender>((provider,http)=>
            {
                if(!provider.GetRequiredService<IHostEnvironment>().IsDevelopment()&&!TwilioOptions.HostedEndpoint(twilio.BaseUrl))throw new InvalidOperationException("Hosted Twilio requires its HTTPS API origin.");
                http.Timeout=TimeSpan.FromSeconds(15);
            }).ConfigurePrimaryHttpMessageHandler(()=>new SocketsHttpHandler{AllowAutoRedirect=false,MaxResponseHeadersLength=16}).RemoveAllLoggers();
        }
        else if(smsChoice=="mailpit")
            // Its own sink sender, never the application email provider: SMS copies must not leave the host.
            services.AddSingleton<IVerificationMessageSender>(provider=>
            {
                var environment=provider.GetRequiredService<IHostEnvironment>();
                var sink=ApplicationEmailSetup.MailpitSink(configuration,environment.IsDevelopment(),environment.EnvironmentName,"Swoovr SMS sink <sms@simulated.swoovr.invalid>");
                return new SimulatedMessageSender(sink(provider.GetRequiredService<ILogger<SmtpEmailSender>>()),true);
            });
        else services.AddSingleton<IVerificationMessageSender>(provider=>
        {
            var development=provider.GetRequiredService<IHostEnvironment>().IsDevelopment();
            var selected=smsChoice??(development?"simulated":"disabled");
            if(selected=="disabled")return new DisabledMessageSender();
            if(!development)throw new InvalidOperationException("Simulated SMS is permitted only in Development; use mailpit on UAT.");
            return new SimulatedMessageSender(provider.GetRequiredService<IEmailSender>(),true);
        });
    }
}
