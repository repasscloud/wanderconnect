using Swoovr.Infrastructure.Email;

namespace Swoovr.Infrastructure.Messaging;

/// <summary>One push to one device: what the lock screen shows and where the app should open.</summary>
public sealed record PushMessage(Guid DeviceId, string Platform, string Token, string Title, string Body, string Link);

/// <summary>
/// Sends push notifications (#49). APNs and FCM need the owner's accounts (Q83); until then the only sender is the
/// labelled simulated one. Returns false when nothing was delivered; callers record that.
/// </summary>
public interface IPushSender
{
    /// <summary>"simulated" until a real provider is configured.</summary>
    string Kind { get; }
    Task<bool> SendAsync(PushMessage message, CancellationToken cancellationToken);
}

/// <summary>
/// The clearly labelled simulated push sender (Q83 default): it never claims delivery to a device. In local development it
/// writes exactly what the lock screen would show to the local mail sink (Mailpit, which delivers nothing), addressed to
/// push-&lt;device id&gt;@simulated.swoovr.invalid and marked SIMULATED, so a person or a test can read it; anywhere else
/// it sends nothing. The device token is never written out.
/// </summary>
public sealed class SimulatedPushSender(IEmailSender email, bool localSink) : IPushSender
{
    public string Kind => "simulated";

    public Task<bool> SendAsync(PushMessage message, CancellationToken cancellationToken) => !localSink
        ? Task.FromResult(false)
        : email.SendAsync(new EmailMessage(Address(message.DeviceId), "SIMULATED PUSH (not delivered)",
            $"""
            SIMULATED: no push notification was sent. This is the local development sink.

            Platform: {message.Platform}
            Title: {message.Title}
            Body: {message.Body}
            Opens: {message.Link}
            """), cancellationToken);

    public static string Address(Guid device) => $"push-{device:N}@simulated.swoovr.invalid";
}
