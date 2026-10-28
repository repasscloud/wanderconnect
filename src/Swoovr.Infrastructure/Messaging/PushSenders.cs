using Swoovr.Infrastructure.Email;

namespace Swoovr.Infrastructure.Messaging;

/// <summary>One push to one device: what the lock screen shows and where the app should open.</summary>
public sealed record PushMessage(Guid DeviceId, string Platform, string Token, string Title, string Body, string Link, bool HighPriority = false)
{
    public Guid AttemptId { get; init; } = Guid.NewGuid();
    public override string ToString() => $"PushMessage Device={DeviceId} Platform={Platform} (content/token redacted)";
}

/// <summary>
/// Sends push notifications (#49). APNs and FCM need the owner's accounts (Q83); until then the only sender is the
/// labelled simulated one. Returns provider acceptance or a restricted failure code; this never proves device delivery.
/// </summary>
public interface IPushSender
{
    /// <summary>Selected provider family; simulation is Development only.</summary>
    string Kind { get; }
    Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken);
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

    public async Task<PushOutcome> SendAsync(PushMessage message, CancellationToken cancellationToken) => !localSink
        ? new("disabled")
        : await email.SendAsync(new EmailMessage(Address(message.DeviceId), "SIMULATED PUSH (not delivered)",
            $"""
            SIMULATED: no push notification was sent. This is the local development sink.

            Platform: {message.Platform}
            Title: {message.Title}
            Body: {message.Body}
            Opens: {message.Link}
            """), cancellationToken) ? new("simulated") : new("failed");

    public static string Address(Guid device) => $"push-{device:N}@simulated.swoovr.invalid";
}
