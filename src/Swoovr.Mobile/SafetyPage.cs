using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Safety, the Serious Safety Incident pathway (policy §9, owner Q77, D190), as on the web: immediate safety
/// information with tap-to-call contacts for the member's country first (#103, D204), the people the member has dealt with (reportable even after a
/// block, a disconnect or their deletion), and the serious report: a category, what happened, police (with an optional
/// reference, kept encrypted), messages chosen as evidence and an optional block. The other person is never told.
/// </summary>
public sealed class SafetyPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private string? subjectType, subjectId, state;

	public SafetyPage(string? subjectType = null, string? subjectId = null, string? state = null)
	{
		(this.subjectType, this.subjectId, this.state) = (subjectType, subjectId, state);
		Title = "Safety";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync();
	}

	/// <summary>
	/// #103 (D204): the country's contacts from the API, emergency first. The member's choice is kept on this device and
	/// wins; otherwise the API decides (Cloudflare's network country when hosted, else the deployment's fallback). The
	/// last pack is kept on the device for when there's no connection, shown with when it was saved and a stale warning.
	/// </summary>
	private async Task<List<View>> SafetyCardsAsync()
	{
		var chosen = SafetyText.Manual(Preferences.Default.Get<string?>(SafetyText.ChoiceKey, null));
		SafetyPack? pack = null;
		DateTimeOffset? savedAt = null;
		if (MobileEndpoints.Api is { } api)
			try
			{
				using var http = new HttpClient { BaseAddress = api, Timeout = TimeSpan.FromSeconds(10) };
				pack = await new SafetyClient(http).PackAsync(chosen);
			}
			catch (Exception error) when (error is HttpRequestException or TaskCanceledException) { }
		if (pack is not null && pack.Resources.Count > 0)
			Preferences.Default.Set(SafetyText.OfflineCacheKey, System.Text.Json.JsonSerializer.Serialize(new OfflinePack(pack, DateTimeOffset.UtcNow)));
		else if (pack is null && Preferences.Default.Get<string?>(SafetyText.OfflineCacheKey, null) is { } saved
			&& System.Text.Json.JsonSerializer.Deserialize<OfflinePack>(saved) is { } offline && (chosen is null || offline.Pack.Country == chosen))
			(pack, savedAt) = (offline.Pack, offline.SavedAt);
		var cards = new List<View>();
		var picker = new Picker { Title = "Country", AutomationId = "SafetyCountry", ItemsSource = (pack?.Countries ?? []).Select(c => c.Name).ToList() };
		picker.SelectedIndex = pack?.Countries.ToList().FindIndex(c => c.Code == pack.Country) ?? -1;
		picker.SelectedIndexChanged += async (_, _) =>
		{
			if (pack?.Countries is { } options && picker.SelectedIndex >= 0 && options[picker.SelectedIndex].Code != pack.Country)
			{
				Preferences.Default.Set(SafetyText.ChoiceKey, options[picker.SelectedIndex].Code);
				await LoadAsync();
			}
		};
		cards.Add(Ui.Card(Ui.Text(SafetyText.Title, "SectionHeading"), picker,
			Ui.Text(SafetyText.Hint(chosen is not null ? "manual" : pack?.Source ?? "unknown"), "FooterClaims", "SafetyCountryHint")));
		var rows = new List<View>();
		if (pack is null || pack.Resources.Count == 0)
			rows.AddRange([Ui.Text("If you're in immediate danger", "SectionHeading", "SafetyDanger"), Ui.Text(SafetyText.Neutral, id: "SafetyNeutral")]);
		else
		{
			var emergency = pack.Resources.FirstOrDefault(r => r.Category == "emergency");
			rows.Add(Ui.Text(emergency?.PhoneDisplay is { } number ? $"If you're in immediate danger, call {number} now." : "If you're in immediate danger", "SectionHeading", "SafetyDanger"));
			if (savedAt is { } at)
				rows.Add(Ui.Text($"No connection: these were saved on {at.ToLocalTime():d MMM yyyy}." + (DateTimeOffset.UtcNow - at > SafetyText.OfflineFreshFor ? " " + SafetyText.StaleWarning : ""),
					"FooterClaims", "SafetyOffline"));
			if (SafetyText.Warning(pack) is { } warning) rows.Add(Ui.Text(warning, "FooterClaims", "SafetyWarning"));
			foreach (var r in pack.Resources)
			{
				rows.Add(Ui.Text($"{r.Name} ({SafetyText.Category(r.Category)}): {r.Description}", id: $"Service_{r.Phone ?? r.Name}"));
				// Deliberate taps only: nothing dials or opens by itself.
				if (r.Phone is { } phone)
					rows.Add(Ui.Button($"Call {r.PhoneDisplay ?? phone}", $"Call_{phone}", async () => { if (PhoneDialer.Default.IsSupported) PhoneDialer.Default.Open(phone); await Task.CompletedTask; },
						primary: r.Category == "emergency"));
				if (r.WebsiteUrl is { } link)
					rows.Add(Ui.Button($"Open {r.Name}", $"Open_{r.Name.Replace(' ', '_')}", () => Launcher.Default.OpenAsync(new Uri(link)), primary: false));
			}
			rows.Add(Ui.Text(SafetyText.External, "FooterClaims"));
		}
		cards.Add(Ui.Card([.. rows]));
		cards.Add(Ui.Card(Ui.Text(SafetyText.SwoovrActions, "SectionHeading"), Ui.Text(SafetyText.SwoovrHelp, "FooterClaims", "SwoovrActions")));
		return cards;
	}

	private sealed record OfflinePack(SafetyPack Pack, DateTimeOffset SavedAt);

	private async Task LoadAsync()
	{
		body.Children.Clear();
		body.Children.Add(Ui.Text("Safety", "PageTitle", "SafetyTitle"));
		foreach (var card in await SafetyCardsAsync()) body.Children.Add(card);
		// Signed out (or no account yet), the contacts above still show; reporting needs a signed-in member.
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to report a serious safety incident.", id: "SafetySignedOut")); return; }
		if (subjectType is not null && subjectId is not null) body.Children.Add(await FormAsync());
		var people = (await Ui.Members!.SafetyPeopleAsync()).Value ?? [];
		var rows = new List<View> { Ui.Text("People you've dealt with", "SectionHeading"),
			Ui.Text("You can report someone even after blocking or disconnecting, or if their account was deleted.", "FooterClaims") };
		if (people.Count == 0) rows.Add(Ui.Text("No one yet.", id: "SafetyPeopleNone"));
		foreach (var p in people)
		{
			rows.Add(Ui.Text($"{p.Name} · {MemberText.SafetyState(p.State)}", id: $"SafetyPerson_{p.SubjectId}"));
			rows.Add(Ui.Button($"Report {p.Name}", $"SafetyReport_{p.SubjectId}", async () =>
			{
				(subjectType, subjectId, state) = (p.SubjectType, p.SubjectId.ToString(), p.State);
				await LoadAsync();
			}, primary: false));
		}
		body.Children.Add(Ui.Card([.. rows]));
	}

	private async Task<View> FormAsync()
	{
		var categories = (await Ui.Members!.ReportCategoriesAsync(serious: true)).Value ?? [];
		var picker = new Picker { Title = "What happened", AutomationId = "SeriousCategory", ItemsSource = categories.Select(c => c.Label).ToList() };
		var text = new Editor { AutomationId = "SeriousText", MaxLength = 2000, AutoSize = EditorAutoSizeOption.TextChanges, Placeholder = "Tell us what happened (optional)" };
		var answers = new[] { "contacted", "will-contact", "not-now" };
		var police = new Picker { Title = "Police", AutomationId = "SeriousPolice", ItemsSource = answers.Select(MemberText.PoliceAnswer).ToList() };
		var reference = Ui.Entry("SeriousPoliceReference", "Police reference (optional)", 100);
		var block = new Switch { AutomationId = "SeriousBlock" };
		var message = Ui.Text(null, id: "SeriousStatus");
		var chosen = new HashSet<Guid>();
		var views = new List<View> { Ui.Text("Report a serious safety incident", "SectionHeading"), Ui.Text(MemberText.SeriousPromise, "FooterClaims"),
			Ui.Labelled("What happened", picker), Ui.Labelled("Tell us what happened (optional)", text), Ui.Labelled("Police", police), Ui.Labelled("Police reference (optional)", reference) };
		// A conversation the member can still read offers its messages as evidence (up to 20).
		if (subjectType == "conversation" && Guid.TryParse(subjectId, out var conversation))
			foreach (var m in ((await Ui.Members.MessagesAsync(conversation)).Value?.Items ?? []).Where(m => !m.Deleted).Reverse().TakeLast(30))
			{
				var tick = new CheckBox { AutomationId = $"Choose_{m.Id}" };
				tick.CheckedChanged += (_, e) => { if (e.Value && chosen.Count < 20) chosen.Add(m.Id); else chosen.Remove(m.Id); };
				views.Add(new HorizontalStackLayout { Spacing = 8, Children = { tick, Ui.Text($"{(m.Mine ? "You" : "Them")}: {(m.Kind == "photo" ? "[photo]" : m.Text)}") } });
			}
		if (state is not ("blocked" or "deleted")) views.Add(new HorizontalStackLayout { Spacing = 8, Children = { block, Ui.Text("Also block them") } });
		views.Add(Ui.Button("Send serious report", "SeriousSend", async () =>
		{
			if (picker.SelectedIndex < 0) { message.Text = "Choose what happened."; return; }
			var result = await Ui.Members.ReportAsync(new NewReport(subjectType!, subjectId!, categories[picker.SelectedIndex].Id, text.Text, block.IsToggled,
				police.SelectedIndex < 0 ? null : answers[police.SelectedIndex], reference.Text, chosen.Count > 0 ? [.. chosen] : null));
			if (!result.Ok) { message.Text = "That report couldn't be sent. If you're in danger, call 000."; return; }
			(subjectType, subjectId) = (null, null);
			await LoadAsync();
			body.Children.Insert(1, Ui.Card(Ui.Text("Your report was sent", "SectionHeading", "SeriousDone"), Ui.Text(MemberText.SeriousPromise)));
		}));
		views.Add(message);
		return Ui.Card([.. views]);
	}
}
