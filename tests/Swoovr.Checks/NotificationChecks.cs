using Swoovr.Client;
using Swoovr.Contracts;
using Swoovr.Domain;

/// <summary>
/// Notification rules (#49; policy §10): blocked, barred or deleted people never cause one; a barred recipient gets account
/// notices only; switches, muted modes and quiet hours stop pushes and emails but not the centre; messages are never
/// emailed; lock screens stay private by default and emails always do.
/// </summary>
static class NotificationChecks
{
    public static void Run()
    {
        var checks = 0;
        void Expect(bool condition, string what) { if (!condition) throw new Exception("Notification check failed: " + what); checks++; }
        NotificationFacts Facts(NotificationKind kind = NotificationKind.Request, bool active = true, bool barred = false, bool actor = true, bool allowed = true,
            bool on = true, bool muted = false, bool quiet = false, bool push = true, bool email = true) => new(kind, active, barred, actor, allowed, on, muted, quiet, push, email);
        var send = NotificationPolicy.Send;

        // Who may cause or receive one.
        Expect(NotificationPolicy.Decide(Facts()) == new NotificationDecision(true, send, send), "a request from an allowed member goes everywhere");
        Expect(NotificationPolicy.Decide(Facts(allowed: false)) == NotificationDecision.None, "a blocked, barred or deleted sender causes nothing at all");
        Expect(NotificationPolicy.Decide(Facts(active: false)) == NotificationDecision.None, "nothing reaches an account pending deletion or deleted");
        Expect(NotificationPolicy.Decide(Facts(barred: true)) == NotificationDecision.None, "a barred recipient hears about nobody");
        Expect(NotificationPolicy.Decide(Facts(NotificationKind.Account, barred: true, actor: false)).Centre, "but still gets account notices");
        Expect(NotificationPolicy.Decide(Facts(NotificationKind.Travel, actor: false)).Centre, "reminders need no other person");

        // Switches, muted modes and quiet hours stop channels, not the centre.
        Expect(NotificationPolicy.Decide(Facts(on: false)) == new NotificationDecision(true, NotificationPolicy.Off, NotificationPolicy.Off), "a kind switched off still lists");
        Expect(NotificationPolicy.Decide(Facts(muted: true)) == new NotificationDecision(true, NotificationPolicy.Off, NotificationPolicy.Off), "a muted mode still lists");
        Expect(NotificationPolicy.Decide(Facts(quiet: true)) == new NotificationDecision(true, NotificationPolicy.Quiet, NotificationPolicy.Quiet), "quiet hours hold pushes and emails");
        Expect(NotificationPolicy.Decide(Facts(push: false)).Push == NotificationPolicy.Off && NotificationPolicy.Decide(Facts(push: false)).Email == send, "push and email are separate");
        Expect(NotificationPolicy.Decide(Facts(NotificationKind.Account, actor: false, on: false, muted: true)).Push == send, "account notices can't be switched off");
        Expect(NotificationPolicy.Decide(Facts(NotificationKind.Message)).Email == NotificationPolicy.NotForKind, "messages are never emailed");
        Expect(NotificationPolicy.Decide(Facts(NotificationKind.Account, actor: false)).Email == NotificationPolicy.NotForKind, "account notices keep D135's own email");
        Expect(NotificationPolicy.Decide(Facts(NotificationKind.Connection)).Email == send && NotificationPolicy.Decide(Facts(NotificationKind.Travel, actor: false)).Email == send,
            "connections and travel reminders may be emailed");
        var defaults = new NotificationSettings();
        Expect(defaults is { Messages: true, Requests: true, Connections: true, Travel: true, Push: true, Email: true, PrivatePreviews: true, ChatPhotos: true, QuietFrom: null },
            "defaults: everything on, private previews, no quiet hours, photos accepted");
        Expect(NotificationPolicy.KindOn(new NotificationSettings { Requests = false }, NotificationKind.Request) == false
            && NotificationPolicy.KindOn(new NotificationSettings { Requests = false }, NotificationKind.Account), "per-kind switches");

        // Quiet hours, including across midnight.
        TimeOnly T(int h, int m = 0) => new(h, m);
        Expect(NotificationPolicy.InQuietHours(T(22), T(7), T(23, 30)) && NotificationPolicy.InQuietHours(T(22), T(7), T(6, 59)), "22:00–07:00 covers the night");
        Expect(!NotificationPolicy.InQuietHours(T(22), T(7), T(7)) && !NotificationPolicy.InQuietHours(T(22), T(7), T(12)), "and ends at 07:00");
        Expect(NotificationPolicy.InQuietHours(T(13), T(14), T(13, 30)) && !NotificationPolicy.InQuietHours(T(13), T(14), T(14)), "a daytime window");
        Expect(!NotificationPolicy.InQuietHours(T(9), T(9), T(9)) && !NotificationPolicy.InQuietHours(null, T(9), T(9)), "equal or missing ends mean none");
        var adelaideNight = new NotificationSettings { QuietFrom = T(22), QuietUntil = T(7), TimeZone = "Australia/Adelaide" };
        Expect(NotificationPolicy.InQuietHours(adelaideNight, new DateTimeOffset(2026, 10, 28, 13, 0, 0, TimeSpan.Zero)), "23:30 in Adelaide (UTC+10:30) is quiet");
        Expect(!NotificationPolicy.InQuietHours(adelaideNight, new DateTimeOffset(2026, 10, 28, 2, 0, 0, TimeSpan.Zero)), "12:30 in Adelaide isn't");
        Expect(NotificationPolicy.Zone("Australia/Adelaide") is not null && NotificationPolicy.Zone("Mars/Base") is null && NotificationPolicy.Zone("") is null, "time zones are checked");

        // What a lock screen and an email may say.
        var (title, body) = NotificationPolicy.Push(NotificationKind.Message, true, "Fictional Ana", "Dating", "secret text", null);
        Expect(title == "Swoovr" && body == "You have a new message.", "private previews: no name or text");
        (title, body) = NotificationPolicy.Push(NotificationKind.Request, true, "Fictional Ana", "Dating", null, null);
        Expect(!(title + body).Contains("Ana") && !(title + body).Contains("Dating"), "private previews: no name or mode on a request");
        (title, body) = NotificationPolicy.Push(NotificationKind.Message, false, "Fictional Ana", null, "Hello\nthere " + new string('x', 100), null);
        Expect(title == "Fictional Ana" && body.StartsWith("Hello there") && body.Length == 80 && body.EndsWith('…'), "full previews show the name and 80 characters on one line");
        (_, body) = NotificationPolicy.Push(NotificationKind.Request, false, "Fictional Ana", "Dating", null, null);
        Expect(body == "Fictional Ana sent you a Dating request.", "full previews may name the mode");
        (_, body) = NotificationPolicy.Push(NotificationKind.Account, false, null, null, null, null);
        Expect(body == "There's a notice about your account.", "account notices never go into detail");
        foreach (var kind in new[] { NotificationKind.Request, NotificationKind.Connection, NotificationKind.Travel })
        {
            var (subject, text) = NotificationPolicy.Email(kind);
            Expect(subject == "Something new on Swoovr" && !text.Contains("Dating") && !text.Contains("Ana"), $"the {kind} email says only what kind of thing is waiting");
        }

        // Devices.
        Expect(NotificationPolicy.ValidPlatform("ios") && NotificationPolicy.ValidPlatform("android") && !NotificationPolicy.ValidPlatform("web") && !NotificationPolicy.ValidPlatform(null), "platforms");
        Expect(NotificationPolicy.ValidToken("simulated-abc") && !NotificationPolicy.ValidToken("") && !NotificationPolicy.ValidToken("a b")
            && !NotificationPolicy.ValidToken(new string('a', 513)) && !NotificationPolicy.ValidToken("tok\u0000"), "tokens are printable and bounded");

        // Shared wording.
        var view = new NotificationSettingsView(true, true, true, true, true, true, true, "22:00", "07:00", "Australia/Adelaide", ["dating"], true, 1);
        Expect(NotificationText.Summary(view) == "Quiet 22:00–07:00 · 1 mode muted", "the settings summary");
        Expect(NotificationText.Update(view) is { QuietFrom: "22:00", MutedModes: ["dating"] }, "an update that keeps the settings");
        Expect(NotificationText.Unread(0) == "No unread notifications" && NotificationText.Unread(2) == "2 unread", "unread wording");

        Console.WriteLine($"PASS: notification rules ({checks} checks).");
    }
}
