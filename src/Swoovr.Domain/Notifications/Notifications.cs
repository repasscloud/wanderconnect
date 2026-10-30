namespace Swoovr.Domain;

/// <summary>
/// What a notification is about (#49). Account notices (D135) can't be turned off; the rest follow the member's settings.
/// Message requests in Swoovr are connection requests: people can only message once connected (D143).
/// </summary>
public enum NotificationKind { Message, Request, Connection, Travel, Account, ConversationEnded, MeetingSafety }

/// <summary>
/// One item in the member's notification centre. It holds ids only (who, which mode, which conversation, request, trip or
/// notice), never message text or names: the centre's wording is made when it is read, so a later block, deletion or
/// rename is respected. Unread message notifications for one conversation collapse into one item with a count.
/// </summary>
public sealed class Notification
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public NotificationKind Kind { get; set; }
    public Guid? ActorAccountId { get; set; }
    public string? ModeId { get; set; }
    /// <summary>The conversation, request, trip or member notice it points to.</summary>
    public Guid? SubjectId { get; set; }
    public int Count { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    /// <summary>Makes a reminder happen once (for example `trip-start:{trip id}`).</summary>
    public string? DedupeKey { get; set; }
}

/// <summary>
/// What happened to one notification on one channel: sent (or simulated), held by quiet hours, switched off, nowhere to
/// send it, or failed. Ids and codes only.
/// </summary>
public sealed class NotificationDelivery
{
    public long Id { get; set; }
    public Guid NotificationId { get; set; }
    public string Channel { get; set; } = "";
    public string Status { get; set; } = "";
    public Guid? DeviceId { get; set; }
    public DateTimeOffset At { get; set; }
    public int? Attempts { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public Guid? AttemptId { get; set; }
}

/// <summary>
/// A member's notification settings (#49). Without a row the defaults apply: everything on, private previews, no quiet
/// hours, photos in chat accepted. Quiet hours are local times in the member's time zone and may cross midnight.
/// </summary>
public sealed class NotificationSettings
{
    public Guid AccountId { get; set; }
    public bool Messages { get; set; } = true;
    public bool Requests { get; set; } = true;
    public bool Connections { get; set; } = true;
    public bool Travel { get; set; } = true;
    public bool Push { get; set; } = true;
    public bool Email { get; set; } = true;
    /// <summary>Lock screens show no names, message text or mode while this is on (the default).</summary>
    public bool PrivatePreviews { get; set; } = true;
    public TimeOnly? QuietFrom { get; set; }
    public TimeOnly? QuietUntil { get; set; }
    public string TimeZone { get; set; } = NotificationPolicy.DefaultTimeZone;
    /// <summary>Policy §14: the member accepts photo attachments in chat (Q84 default on).</summary>
    public bool ChatPhotos { get; set; } = true;
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>A mode the member muted: nothing about it is pushed or emailed (it still shows in the centre).</summary>
public sealed class NotificationModeMute
{
    public Guid AccountId { get; set; }
    public string ModeId { get; set; } = "";
}

/// <summary>
/// An owned push registration. Token is an HMAC lookup, TokenCipher holds versioned authenticated encryption;
/// plaintext legacy Development rows are upgraded before the new host starts serving.
/// </summary>
public sealed class PushDevice
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public string Platform { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public Guid? InstallationId { get; set; }
    public byte[]? TokenCipher { get; set; }
    public int? TokenKeyVersion { get; set; }
    public DateTimeOffset? InvalidatedAt { get; set; }
}

/// <summary>What the API knows when something might notify a member.</summary>
/// <param name="RecipientActive">The recipient's account is active (not pending deletion, deleted or locked).</param>
/// <param name="RecipientBarred">The recipient is restricted, suspended or terminated (D160): only account notices reach them.</param>
/// <param name="HasActor">Another member caused it.</param>
/// <param name="ActorAllowed">That member is active, not barred, and neither has blocked the other.</param>
/// <param name="KindOn">The member's switch for this kind (account notices are always on).</param>
/// <param name="ModeMuted">The notification is about a mode the member muted (a message: every mode connecting the pair is muted).</param>
public sealed record NotificationFacts(NotificationKind Kind, bool RecipientActive, bool RecipientBarred, bool HasActor, bool ActorAllowed, bool KindOn,
    bool ModeMuted, bool QuietNow, bool PushOn, bool EmailOn);

/// <summary>Where a notification goes: the centre, a push, an email, and why a channel was skipped.</summary>
public sealed record NotificationDecision(bool Centre, string Push, string Email)
{
    public static readonly NotificationDecision None = new(false, NotificationPolicy.Suppressed, NotificationPolicy.Suppressed);
}

/// <summary>
/// Notification rules (#49; policy §10; D135). A blocked, restricted, suspended, terminated or deleted person never causes
/// a notification, and a barred recipient gets account notices only. The member's switches and muted modes stop pushes
/// and emails; quiet hours hold them; the in-app centre always lists what happened. Messages are never emailed. Emails
/// never carry names, message text or the mode (subject or body); lock screens don't either while private previews are
/// on (the default).
/// </summary>
public static class NotificationPolicy
{
    public const string DefaultTimeZone = "Australia/Adelaide";
    public const string Send = "send", Off = "off", Quiet = "quiet", Suppressed = "suppressed", NotForKind = "not-for-kind";
    public const int PageSize = 50, DevicesMax = 10, TokenMax = 512;
    /// <summary>How far ahead a trip-start reminder goes out (the day before, in the destination's time zone).</summary>
    public static readonly TimeSpan TravelLead = TimeSpan.FromDays(1);

    public static NotificationDecision Decide(NotificationFacts f)
    {
        if (!f.RecipientActive) return NotificationDecision.None;
        if (f.RecipientBarred && f.Kind != NotificationKind.Account) return NotificationDecision.None;
        if (f.HasActor && !f.ActorAllowed) return NotificationDecision.None;
        var wanted = f.Kind == NotificationKind.Account || f.KindOn && !f.ModeMuted;
        string Channel(bool on, bool forKind) => !forKind ? NotForKind : !wanted || !on ? Off : f.QuietNow ? Quiet : Send;
        // Account notices are emailed by D135's own notice email; messages are never emailed.
        return new(true, Channel(f.PushOn, true), Channel(f.EmailOn, f.Kind is NotificationKind.Request or NotificationKind.Connection or NotificationKind.Travel));
    }

    public static bool KindOn(NotificationSettings s, NotificationKind kind) => kind switch
    {
        NotificationKind.Message or NotificationKind.ConversationEnded => s.Messages,
        NotificationKind.Request => s.Requests,
        NotificationKind.Connection => s.Connections,
        NotificationKind.Travel => s.Travel,
        _ => true,
    };

    /// <summary>Whether a local time falls in quiet hours [from, until), which may cross midnight. Equal ends mean none.</summary>
    public static bool InQuietHours(TimeOnly? from, TimeOnly? until, TimeOnly local) =>
        from is { } f && until is { } u && f != u && (f < u ? local >= f && local < u : local >= f || local < u);

    public static bool InQuietHours(NotificationSettings s, DateTimeOffset now)
    {
        if (s.QuietFrom is null || s.QuietUntil is null) return false;
        var zone = Zone(s.TimeZone) ?? Zone(DefaultTimeZone)!;
        return InQuietHours(s.QuietFrom, s.QuietUntil, TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime));
    }

    public static TimeZoneInfo? Zone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64) return null;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException) { return null; }
    }

    /// <summary>A push device's platform: ios or android (web push isn't offered).</summary>
    public static bool ValidPlatform(string? platform) => platform is "ios" or "android";

    public static bool ValidToken(string? token) => token is { Length: > 0 and <= TokenMax } && token.All(c => c is > ' ' and < (char)127);

    /// <summary>
    /// What a lock screen shows. With private previews (the default) only a generic line; otherwise the person's name,
    /// the mode's label and, for a message, its first 80 characters.
    /// </summary>
    public static (string Title, string Body) Push(NotificationKind kind, bool privatePreviews, string? name, string? modeLabel, string? text, string? place)
    {
        if (kind == NotificationKind.MeetingSafety) return ("Swoovr", "You have a meeting safety update. Open Swoovr to check in.");
        if (kind == NotificationKind.ConversationEnded) return ("Swoovr", ConversationEnding.Notification);
        if (privatePreviews || kind == NotificationKind.Account)
            return ("Swoovr", kind switch
            {
                NotificationKind.Message => "You have a new message.",
                NotificationKind.Request => "You have a new request.",
                NotificationKind.Connection => "You have a new connection.",
                NotificationKind.Travel => "You have a travel reminder.",
                _ => "There's a notice about your account.",
            });
        var who = string.IsNullOrWhiteSpace(name) ? "Someone" : name;
        return kind switch
        {
            NotificationKind.Message => (who, text is null ? "Sent you a message." : Short(text, 80)),
            NotificationKind.Request => ("Swoovr", modeLabel is null ? $"{who} sent you a request." : $"{who} sent you a {modeLabel} request."),
            NotificationKind.Connection => ("Swoovr", modeLabel is null ? $"{who} accepted your request." : $"{who} accepted your {modeLabel} request."),
            _ => ("Swoovr", place is null ? "Your trip starts soon." : $"Your trip to {place} starts soon."),
        };
    }

    /// <summary>The email: the same subject every time, and a body that says only what kind of thing is waiting.</summary>
    public static (string Subject, string Body) Email(NotificationKind kind) => ("Something new on Swoovr", $"""
        You have {kind switch
        {
            NotificationKind.Request => "a new request",
            NotificationKind.Connection => "a new connection",
            _ => "a travel reminder",
        }} on Swoovr. Sign in to Swoovr to see it.

        You can change which notifications you get, or turn off emails, in Me → Notifications.

        — Swoovr (RePass Cloud Pty Ltd)
        """);

    private static string Short(string text, int max)
    {
        var line = text.ReplaceLineEndings(" ").Trim();
        return line.Length <= max ? line : line[..(max - 1)].TrimEnd() + "…";
    }
}
