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
        // Host-free smoke/simulation configurations deliberately have no persistence context.
        if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString("Swoovr")))
        {
            services.AddScoped<BannedIdentifierKeys>();
            services.AddScoped<BannedIdentifierRegistry>();
            services.AddScoped<RegistryExpiry>();
            services.AddScoped<RegistryDisposal>();
            services.AddScoped<RegistryPopulation>();
            services.AddScoped<StaffConflictHistory>();
            services.AddScoped<Retention.SafetyRetention>();
            services.AddScoped<Retention.MessageRetention>();
            services.AddScoped<Retention.RetentionJobs>();
        }
        services.AddScoped<Microsoft.EntityFrameworkCore.Diagnostics.ISaveChangesInterceptor, Retention.PreservationSaveInterceptor>();
        services.AddScoped<PhoneKeys>();
        services.AddScoped<ContactHistoryKeys>();
        // D189: legal holds and the evidence store (keys from configuration, else derived from the founder secret).
        services.AddScoped<Swoovr.Api.Evidence.EvidenceKeys>();
        services.AddScoped<Swoovr.Api.Evidence.EvidencePreservation>();
        // D191: the verified identity store and its keys.
        services.AddScoped<Swoovr.Api.Verification.IdentityKeys>();
        services.AddScoped<Swoovr.Api.Verification.VerifiedIdentityStore>();
        // #89: security alerts and key rotation.
        services.AddScoped<Swoovr.Api.Security.SecurityAlerts>();
        services.AddScoped<Swoovr.Api.Security.KeyRotation>();
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
        // D210: credentials are configuration secrets, never a fallback trigger. Provider HTTP logging is removed:
        // APNs puts the token in its path; neither URI, payload nor authorization header may reach normal logs.
        services.AddHttpClient("swoovr-push", http => http.Timeout = TimeSpan.FromSeconds(15))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RemoveAllLoggers();
        services.AddSingleton<IPushSender>(provider => PushProviders.Create(configuration,
            provider.GetRequiredService<IHostEnvironment>().IsDevelopment(),
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("swoovr-push"),
            provider.GetRequiredService<Swoovr.Infrastructure.Email.IEmailSender>(), TimeProvider.System));
        services.AddScoped<Swoovr.Api.Notifications.PushKeys>();
        services.AddScoped<Swoovr.Api.Notifications.Notifier>();
        return services;
    }
}
