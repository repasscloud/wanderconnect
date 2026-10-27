using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Safety, the Serious Safety Incident pathway (policy §9, owner Q77, D190), as on the web: immediate safety
/// information with tap-to-call Australian services first, the people the member has dealt with (reportable even after a
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

	private async Task LoadAsync()
	{
		body.Children.Clear();
		body.Children.Add(Ui.Text("Safety", "PageTitle", "SafetyTitle"));
		var services = new List<View> { Ui.Text(MemberText.ImmediateDanger, "SectionHeading", "SafetyDanger") };
		foreach (var s in MemberText.SafetyServices)
		{
			services.Add(Ui.Text($"{s.Name}: {s.Detail}", id: $"Service_{s.Phone ?? s.Name}"));
			if (s.Phone is { } phone)
				services.Add(Ui.Button($"Call {s.Name}", $"Call_{phone}", async () => { if (PhoneDialer.Default.IsSupported) PhoneDialer.Default.Open(phone); await Task.CompletedTask; }, primary: phone == "000"));
			else if (s.Link is { } link)
				services.Add(Ui.Button($"Open {s.Name}", $"Open_{s.Name.Replace(' ', '_')}", () => Launcher.Default.OpenAsync(new Uri(link)), primary: false));
		}
		body.Children.Add(Ui.Card([.. services]));
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
				views.Add(new HorizontalStackLayout { Spacing = 8, Children = { tick, Ui.Text($"{(m.Mine ? "You" : "Them")}: {m.Text}") } });
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
