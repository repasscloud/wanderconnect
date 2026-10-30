using Swoovr.Contracts;

namespace Swoovr.Client;

/// <summary>Notification centre and settings wording shared by the web and the app (#49), so every client says the same thing.</summary>
public static class NotificationText
{
    public const string Title = "Notifications";
    public const string PrivacyNote = "Lock screens and emails never show names, message text or which mode something is about unless you turn private previews off. Emails never do. Swoovr never shows anyone when you're online, active or have read their message.";
    public const string PreviewsHelp = "On: your lock screen says only that something is new. Off: it shows the person's name, the mode and the start of a message.";
    public const string QuietHelp = "No pushes or emails between these times (in your time zone). Everything still waits for you here.";
    public const string MutedHelp = "Nothing about a muted mode is pushed or emailed. It still shows here.";
    public const string EmailHelp = "Emails go for requests, new connections and travel reminders, never for messages, and say only that something is waiting.";
    public const string PhotosHelp = "When off, people can't send you photos in chat.";
    public const string PushSimulated = "Push notifications are simulated in this prototype: nothing reaches your lock screen yet.";
    public const string Empty = "Nothing new.";
    /// <summary>The app's install id, kept in its preferences, which stands in for a push token until APNs and FCM exist (Q83).</summary>
    public const string InstallIdKey = "swoovr.install.id";

    public static string Unread(int count) => count switch { 0 => "No unread notifications", 1 => "1 unread", _ => $"{count} unread" };

    public static string KindLabel(string kind) => kind switch
    {
        "policyChange" => "Safety policy updates",
        "message" => "Messages",
        "conversationEnded" => "Conversation ended",
        "request" => "Requests",
        "connection" => "New connections",
        "travel" => "Travel reminders",
        _ => "Your account",
    };

    /// <summary>The settings as an update that keeps everything as it is.</summary>
    public static NotificationSettingsUpdate Update(NotificationSettingsView s) => new(s.Messages, s.Requests, s.Connections, s.Travel, s.Push, s.Email,
        s.PrivatePreviews, s.QuietFrom, s.QuietUntil, s.TimeZone, [.. s.MutedModes], s.ChatPhotos);

    /// <summary>A short summary for the settings link, for example "Quiet 22:00–07:00 · 1 mode muted".</summary>
    public static string Summary(NotificationSettingsView s)
    {
        var parts = new List<string>();
        if (!s.Push && !s.Email) parts.Add("Pushes and emails off");
        if (s.QuietFrom is { } from && s.QuietUntil is { } until) parts.Add($"Quiet {from}–{until}");
        if (s.MutedModes.Count > 0) parts.Add(s.MutedModes.Count == 1 ? "1 mode muted" : $"{s.MutedModes.Count} modes muted");
        if (!s.PrivatePreviews) parts.Add("Full previews");
        return parts.Count == 0 ? "Private previews · everything on" : string.Join(" · ", parts);
    }
}
