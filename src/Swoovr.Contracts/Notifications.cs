namespace Swoovr.Contracts;

/// <summary>
/// One item in the notification centre (#49), worded for the signed-in member: Title names the person and mode where it
/// helps (the member is signed in; lock screens and emails never do by default). Link is the member page it opens.
/// </summary>
public sealed record NotificationView(Guid Id, string Kind, string Title, string Link, int Count, DateTimeOffset At, bool Read);

public sealed record NotificationCentre(IReadOnlyList<NotificationView> Items, int Unread);

/// <summary>
/// The member's notification settings (#49): kinds on or off, push and email, private previews (on by default), quiet
/// hours as local "HH:mm" times in TimeZone (both or neither), modes muted, and whether photos in chat are accepted.
/// </summary>
public sealed record NotificationSettingsView(bool Messages, bool Requests, bool Connections, bool Travel, bool Push, bool Email, bool PrivatePreviews,
    string? QuietFrom, string? QuietUntil, string TimeZone, IReadOnlyList<string> MutedModes, bool ChatPhotos, int Devices);

/// <summary>A full replacement of the settings; omitted switches keep their defaults.</summary>
public sealed record NotificationSettingsUpdate(bool? Messages, bool? Requests, bool? Connections, bool? Travel, bool? Push, bool? Email, bool? PrivatePreviews,
    string? QuietFrom, string? QuietUntil, string? TimeZone, List<string>? MutedModes, bool? ChatPhotos);

/// <summary>A device for push notifications: platform "ios" or "android" and the provider's token (simulated until Q83).</summary>
public sealed record PushDeviceRegistration(string? Platform, string? Token);
public sealed record PushDeviceView(Guid Id, string Platform, DateTimeOffset CreatedAt, bool Simulated);
