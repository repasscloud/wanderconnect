using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Explore → Traveller Search (D142), as on the web: the destination's locals in one mode, only through one of the
/// member's own trips. Opened without a trip it lists the trips whose search is open now (never a free choice of
/// market). Before arrival is confirmed the traveller may look and open profiles but not send requests (D149).
/// </summary>
public sealed class TravellerSearchPage : ContentPage
{
	private readonly Guid? tripId;
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly HorizontalStackLayout control = new() { Spacing = 6, AutomationId = "SearchModeControl" };
	private readonly VerticalStackLayout results = new() { Spacing = 12, AutomationId = "SearchResults" };
	private List<ConnectionModeView> modes = [];

	public TravellerSearchPage(Guid? tripId)
	{
		this.tripId = tripId;
		Title = "Traveller Search";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		body.Children.Add(Ui.Text("Traveller Search", "PageTitle", "SearchHeading"));
		var (catalogue, own) = (await Ui.Members!.ModesAsync(), await Ui.Members.ProfileAsync());
		if (catalogue.Value is null || own.Value is null) { body.Children.Add(Ui.Barred(catalogue.Code ?? own.Code, "SearchBarred") ?? Ui.Text("We couldn't load this.", id: "SearchRefused")); return; }
		modes = catalogue.Value;
		if (tripId is not { } id) { await TripsAsync(); return; }
		body.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = Ui.Detach(control) });
		body.Children.Add(Ui.Detach(results));
		await SelectAsync(id, own.Value.Modes.FirstOrDefault(m => m.Enabled)?.Id ?? modes.FirstOrDefault()?.Id ?? "");
	}

	private async Task TripsAsync()
	{
		var trips = await Ui.Members!.TripsAsync();
		if (trips.Value is null) { body.Children.Add(Ui.Barred(trips.Code, "SearchBarred") ?? Ui.Text("We couldn't load your trips.", id: "SearchRefused")); return; }
		var open = trips.Value.Items.Where(t => t.SearchOpen).ToList();
		body.Children.Add(Ui.Text("Search a destination through one of your trips. It opens 14 days before you arrive, while the trip is visible to locals.", "Subtitle"));
		if (open.Count == 0) body.Children.Add(Ui.Text("None of your trips can search yet. Plan one under Me → Travel.", id: "SearchNoTrips"));
		foreach (var trip in open)
			body.Children.Add(Ui.Button($"{trip.City.Label} · {Ui.Dates(trip.Arrival, trip.Departure)}", $"SearchTrip_{trip.Id}", () => Ui.PushAsync(new TravellerSearchPage(trip.Id)), primary: false));
	}

	private async Task SelectAsync(Guid trip, string modeId)
	{
		control.Children.Clear();
		foreach (var mode in modes) control.Children.Add(Ui.Chip(mode.Label, mode.Id == modeId, $"SearchMode_{mode.Id}", () => SelectAsync(trip, mode.Id)));
		results.Children.Clear();
		var result = await Ui.Members!.TravellerSearchAsync(trip, modeId);
		if (Ui.Barred(result.Code, "SearchBarred") is { } barred) { results.Children.Add(barred); return; }
		if (result.Value is not { } found) { results.Children.Add(Ui.Text(MemberText.SearchProblem(result.Code), id: "SearchNotOpen_" + result.Code)); return; }
		results.Children.Add(Ui.Text($"People in {found.Market}", "FieldLabel", "SearchMarket"));
		results.Children.Add(Ui.Text(found.CanRequest ? MemberText.Arrived : MemberText.BrowsingBeforeArrival, "FooterClaims", found.CanRequest ? "SearchArrived" : "SearchBeforeArrival"));
		if (found.People.Count == 0) results.Children.Add(Ui.Text("No one is showing here in this mode yet.", id: "SearchEmpty"));
		foreach (var person in found.People)
		{
			var details = string.Join(" · ", new[] { person.Age?.ToString(), person.Gender, person.Pronouns }.Where(x => x is not null));
			results.Children.Add(Ui.Card(
				new HorizontalStackLayout { Spacing = 8, Children = { Ui.Text(person.DisplayName, "TileName", $"SearchPerson_{person.Id}"), Ui.BadgeRow(person.Badges, $"SearchBadges_{person.Id}") } },
				Ui.Text(details.Length == 0 ? null : details, "FooterClaims"),
				Ui.Text(string.Join(" · ", person.Intentions), "FooterClaims"),
				Ui.Button("View profile", $"SearchOpen_{person.Id}", () => Ui.PushAsync(new PersonPage(person.Id)), primary: false)));
		}
	}
}
