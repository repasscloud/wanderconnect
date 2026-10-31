using System.Globalization;
using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Notifications (#49), as on the web: the centre (worded by the API, so blocks and restrictions remove items at
/// once) and the settings. Saved platform tokens register on load; the member explicitly enables OS notifications.
/// Default Debug simulation is labelled and never claims lock-screen delivery.
/// </summary>
public sealed class NotificationsPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "NotificationsStatus");

	public NotificationsPage()
	{
		Title = NotificationText.Title;
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RegisterAsync();
		await LoadAsync();
	}

	private static Task RegisterAsync() => PushRegistration.RegisterAsync();

	private async Task LoadAsync()
	{
		body.Children.Clear();
		body.Children.Add(Ui.Text(NotificationText.Title, "PageTitle"));
		var centre = (await Ui.Members!.NotificationsAsync()).Value ?? new([], 0);
		var rows = new List<View> { Ui.Text("Recent", "SectionHeading"), Ui.Text(NotificationText.Unread(centre.Unread), "FooterClaims", "NotificationsUnread") };
		if (centre.Items.Count == 0) rows.Add(Ui.Text(NotificationText.Empty, "FooterClaims", "NotificationsEmpty"));
		foreach (var n in centre.Items)
        {
			rows.Add(Ui.Text($"{(n.Read ? "" : "• ")}{n.Title} · {n.At.ToLocalTime():d MMM, HH:mm}", n.Read ? null : "TileName", $"Notification_{n.Id}"));
            if(n.Kind=="policyChange"&&n.Link=="/safety/advice")rows.Add(Ui.Button("Read safety advice","PolicyAdvice_"+n.Id,()=>Ui.PushAsync(new SafetyAdvicePage()),primary:false));
            if(n.Kind=="policyChange"&&n.Link=="/safety")rows.Add(Ui.Button("Safety & Support","PolicySupport_"+n.Id,()=>Ui.PushAsync(new SafetyPage()),primary:false));
            if(n.Kind=="reportOutcome")rows.Add(Ui.Button("Open Notices and reports","ReportOutcome_"+n.Id,()=>Ui.PushAsync(new NoticesPage()),primary:false));
            if(n.Link=="/me/meeting-safety")rows.Add(Ui.Button("Open meeting safety","MeetingNotification_"+n.Id,()=>Ui.PushAsync(new MeetingSafetyPage()),primary:false));
        }
		if (centre.Unread > 0) rows.Add(Ui.Button("Mark all as read", "NotificationsReadAll", async () => { await Ui.Members.ReadAllNotificationsAsync(); await LoadAsync(); }, primary: false));
		body.Children.Add(Ui.Card([.. rows]));

		var saved = await Ui.Members.NotificationSettingsAsync();
		if (saved.Value is not { } s)
		{
			body.Children.Add(Ui.Barred(saved.Code, "NotificationsBarred") ?? Ui.Text("Settings aren't available right now.", id: "NotificationsRefused"));
			return;
		}
		var modes = (await Ui.Members.ModesAsync()).Value ?? [];
		var (messages, requests, connections, travel) = (Toggle("NotifyMessages", s.Messages), Toggle("NotifyRequests", s.Requests), Toggle("NotifyConnections", s.Connections), Toggle("NotifyTravel", s.Travel));
		var (push, email, previews, photos) = (Toggle("NotifyPush", s.Push), Toggle("NotifyEmail", s.Email), Toggle("PrivatePreviews", s.PrivatePreviews), Toggle("ChatPhotos", s.ChatPhotos));
		var quiet = Toggle("QuietHours", s.QuietFrom is not null);
		var from = new TimePicker { AutomationId = "QuietFrom", Time = Parse(s.QuietFrom) ?? new TimeSpan(22, 0, 0) };
		var until = new TimePicker { AutomationId = "QuietUntil", Time = Parse(s.QuietUntil) ?? new TimeSpan(7, 0, 0) };
		var zone = Ui.Entry("TimeZone", "Time zone", 64);
		zone.Text = s.TimeZone;
		var mutes = modes.ToDictionary(m => m.Id, m => Toggle($"Mute_{m.Id}", s.MutedModes.Contains(m.Id)));
		var settings = new List<View>
		{
			Ui.Text("Settings", "SectionHeading"), Ui.Text(NotificationText.PrivacyNote, "FooterClaims", "NotificationPrivacy"),
			Ui.Text("Tell me about", "TileName"), Row(messages, "Messages"), Row(requests, "Connection requests"), Row(connections, "New connections"), Row(travel, "Travel reminders"),
			Ui.Text("How", "TileName"), Row(push, "Push notifications"), Ui.Text(PushRegistration.Status, "FooterClaims"), Ui.Button("Enable notifications on this device", "PushEnable", async () => { await PushRegistration.EnableAsync(); await LoadAsync(); }, primary: false), Row(email, "Email"),
			Ui.Text(NotificationText.EmailHelp, "FooterClaims"), Row(previews, "Private previews"), Ui.Text(NotificationText.PreviewsHelp, "FooterClaims"),
			Ui.Text("Quiet hours", "TileName"), Ui.Text(NotificationText.QuietHelp, "FooterClaims"), Row(quiet, "Use quiet hours"), Ui.Labelled("From", from), Ui.Labelled("Until", until),
			Ui.Labelled("Time zone", zone), Ui.Text("Muted modes", "TileName"), Ui.Text(NotificationText.MutedHelp, "FooterClaims"),
		};
		settings.AddRange(modes.Select(m => Row(mutes[m.Id], m.Label)));
		settings.Add(Ui.Text("Chat", "TileName"));
		settings.Add(Row(photos, "Accept photos in chat"));
		settings.Add(Ui.Text(NotificationText.PhotosHelp, "FooterClaims"));
		settings.Add(Ui.Button("Save", "NotificationsSave", async () =>
		{
			var result = await Ui.Members.SaveNotificationSettingsAsync(new(messages.IsToggled, requests.IsToggled, connections.IsToggled, travel.IsToggled, push.IsToggled,
				email.IsToggled, previews.IsToggled, quiet.IsToggled ? Text(from.Time) : null, quiet.IsToggled ? Text(until.Time) : null, zone.Text,
				mutes.Where(m => m.Value.IsToggled).Select(m => m.Key).ToList(), photos.IsToggled));
			status.Text = result.Ok ? "Saved." : result.Code == ProblemCodes.InvalidSettings ? "Check the quiet hours and the time zone." : "Those settings couldn't be saved.";
		}));
		settings.Add(Ui.Detach(status));
		body.Children.Add(Ui.Card([.. settings]));
	}

	private static TimeSpan? Parse(string? time) => TimeSpan.TryParseExact(time, @"hh\:mm", CultureInfo.InvariantCulture, out var t) ? t : null;
	private static string? Text(TimeSpan? time) => time?.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
	private static Switch Toggle(string id, bool on) => new() { AutomationId = id, IsToggled = on };
	private static View Row(Switch toggle, string label) => new HorizontalStackLayout { Spacing = 8, Children = { Ui.Detach(toggle), Ui.Text(label) } };
}
