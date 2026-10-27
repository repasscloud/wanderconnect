using Swoovr.Api.Accounts;
using Swoovr.Api.Auth;
using Swoovr.Domain;
using Swoovr.Infrastructure.Identity;
using Swoovr.Infrastructure.Messaging;
using Swoovr.Infrastructure.Verification;

namespace Swoovr.Api;

/// <summary>
/// Outside services shared by the host and the operator commands: Didit (D147) and the members realm's
/// service-account client for account deletion (D169), both from configuration only (locally .local/docker.env).
/// </summary>
public static class ProviderSetup
{
    public static IServiceCollection AddSwoovrProviders(this IServiceCollection services, IConfiguration configuration)
    {
        var didit = new DiditOptions(configuration["Didit:ApiKey"], configuration["Didit:WorkflowId"],
            configuration["Didit:WebhookSecret"], new Uri(configuration["Didit:BaseUrl"] ?? "https://verification.didit.me/"));
        services.AddSingleton(didit);
        services.AddHttpClient<IIdVerificationProvider, DiditClient>(http => http.Timeout = TimeSpan.FromSeconds(15));
        var keycloak = configuration.GetSection("Keycloak").Get<KeycloakOptions>() ?? new KeycloakOptions();
        services.AddSingleton(new IdentityAdminOptions(keycloak.BaseUrl, keycloak.MembersRealm,
            configuration["MemberAdmin:ClientId"] ?? "swoovr-api-accounts", configuration["MemberAdmin:ClientSecret"]));
        services.AddHttpClient<IIdentityAdmin, KeycloakIdentityAdmin>(http => http.Timeout = TimeSpan.FromSeconds(15));
        services.AddScoped<AccountDeletion>();
        services.AddScoped<PhoneKeys>();
        services.AddScoped<ContactHistoryKeys>();
        // D189: legal holds and the evidence store (keys from configuration, else derived from the founder secret).
        services.AddScoped<Swoovr.Api.Evidence.EvidenceKeys>();
        services.AddScoped<Swoovr.Api.Evidence.EvidencePreservation>();
        // D153, D174: Twilio only when Sms:Provider is "twilio"; otherwise the labelled simulated sender, which writes to
        // the local mail sink in Development and sends nothing anywhere else.
        var twilio = new TwilioOptions(configuration["Sms:Twilio:AccountSid"], configuration["Sms:Twilio:AuthToken"], configuration["Sms:Twilio:From"],
            new Uri(configuration["Sms:Twilio:BaseUrl"] ?? "https://api.twilio.com/"));
        if (configuration["Sms:Provider"] == "twilio" && twilio.Configured)
        {
            services.AddSingleton(twilio);
            services.AddHttpClient<IVerificationMessageSender, TwilioMessageSender>(http => http.Timeout = TimeSpan.FromSeconds(15));
        }
        else
            services.AddSingleton<IVerificationMessageSender>(provider => new SimulatedMessageSender(provider.GetRequiredService<Swoovr.Infrastructure.Email.IEmailSender>(),
                provider.GetRequiredService<IHostEnvironment>().IsDevelopment()));
        return services;
    }
}
