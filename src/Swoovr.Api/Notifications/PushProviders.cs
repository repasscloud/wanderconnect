using System.Security.Cryptography;
using Swoovr.Infrastructure.Email;
using Swoovr.Infrastructure.Messaging;

namespace Swoovr.Api;

public static class PushProviders
{
    public static IPushSender Create(IConfiguration config, bool development, HttpClient http, IEmailSender email, TimeProvider clock)
    {
        var mode = config["Push:Provider"] ?? (development ? "simulated" : "disabled");
        if (mode == "disabled") return new DisabledPushSender();
        if (mode == "simulated")
        {
            if (!development) throw new InvalidOperationException("Simulated push is permitted only in Development.");
            return new SimulatedPushSender(email, true);
        }
        if (mode != "providers") throw new InvalidOperationException("Push:Provider must be disabled, simulated or providers.");
        IPushSender ios = new DisabledPushSender(), android = new DisabledPushSender();
        if (config.GetValue<bool>("Push:Apns:Enabled"))
        {
            var options = new ApnsOptions(Required(config, "Push:Apns:KeyId"), Required(config, "Push:Apns:TeamId"),
                config["Push:Apns:BundleId"] ?? "com.swoovr.app", Required(config, "Push:Apns:PrivateKeyPem"), config.GetValue<bool>("Push:Apns:Sandbox"));
            try { using var key = ECDsa.Create(); key.ImportFromPem(options.PrivateKeyPem); if (key.KeySize != 256 || key.ExportParameters(true).Curve.Oid.Value != "1.2.840.10045.3.1.7") throw new CryptographicException(); }
            catch (Exception e) when (e is CryptographicException or ArgumentException) { throw new InvalidOperationException("APNs requires a valid P-256 private key."); }
            ios = new ApnsPushSender(http, options, clock);
        }
        if (config.GetValue<bool>("Push:Fcm:Enabled"))
        {
            var options = new FcmOptions(Required(config, "Push:Fcm:ProjectId"), Required(config, "Push:Fcm:ClientEmail"),
                Required(config, "Push:Fcm:PrivateKeyId"), Required(config, "Push:Fcm:PrivateKeyPem"));
            try { using var key = RSA.Create(); key.ImportFromPem(options.PrivateKeyPem); if (key.KeySize < 2048 || key.ExportParameters(true).D is null) throw new CryptographicException(); }
            catch (Exception e) when (e is CryptographicException or ArgumentException) { throw new InvalidOperationException("FCM requires a valid RSA private key of at least 2048 bits."); }
            android = new FcmPushSender(http, options, clock);
        }
        if (ios is DisabledPushSender && android is DisabledPushSender) throw new InvalidOperationException("Push providers mode requires an explicitly enabled platform.");
        return new PlatformPushSender(ios, android);
    }
    private static string Required(IConfiguration config, string name) => !string.IsNullOrWhiteSpace(config[name]) ? config[name]! : throw new InvalidOperationException(name + " is required for its enabled push provider.");
}
