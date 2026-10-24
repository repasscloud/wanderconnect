using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Explore (D92, Q11, D112): the four-mode control from the API's mode records, then the people the API returns for
/// that mode with their badges and chips, and a request button. Travel is not a mode; it has its own tab.
/// </summary>
public sealed class ExplorePage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly HorizontalStackLayout control = new() { Spacing = 6, AutomationId = "ModeControl" };
	private readonly VerticalStackLayout results = new() { Spacing = 12, AutomationId = "ExploreResults" };
	private readonly Label status = Ui.Text(null, id: "ExploreStatus");
	private List<ConnectionModeView> modes = [];
	private string selected = "";

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
		modes = catalogue.Value;
		body.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = control });
		body.Children.Add(status);
		body.Children.Add(results);
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
