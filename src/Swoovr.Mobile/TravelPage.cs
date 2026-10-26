using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Travel (Q17, D107, D109–D111, D141): the member's trips with visibility and cancel, and a form to add one. Who's
/// visiting the member's own city is destination discovery and sits under Explore (VisitorsPage). The API decides
/// windows, lifecycle and who is local.
/// </summary>
public sealed class TravelPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "TravelStatus");
	private readonly CitySearchView city = new("TripCity", "Destination");
	private readonly DatePicker arrival = new() { AutomationId = "Arrival", MinimumDate = DateTime.Today.AddDays(-1) };
	private readonly DatePicker departure = new() { AutomationId = "Departure", MinimumDate = DateTime.Today.AddDays(-1) };
	private readonly Switch visible = new() { IsToggled = true, AutomationId = "TripVisible" };

	public TravelPage()
	{
		Title = "Travel";
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
		body.Children.Add(Ui.Text("Travel", "PageTitle"));
		body.Children.Add(Ui.Text("Locals can see you're visiting from 14 days before you arrive. Your modes and home discovery don't change.", "Subtitle"));
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to plan trips.", id: "TravelSignedOut")); return; }
		var trips = await Ui.Members!.TripsAsync();
		if (trips.Value is null) { body.Children.Add(Ui.Barred(trips.Code, "TravelBarred") ?? Ui.Text("We couldn't load Travel.", id: "TravelRefused")); return; }
		body.Children.Add(Ui.Detach(status));
		var list = new VerticalStackLayout { Spacing = 10, AutomationId = "TripList" };
		if (trips.Value.Items.Count == 0) list.Children.Add(Ui.Text("No trips yet.", "FooterClaims"));
		foreach (var trip in trips.Value.Items) list.Children.Add(TripRow(trip));
		body.Children.Add(Ui.Card(Ui.Text("Your trips", "SectionHeading"), list));
		body.Children.Add(Ui.Card(Ui.Text("Add a trip", "SectionHeading"), city, Ui.Labelled("Arrival", arrival), Ui.Labelled("Departure", departure),
			new HorizontalStackLayout { Spacing = 8, Children = { Ui.Detach(visible), Ui.Text("Visible to locals when the trip opens") } },
			Ui.Button("Save trip", "SaveTrip", AddAsync)));
	}

	private View TripRow(TripView trip)
	{
		var row = new VerticalStackLayout { Spacing = 4, AutomationId = $"Trip_{trip.Id}" };
		row.Children.Add(Ui.Text(trip.City.Label, "TileName"));
		row.Children.Add(Ui.Text($"{Ui.Dates(trip.Arrival, trip.Departure)} · {State(trip)}", "FooterClaims", $"TripState_{trip.Id}"));
		if (!trip.ActiveMarket && trip.State is not ("expired" or "cancelled"))
			row.Children.Add(Ui.Text("Swoovr isn't open there yet, so no one can see this trip.", "FooterClaims"));
		if (trip.State is "expired" or "cancelled") return row;
		var toggle = new Switch { IsToggled = trip.Visible, AutomationId = $"TripToggle_{trip.Id}" };
		toggle.Toggled += async (_, e) =>
		{
			var result = await Ui.Members!.SaveTripAsync(trip.Id, new TripUpdate(trip.City.Id, trip.Arrival, trip.Departure, e.Value, trip.ItineraryId));
			status.Text = result.Ok ? (e.Value ? "Visible to locals once it opens." : "Hidden. The trip is kept.") : Problem(result.Code);
			await LoadAsync();
		};
		row.Children.Add(new HorizontalStackLayout { Spacing = 8, Children = { toggle, Ui.Text("Visible to locals") } });
		row.Children.Add(Ui.Button("Cancel trip", $"TripCancel_{trip.Id}", async () =>
		{
			status.Text = await Ui.Members!.CancelTripAsync(trip.Id) == 204 ? "Trip cancelled. It stays in your history." : "That didn't work.";
			await LoadAsync();
		}, primary: false));
		return row;
	}

	private async Task AddAsync()
	{
		if (city.Selected is null) { status.Text = "Pick a city from the list."; return; }
		var result = await Ui.Members!.SaveTripAsync(null, new TripUpdate(city.Selected.Id, DateOnly.FromDateTime(arrival.Date ?? DateTime.Today),
			DateOnly.FromDateTime(departure.Date ?? DateTime.Today), visible.IsToggled, null));
		status.Text = result.Ok ? "Trip saved." : Problem(result.Code);
		if (result.Ok) { city.Show(null); await LoadAsync(); status.Text = "Trip saved."; }
	}

	private static string State(TripView trip) => trip.State switch
	{
		"visible" => "Visible to locals", "expired" => "Over", "cancelled" => "Cancelled",
		_ => trip.Visible ? $"Opens {trip.OpensOn:d MMM yyyy}" : "Hidden",
	};

	private static string Problem(string? code) => code switch
	{
		ProblemCodes.InvalidTripDates => "Check the dates: departure on or after arrival, not already over, within two years.",
		ProblemCodes.TripOverlaps => "Another visible trip overlaps these dates.",
		ProblemCodes.UnknownPlace => "Pick the city from the list.",
		_ => "That trip wasn't saved. Try again.",
	};
}
