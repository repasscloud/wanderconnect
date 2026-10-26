using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Another member's profile (D92, D112, D131), as the web's person page: name with dated badge artwork, what the person
/// shows of age, gender and pronouns, about, the modes both share with the person's chips, a request per mode (send,
/// withdraw, accept, decline), block with a confirmation, and a report with an optional block. The API returns the
/// profile only where the pair may see each other; anything else reads as "not available", never who exists.
/// </summary>
public sealed class PersonPage : ContentPage
{
	private readonly Guid id;
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "PersonStatus");
	private PersonProfile? person;
	private bool confirmBlock, reporting;

	public PersonPage(Guid id)
	{
		this.id = id;
		Title = "Profile";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync();
	}

	private async Task LoadAsync()
	{
		var result = await Ui.Members!.PersonAsync(id);
		person = result.Value;
		body.Children.Clear();
		if (Ui.Barred(result.Code, "PersonBarred") is { } barred) { body.Children.Add(barred); return; }
		if (person is null)
		{
			body.Children.Add(Ui.Text("Profile not available", "PageTitle", "PersonUnavailable"));
			body.Children.Add(Ui.Text("This profile isn't showing in any mode you use."));
			body.Children.Add(Ui.Detach(status));
			return;
		}
		var details = string.Join(" · ", new[] { person.Age?.ToString(), person.Gender, person.Pronouns }.Where(x => x is not null));
		body.Children.Add(Ui.Text(person.DisplayName, "PageTitle", "PersonName"));
		body.Children.Add(Ui.BadgeRow(person.Badges, "PersonBadges", 20, dated: true));
		if (details.Length > 0) body.Children.Add(Ui.Text(details, "FooterClaims", "PersonDetails"));
		if (person.Area is not null) body.Children.Add(Ui.Text(person.Area, "TileArea"));
		if (person.About is not null) body.Children.Add(Ui.Card(Ui.Text("About me", "SectionHeading"), Ui.Text(person.About, id: "PersonAbout")));
		body.Children.Add(ConnectCard(person));
		body.Children.Add(Ui.Detach(status));
		body.Children.Add(BlockCard(person));
		body.Children.Add(reporting ? await ReportCardAsync(person) : Ui.Card(Ui.Text("Report", "SectionHeading"),
			Ui.Button($"Report {person.DisplayName}", "ReportOpen", async () => { reporting = true; await LoadAsync(); }, primary: false)));
	}

	private View ConnectCard(PersonProfile person)
	{
		var rows = new List<View> { Ui.Text("Connect", "SectionHeading"), Ui.Text("A request goes to one mode at a time. Nothing connects until they accept.", "FooterClaims") };
		foreach (var mode in person.Modes)
		{
			rows.Add(Ui.Text(mode.Label + (mode.Intentions.Count > 0 ? ": " + string.Join(" · ", mode.Intentions) : ""), "FieldLabel", $"SharedMode_{mode.Id}"));
			rows.Add(mode.Connection switch
			{
				"connected" => Ui.Text("Connected", id: $"Connection_{mode.Id}"),
				"sent" => Ui.Button("Withdraw request", $"Withdraw_{mode.Id}", () => AnswerAsync(mode.RequestId!.Value, "withdraw"), primary: false),
				"received" => new HorizontalStackLayout
				{
					Spacing = 8,
					Children =
					{
						Ui.Button("Accept", $"Accept_{mode.Id}", () => AnswerAsync(mode.RequestId!.Value, "accept")),
						Ui.Button("Decline", $"Decline_{mode.Id}", () => AnswerAsync(mode.RequestId!.Value, "decline"), primary: false),
					},
				},
				// D125 and D142: no remote contact; the API refuses it too.
				_ when !mode.CanRequest => Ui.Text(MemberText.RequestUnavailable, "FooterClaims", $"RequestUnavailable_{mode.Id}"),
				_ => Ui.Button("Send request", $"Request_{mode.Id}", () => SendAsync(mode.Id)),
			});
		}
		return Ui.Card([.. rows]);
	}

	private View BlockCard(PersonProfile person) => confirmBlock
		? Ui.Card(Ui.Text("Block", "SectionHeading"), Ui.Text(MemberText.BlockExplained),
			Ui.Button("Block", "BlockConfirm", async () =>
			{
				if (await Ui.Members!.BlockAsync(id) == 204) { await BlockedAsync(); return; }
				status.Text = "That didn't work. Try again.";
			}),
			Ui.Button("Cancel", "BlockCancel", async () => { confirmBlock = false; await LoadAsync(); }, primary: false))
		: Ui.Card(Ui.Text("Block", "SectionHeading"), Ui.Button($"Block {person.DisplayName}", "BlockOpen", async () => { confirmBlock = true; await LoadAsync(); }, primary: false));

	private async Task<View> ReportCardAsync(PersonProfile person)
	{
		var categories = (await Ui.Members!.ReportCategoriesAsync()).Value ?? [];
		var picker = new Picker { Title = "Choose a reason", AutomationId = "ReportCategory", ItemsSource = categories.Select(c => c.Label).ToList() };
		var text = new Editor { AutomationId = "ReportText", MaxLength = 2000, AutoSize = EditorAutoSizeOption.TextChanges, Placeholder = "Anything else we should know (optional)" };
		var block = new Switch { AutomationId = "ReportBlock" };
		var message = Ui.Text(null, id: "ReportStatus");
		return Ui.Card(Ui.Text("Report", "SectionHeading"), Ui.Labelled("Reason", picker), Ui.Labelled("Anything else we should know (optional)", text),
			new HorizontalStackLayout { Spacing = 8, Children = { block, Ui.Text($"Also block {person.DisplayName}") } },
			Ui.Text("If someone is in immediate danger, contact emergency services (000 in Australia).", "FooterClaims"),
			Ui.Button("Send report", "ReportSend", async () =>
			{
				if (picker.SelectedIndex < 0) { message.Text = "Choose a reason."; return; }
				var bot = person.Badges.Any(b => b.Badge == "bot");
				var result = await Ui.Members.ReportAsync(new NewReport(bot ? "bot" : "profile", id.ToString(), categories[picker.SelectedIndex].Id, text.Text, block.IsToggled));
				if (!result.Ok)
				{
					message.Text = result.Code == ProblemCodes.TooManyRequests ? "You've sent a lot of reports. Try again in a few minutes." : "That report couldn't be sent.";
					return;
				}
				reporting = false;
				if (block.IsToggled) { await BlockedAsync(); return; }
				await LoadAsync();
				status.Text = "Thanks. Your report was submitted. You can follow its status under Me → Notices and reports.";
			}),
			message);
	}

	private async Task BlockedAsync()
	{
		(person, confirmBlock, reporting) = (null, false, false);
		body.Children.Clear();
		body.Children.Add(Ui.Text("Blocked", "PageTitle", "PersonBlocked"));
		body.Children.Add(Ui.Text(MemberText.Blocked));
		await Task.CompletedTask;
	}

	private async Task SendAsync(string modeId)
	{
		var result = await Ui.Members!.SendRequestAsync(id, modeId);
		await LoadAsync();
		status.Text = result.Ok ? "Request sent." : MemberText.RequestProblem(result.Code);
	}

	private async Task AnswerAsync(Guid requestId, string answer)
	{
		var result = await Ui.Members!.AnswerRequestAsync(requestId, answer);
		await LoadAsync();
		status.Text = MemberText.Answered(answer, result.Ok);
	}
}
