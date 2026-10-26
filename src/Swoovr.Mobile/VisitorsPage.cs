using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Explore → Who's visiting (Q17, D141): for locals of an open market, the travellers visiting it in one mode ("Visiting",
/// city, country and dates only), each with a request and a link to their profile. The API decides who is local and
/// applies the reciprocal rules and the member's visitor filter.
/// </summary>
public sealed class VisitorsPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "VisitorsStatus");

	public VisitorsPage()
	{
		Title = "Who's visiting";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		var heading = Ui.Text("Who's visiting", "PageTitle", "VisitorsHeading");
		body.Children.Add(heading);
		var own = await Ui.Members!.ProfileAsync();
		if (own.Value is null) { body.Children.Add(Ui.Barred(own.Code, "VisitorsBarred") ?? Ui.Text("We couldn't load this.", id: "VisitorsRefused")); return; }
		var mode = own.Value.Modes.FirstOrDefault(m => m.Enabled)?.Id;
		if (mode is null) { body.Children.Add(Ui.Text("Turn on a mode in your profile under Me to see who's visiting.", id: "VisitorsOff")); return; }
		var visitors = await Ui.Members.VisitorsAsync(mode);
		if (Ui.Barred(visitors.Code, "VisitorsBarred") is { } barred) { body.Children.Add(barred); return; }
		if (visitors.Code == ProblemCodes.NotLocal) { body.Children.Add(Ui.Text("Set a home city in a Swoovr market under Me → Your profile to see who's visiting.", id: "VisitorsNotLocal")); return; }
		heading.Text = $"Visiting {visitors.Value?.Market}";
		body.Children.Add(Ui.Detach(status));
		if (visitors.Value is not { People.Count: > 0 }) { body.Children.Add(Ui.Text("No one is visiting yet.", id: "VisitorsEmpty")); return; }
		foreach (var person in visitors.Value.People)
			body.Children.Add(Ui.Card(
				new HorizontalStackLayout { Spacing = 8, Children = { Ui.Text(person.DisplayName, "TileName", $"Visitor_{person.Id}"), Ui.BadgeRow(person.Badges, $"VisitorBadges_{person.Id}") } },
				Ui.Text($"Visiting {person.Visiting.City}, {person.Visiting.Country} · {Ui.Dates(person.Visiting.Arrival, person.Visiting.Departure)}", id: $"Visit_{person.Id}"),
				Ui.Text(string.Join(" · ", person.Intentions), "FooterClaims"),
				Ui.Button("View profile", $"VisitorOpen_{person.Id}", () => Ui.PushAsync(new PersonPage(person.Id)), primary: false),
				Ui.Button("Send request", $"VisitorRequest_{person.Id}", async () =>
				{
					var sent = await Ui.Members.SendRequestAsync(person.Id, mode, person.Visiting.TripId);
					status.Text = sent.Ok ? $"Request sent to {person.DisplayName}." : MemberText.RequestProblem(sent.Code);
				}, primary: false)));
	}
}
