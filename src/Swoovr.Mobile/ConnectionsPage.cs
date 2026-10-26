using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Requests and connections (D112, D141), as on the web: requests received (accept or decline) and sent (withdraw),
/// accepted connections, and the people the member blocked (unblock). The API decides every list; blocked pairs and
/// closed accounts never appear. Tapping a person opens their profile.
/// </summary>
public sealed class ConnectionsPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "ConnectionsStatus");
	private List<ConnectionModeView> modes = [];

	public ConnectionsPage()
	{
		Title = "Connections";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		status.Text = null;
		await LoadAsync();
	}

	private async Task LoadAsync()
	{
		body.Children.Clear();
		body.Children.Add(Ui.Text("Connections", "PageTitle"));
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to see your requests and connections.", id: "ConnectionsSignedOut")); return; }
		var catalogue = await Ui.Members!.ModesAsync();
		var inbox = await Ui.Members.RequestsAsync("received");
		if (catalogue.Value is null || inbox.Value is null)
		{
			body.Children.Add(Ui.Barred(catalogue.Code ?? inbox.Code, "ConnectionsBarred") ?? Ui.Text("We couldn't load your connections.", id: "ConnectionsRefused"));
			return;
		}
		modes = catalogue.Value;
		var (outbox, links, blocks) = (await Ui.Members.RequestsAsync("sent"), await Ui.Members.ConnectionsAsync(), await Ui.Members.BlocksAsync());
		body.Children.Add(Ui.Detach(status));
		body.Children.Add(Section("Requests for you", "No requests waiting.", "Received", inbox.Value.Items, request => new HorizontalStackLayout
		{
			Spacing = 8,
			Children =
			{
				Ui.Button("Accept", $"Accept_{request.Id}", () => AnswerAsync(request, "accept")),
				Ui.Button("Decline", $"Decline_{request.Id}", () => AnswerAsync(request, "decline"), primary: false),
			},
		}));
		body.Children.Add(Section("Requests you sent", "Nothing waiting for an answer.", "Sent", outbox.Value?.Items ?? [],
			request => Ui.Button("Withdraw", $"Withdraw_{request.Id}", () => AnswerAsync(request, "withdraw"), primary: false)));
		body.Children.Add(Section("Connected", "No connections yet. A connection starts when someone accepts a request.", "Connected", links.Value?.Items ?? [], null));
		var blocked = new List<View> { Ui.Text("Blocked", "SectionHeading") };
		if (blocks.Value is not { Count: > 0 }) blocked.Add(Ui.Text("You haven't blocked anyone.", "FooterClaims", "BlockedEmpty"));
		foreach (var person in blocks.Value ?? [])
		{
			blocked.Add(Ui.Text(person.DisplayName ?? "Someone", "TileName", $"Blocked_{person.Id}"));
			blocked.Add(Ui.Button("Unblock", $"Unblock_{person.Id}", async () =>
			{
				var ok = await Ui.Members.UnblockAsync(person.Id) == 204;
				await LoadAsync();
				status.Text = ok ? "Unblocked. You may see each other again." : "That didn't work. Try again.";
			}, primary: false));
		}
		body.Children.Add(Ui.Card([.. blocked]));
	}

	private View Section(string heading, string empty, string id, IReadOnlyList<ConnectionRequestView> items, Func<ConnectionRequestView, View>? actions)
	{
		var rows = new List<View> { Ui.Text(heading, "SectionHeading") };
		if (items.Count == 0) rows.Add(Ui.Text(empty, "FooterClaims", id + "Empty"));
		foreach (var request in items)
		{
			var name = Ui.Text(request.Person.DisplayName ?? "Someone", "TileName", $"{id}_{request.Id}");
			name.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Ui.PushAsync(new PersonPage(request.Person.Id))) });
			rows.Add(new HorizontalStackLayout { Spacing = 8, Children = { name, Ui.BadgeRow(request.Person.Badges, $"{id}Badges_{request.Id}"), Ui.Text(Label(request.Mode), "FooterClaims") } });
			if (actions is not null) rows.Add(actions(request));
		}
		return Ui.Card([.. rows]);
	}

	private async Task AnswerAsync(ConnectionRequestView request, string answer)
	{
		var result = await Ui.Members!.AnswerRequestAsync(request.Id, answer);
		await LoadAsync();
		status.Text = MemberText.Answered(answer, result.Ok);
	}

	private string Label(string id) => modes.FirstOrDefault(m => m.Id == id)?.Label ?? id;
}
