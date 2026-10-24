using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Explore (D92, Q11, D112): the four-mode control from the API's mode records, then the people the API returns for
/// that mode with their badges and chips, and a request button. Explore is local (D125): without a home city, or
/// outside every open market, the API refuses and the page says why. Travel is not a mode; it has its own tab.
/// </summary>
public sealed class ExplorePage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly HorizontalStackLayout control = new() { Spacing = 6, AutomationId = "ModeControl" };
	private readonly VerticalStackLayout results = new() { Spacing = 12, AutomationId = "ExploreResults" };
	private readonly Label status = Ui.Text(null, id: "ExploreStatus");
	private List<ConnectionModeView> modes = [];
	private string selected = "";
	private bool interested;

	public ExplorePage()
	{
		Title = "Explore";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		body.Children.Add(Ui.Text("Explore", "PageTitle"));
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to see who's on Swoovr.", id: "ExploreSignedOut")); return; }
		var (catalogue, own) = (await Ui.Members!.ModesAsync(), await Ui.Members.ProfileAsync());
		if (catalogue.Value is null || own.Value is null) { body.Children.Add(Ui.Text("We couldn't load Explore.", id: "ExploreRefused")); return; }
		(modes, interested) = (catalogue.Value, own.Value.MarketInterest);
		// The control, status and results are reused each time the tab appears, so detach them first (Ui.Detach).
		body.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = Ui.Detach(control) });
		body.Children.Add(Ui.Detach(status));
		body.Children.Add(Ui.Detach(results));
		await SelectAsync(own.Value.Modes.FirstOrDefault(m => m.Enabled)?.Id ?? modes.FirstOrDefault()?.Id ?? "");
	}

	private async Task SelectAsync(string modeId)
	{
		selected = modeId;
		control.Children.Clear();
		foreach (var mode in modes) control.Children.Add(Ui.Chip(mode.Label, mode.Id == selected, $"Mode_{mode.Id}", () => SelectAsync(mode.Id)));
		results.Children.Clear();
		status.Text = null;
		var result = await Ui.Members!.ExploreAsync(modeId);
		if (result.Code == ProblemCodes.ModeNotEnabled) { results.Children.Add(Ui.Text($"{Label(modeId)} is off for you. Turn it on in the Profile tab.", id: "ExploreOff")); return; }
		if (result.Code == ProblemCodes.HomeCityRequired) { results.Children.Add(Ui.Text("Add your home city on the Profile tab to discover people in your community.", id: "ExploreHomeCityRequired")); return; }
		if (result.Code == ProblemCodes.MarketNotOpen)
		{
			results.Children.Add(Ui.Text("Swoovr isn't available in your area yet. We're expanding to new cities.", id: "ExploreMarketNotOpen"));
			results.Children.Add(Ui.Button(interested ? "Stop telling me" : "Tell me when it opens", "MarketInterest", async () =>
			{
				if (await Ui.Members.MarketInterestAsync(!interested) == 204) interested = !interested;
				await SelectAsync(modeId);
			}, primary: !interested));
			if (interested) results.Children.Add(Ui.Text("We'll let you know when Swoovr opens near you.", id: "MarketInterestOn"));
			return;
		}
		if (result.Value?.Market is { } market) results.Children.Add(Ui.Text($"People in {market}", "FieldLabel", "ExploreMarket"));
		var people = result.Value?.People ?? [];
		if (people.Count == 0) results.Children.Add(Ui.Text($"No one is showing in {Label(modeId)} yet.", id: "ExploreEmpty"));
		foreach (var person in people)
			results.Children.Add(Ui.Card(
				Ui.Text(person.DisplayName, "TileName", $"Person_{person.Id}"),
				Ui.Text(person.Badges.Count == 0 ? null : Ui.Badges(person.Badges), "FieldLabel", $"Badges_{person.Id}"),
				Ui.Text(string.Join(" · ", person.Intentions), "FooterClaims"),
				Ui.Text(person.Area, "TileArea"),
				Ui.Button("Send request", $"Request_{person.Id}", () => RequestAsync(person), primary: false)));
	}

	private async Task RequestAsync(ProfileCard person)
	{
		var result = await Ui.Members!.SendRequestAsync(person.Id, selected);
		status.Text = result.Ok ? $"Request sent to {person.DisplayName}." : result.Code switch
		{
			ProblemCodes.RequestPending => "There's already a request between you in this mode.",
			ProblemCodes.AlreadyConnected => "You're already connected in this mode.",
			_ => "That request couldn't be sent.",
		};
	}

	private string Label(string id) => modes.FirstOrDefault(m => m.Id == id)?.Label ?? id;
}
