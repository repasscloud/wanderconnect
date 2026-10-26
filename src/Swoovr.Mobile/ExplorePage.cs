using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Explore (D92, Q11, D141): the four-mode control from the API's mode records, then the people the API returns for that
/// mode with their badge artwork, chips, age, gender and pronouns as each person allows; tapping someone opens their
/// profile. Explore is local (D125); the API also refuses a viewer without a date of birth, Dating without gender and
/// "show me" (D139, D157), and restricted, suspended or unchecked accounts (D127, D160), and the page says why in the
/// web's words (MemberText). Destination discovery (who's visiting) sits here too; Travel is not a mode.
/// </summary>
public sealed class ExplorePage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly HorizontalStackLayout control = new() { Spacing = 6, AutomationId = "ModeControl" };
	private readonly VerticalStackLayout results = new() { Spacing = 12, AutomationId = "ExploreResults" };
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
		if (catalogue.Value is null || own.Value is null)
		{
			body.Children.Add(Ui.Barred(catalogue.Code ?? own.Code, "ExploreBarred") ?? Ui.Text("We couldn't load Explore.", id: "ExploreRefused"));
			return;
		}
		(modes, interested) = (catalogue.Value, own.Value.MarketInterest);
		body.Children.Add(Ui.Button("Who's visiting", "ExploreVisitors", () => Ui.PushAsync(new VisitorsPage()), primary: false));
		// The control and results are reused each time the tab appears, so detach them first (Ui.Detach).
		body.Children.Add(new ScrollView { Orientation = ScrollOrientation.Horizontal, Content = Ui.Detach(control) });
		body.Children.Add(Ui.Detach(results));
		await SelectAsync(own.Value.Modes.FirstOrDefault(m => m.Enabled)?.Id ?? modes.FirstOrDefault()?.Id ?? "");
	}

	private async Task SelectAsync(string modeId)
	{
		selected = modeId;
		control.Children.Clear();
		foreach (var mode in modes) control.Children.Add(Ui.Chip(mode.Label, mode.Id == selected, $"Mode_{mode.Id}", () => SelectAsync(mode.Id)));
		results.Children.Clear();
		var result = await Ui.Members!.ExploreAsync(modeId);
		if (Ui.Barred(result.Code, "ExploreBarred") is { } barred) { results.Children.Add(barred); return; }
		if (MemberText.ExploreNotice(result.Code, Label(modeId)) is { } notice)
		{
			results.Children.Add(Ui.Text(notice, id: "ExploreNotice_" + result.Code));
			if (result.Code == ProblemCodes.MarketNotOpen)
			{
				results.Children.Add(Ui.Button(interested ? "Stop telling me" : "Tell me when it opens", "MarketInterest", async () =>
				{
					if (await Ui.Members.MarketInterestAsync(!interested) == 204) interested = !interested;
					await SelectAsync(modeId);
				}, primary: !interested));
				if (interested) results.Children.Add(Ui.Text("We'll let you know when Swoovr opens near you.", id: "MarketInterestOn"));
			}
			return;
		}
		if (result.Value is null) { results.Children.Add(Ui.Text("We couldn't load Explore. Try again later.", id: "ExploreRefused")); return; }
		if (result.Value.Market is { } market) results.Children.Add(Ui.Text($"People in {market}", "FieldLabel", "ExploreMarket"));
		if (result.Value.People.Count == 0) results.Children.Add(Ui.Text($"No one is showing in {Label(modeId)} yet.", id: "ExploreEmpty"));
		foreach (var person in result.Value.People) results.Children.Add(Tile(person));
	}

	private View Tile(ProfileCard person)
	{
		var details = string.Join(" · ", new[] { person.Age?.ToString(), person.Gender, person.Pronouns }.Where(x => x is not null));
		var card = Ui.Card(
			new HorizontalStackLayout { Spacing = 8, Children = { Ui.Text(person.DisplayName, "TileName", $"Person_{person.Id}"), Ui.BadgeRow(person.Badges, $"Badges_{person.Id}") } },
			Ui.Text(details.Length == 0 ? null : details, "FooterClaims", $"Details_{person.Id}"),
			Ui.Text(string.Join(" · ", person.Intentions), "FooterClaims"),
			Ui.Text(person.Area, "TileArea"),
			Ui.Button("View profile", $"Open_{person.Id}", () => Ui.PushAsync(new PersonPage(person.Id)), primary: false));
		card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await Ui.PushAsync(new PersonPage(person.Id))) });
		return card;
	}

	private string Label(string id) => modes.FirstOrDefault(m => m.Id == id)?.Label ?? id;
}
