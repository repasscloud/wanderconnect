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
	// D175: the mode the profile was opened from; the API shows that mode and the modes the pair is connected in.
	private readonly string? mode;
	private string? confirmDisconnect;
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "PersonStatus");
	private PersonProfile? person;
	private bool confirmBlock, reporting;

	public PersonPage(Guid id, string? mode = null)
	{
		(this.id, this.mode) = (id, mode);
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
		var result = await Ui.Members!.PersonAsync(id, mode);
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
		// D203: the main photo, then each shown mode's own photos (never another mode's).
		var shown = new List<View>();
		if (person.Photo is { } main) shown.Add(Ui.Photo(main.Link, "PersonPhoto_main", 220));
		foreach (var mode in person.Modes) foreach (var photo in mode.Photos ?? []) shown.Add(Ui.Photo(photo.Link, $"PersonPhoto_{mode.Id}_{photo.Id}", 220));
		if (shown.Count > 0)
		{
			var row = new HorizontalStackLayout { Spacing = 8 };
			foreach (var view in shown) row.Children.Add(view);
			body.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = row, AutomationId = "PersonPhotos" });
		}
		body.Children.Add(Ui.Text(person.DisplayName, "PageTitle", "PersonName"));
		body.Children.Add(Ui.BadgeRow(person.Badges, "PersonBadges", 20, dated: true));
		if (details.Length > 0) body.Children.Add(Ui.Text(details, "FooterClaims", "PersonDetails"));
		if (person.Area is not null) body.Children.Add(Ui.Text(person.Area, "TileArea"));
		// D175: each mode's own about; one block when they're all the same.
		if (person.Modes.Select(m => m.About).Distinct().Count() > 1)
			body.Children.Add(Ui.Card([Ui.Text("About me", "SectionHeading"), .. person.Modes.Where(m => m.About is not null)
				.SelectMany(m => new View[] { Ui.Text(m.Label, "FieldLabel"), Ui.Text(m.About, id: $"PersonAbout_{m.Id}") })]));
		else if (person.About is not null) body.Children.Add(Ui.Card(Ui.Text("About me", "SectionHeading"), Ui.Text(person.About, id: "PersonAbout")));
		body.Children.Add(ConnectCard(person));
		body.Children.Add(Ui.Detach(status));
		body.Children.Add(BlockCard(person));
		body.Children.Add(reporting ? await ReportCardAsync(person) : Ui.Card(Ui.Text("Report", "SectionHeading"),
			Ui.Button($"Report {person.DisplayName}", "ReportOpen", async () => { reporting = true; await LoadAsync(); }, primary: false),
			// D190: the separate Serious Safety Incident pathway.
			Ui.Button("Report a serious safety incident", "SeriousOpen", () => Ui.PushAsync(new SafetyPage("profile", id.ToString())), primary: false)));
	}

	private View ConnectCard(PersonProfile person)
	{
		var rows = new List<View> { Ui.Text("Connect", "SectionHeading"), Ui.Text("A request goes to one mode at a time. Nothing connects until they accept.", "FooterClaims") };
		foreach (var mode in person.Modes)
		{
			rows.Add(Ui.Text(mode.Label + (mode.Intentions.Count > 0 ? ": " + string.Join(" · ", mode.Intentions) : ""), "FieldLabel", $"SharedMode_{mode.Id}"));
			rows.Add(mode.Connection switch
			{
				"connected" => ConnectedRow(mode),
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
				// D142, D149: a traveller may look before arriving but not start contact.
				_ when !mode.CanRequest && mode.ArrivalRequired => Ui.Text(MemberText.RequestProblem(ProblemCodes.ArrivalRequired), "FooterClaims", $"ArrivalRequired_{mode.Id}"),
				// D125: no remote contact; the API refuses it too.
				_ when !mode.CanRequest => Ui.Text(MemberText.RequestUnavailable, "FooterClaims", $"RequestUnavailable_{mode.Id}"),
				_ => Ui.Button("Send request", $"Request_{mode.Id}", () => SendAsync(mode.Id)),
			});
		}
		if (person.Modes.Any(m => m.Connection == "connected"))
			// D143: one conversation per connected pair, across modes.
			rows.Add(Ui.Button($"Message {person.DisplayName}", "MessagePerson", async () =>
			{
				var conversation = await Ui.Members!.StartConversationAsync(id);
				if (conversation.Value is { } open) await Ui.PushAsync(new ConversationPage(open.Id));
				else status.Text = "You can message people you're connected with.";
			}));
		return Ui.Card([.. rows]);
	}

	// D176: the mode's connection label, and disconnecting without blocking after a confirmation.
	private View ConnectedRow(SharedModeView shared)
	{
		var rows = new VerticalStackLayout { Spacing = 6, Children = { Ui.Text(MemberText.ConnectionLabel(shared.Id), id: $"Connection_{shared.Id}") } };
		if (shared.ConnectionId is not { } connection) return rows;
		if (confirmDisconnect == shared.Id)
		{
			rows.Children.Add(Ui.Text(MemberText.DisconnectExplained, "FooterClaims"));
			rows.Children.Add(Ui.Button("Disconnect", $"DisconnectConfirm_{shared.Id}", async () =>
			{
				var result = await Ui.Members!.DisconnectAsync(connection);
				confirmDisconnect = null;
				await LoadAsync();
				status.Text = result.Ok ? "Disconnected. They won't be told." : "That connection isn't available any more.";
			}));
			rows.Children.Add(Ui.Button("Keep", $"DisconnectKeep_{shared.Id}", async () => { confirmDisconnect = null; await LoadAsync(); }, primary: false));
		}
		else rows.Children.Add(Ui.Button("Disconnect", $"Disconnect_{shared.Id}", async () => { confirmDisconnect = shared.Id; await LoadAsync(); }, primary: false));
		return rows;
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
			Ui.Text("If someone is in immediate danger, contact emergency services (000 in Australia). For assault, violence, threats, stalking, coercion or child exploitation, use Report a serious safety incident.", "FooterClaims"),
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
				if (block.IsToggled) { await BlockedAsync(); await Ui.PushAsync(new ReportFollowUpPage()); return; }
				await LoadAsync();
				status.Text = ReportFollowUpText.Confirmation;
                await Ui.PushAsync(new ReportFollowUpPage());
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
