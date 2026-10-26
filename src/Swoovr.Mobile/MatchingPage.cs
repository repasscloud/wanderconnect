using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Who I'd like to meet (D137–D140, D161), as on the web: gender, pronouns and orientation (each optional;
/// orientation never decides who anyone sees), what shows, Dating's reciprocal "show me" ("Prefer not to say" is never
/// offered there), and each mode's filters with the Plus ones only when the entitlement service says Plus. The API
/// checks every value again.
/// </summary>
public sealed class MatchingPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private ProfileOptionsView? options;
	private List<ConnectionModeView> modes = [];
	private string modeId = "dating";

	public MatchingPage()
	{
		Title = "Who I'd like to meet";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		body.Children.Add(Ui.Text("Who I'd like to meet", "PageTitle"));
		var result = await Ui.Members!.ProfileOptionsAsync();
		if ((options = result.Value) is null) { body.Children.Add(Ui.Barred(result.Code, "MatchingBarred") ?? Ui.Text("This page opens once your email is on the private beta list.", id: "MatchingRefused")); return; }
		modes = (await Ui.Members.ModesAsync()).Value ?? [];
		body.Children.Add(await IdentityCardAsync());
		var filters = new VerticalStackLayout { Spacing = 8, AutomationId = "Filters" };
		var picker = new Picker { AutomationId = "FilterMode", ItemsSource = modes.Select(m => m.Label).ToList(), SelectedIndex = Math.Max(0, modes.FindIndex(m => m.Id == modeId)) };
		picker.SelectedIndexChanged += async (_, _) => { modeId = modes[picker.SelectedIndex].Id; await FiltersAsync(filters); };
		body.Children.Add(Ui.Card(Ui.Text("Filters", "SectionHeading"), Ui.Labelled("Mode", picker), filters));
		await FiltersAsync(filters);
	}

	private async Task<View> IdentityCardAsync()
	{
		var mine = (await Ui.Members!.IdentityAsync()).Value;
		var genders = options!.Genders;
		var gender = new Picker { AutomationId = "Gender", Title = "Not set", ItemsSource = genders.Select(g => g.Label).Prepend("Not set").ToList() };
		gender.SelectedIndex = mine?.Gender is { } g ? genders.ToList().FindIndex(x => x.Id == g) + 1 : 0;
		var description = Ui.Entry("GenderDescription", "Describe it (optional)", 40);
		description.Text = mine?.GenderDescription;
		var pronouns = Ui.Entry("Pronouns", "Pronouns (optional)", 40);
		pronouns.Text = mine?.Pronouns;
		var orientations = options.Orientations;
		var orientation = new Picker { AutomationId = "Orientation", ItemsSource = orientations.Select(o => o.Label).Prepend("Not set").ToList() };
		orientation.SelectedIndex = mine?.Orientation is { } o ? orientations.ToList().FindIndex(x => x.Id == o) + 1 : 0;
		var (showGender, showOrientation, showAge) = (Toggle("ShowGender", mine?.ShowGender ?? true), Toggle("ShowOrientation", mine?.ShowOrientation ?? false), Toggle("ShowAge", mine?.ShowAge ?? true));
		var showMe = new Dictionary<string, Switch>();
		var showMeRows = new VerticalStackLayout { Spacing = 4 };
		// D161: "Prefer not to say" is a privacy choice, not a gender, so it's never a "show me" option.
		foreach (var option in genders.Where(x => x.Id != "unsaid"))
		{
			showMe[option.Id] = Toggle($"ShowMe_{option.Id}", mine?.DatingShowMe.Contains(option.Id) == true);
			showMeRows.Children.Add(Row(showMe[option.Id], option.Label));
		}
		var message = Ui.Text(null, id: "IdentityStatus");
		return Ui.Card(Ui.Text("About you", "SectionHeading"), Ui.Labelled("Gender", gender), Ui.Labelled("Describe it (only for another gender, optional)", description),
			Ui.Labelled("Pronouns (optional)", pronouns), Ui.Labelled("Orientation (optional, never used to decide who sees you)", orientation),
			Row(showGender, "Show my gender and pronouns"), Row(showOrientation, "Show my orientation"), Row(showAge, "Show my age"),
			Ui.Text("In Dating, show me", "FieldLabel"), showMeRows,
			Ui.Text("\"Show me\" works both ways: you see someone in Dating only if each of you is in the other's choice. People who prefer not to say their gender can appear for anyone.", "FooterClaims"),
			Ui.Button("Save", "SaveIdentity", async () =>
			{
				var genderId = gender.SelectedIndex > 0 ? genders[gender.SelectedIndex - 1].Id : null;
				var result = await Ui.Members!.SaveIdentityAsync(new IdentityUpdate(genderId, genderId == "another" ? description.Text : null, pronouns.Text,
					orientation.SelectedIndex > 0 ? orientations[orientation.SelectedIndex - 1].Id : null, showGender.IsToggled, showOrientation.IsToggled, showAge.IsToggled,
					showMe.Where(x => x.Value.IsToggled).Select(x => x.Key).ToList()));
				message.Text = result.Ok ? "Saved." : "Check the options you picked.";
			}),
			message);
	}

	private async Task FiltersAsync(VerticalStackLayout target)
	{
		target.Children.Clear();
		var result = await Ui.Members!.FiltersAsync(modeId);
		if (result.Value is not { } filter) { target.Children.Add(Ui.Text("We couldn't load these filters.", id: "FiltersRefused")); return; }
		var chips = modes.FirstOrDefault(m => m.Id == modeId)?.Intentions ?? [];
		var minAge = Ui.Entry("MinAge", "18", 3);
		var maxAge = Ui.Entry("MaxAge", "120", 3);
		(minAge.Keyboard, maxAge.Keyboard, minAge.Text, maxAge.Text) = (Keyboard.Numeric, Keyboard.Numeric, filter.MinAge?.ToString(), filter.MaxAge?.ToString());
		var idOnly = Toggle("IdVerifiedOnly", filter.IdVerifiedOnly);
		var all = Toggle("RequiredAll", filter.RequiredMatch == "all");
		var required = chips.ToDictionary(c => c.Id, c => Toggle($"Required_{c.Id}", filter.RequiredChips.Contains(c.Id)));
		var excluded = chips.ToDictionary(c => c.Id, c => Toggle($"Excluded_{c.Id}", filter.ExcludedChips.Contains(c.Id)));
		string[] visitorValues = ["both", "current", "upcoming", "neither"];
		var visitors = new Picker { AutomationId = "Visitors", ItemsSource = new[] { "Here now and arriving soon", "Here now", "Arriving soon", "Don't show visitors" }, SelectedIndex = Math.Max(0, Array.IndexOf(visitorValues, filter.Visitors)) };
		var distance = Ui.Entry("Distance", "km", 3);
		(distance.Keyboard, distance.Text, distance.IsEnabled) = (Keyboard.Numeric, filter.MaxDistanceKm?.ToString(), filter.PlusAvailable);
		var common = Toggle("MostInCommon", filter.Sort == "compatibility");
		common.IsEnabled = filter.PlusAvailable;
		var message = Ui.Text(null, id: "FilterStatus");
		target.Children.Add(Ui.Labelled("Age from", minAge));
		target.Children.Add(Ui.Labelled("to", maxAge));
		target.Children.Add(Row(idOnly, "ID Verified people only"));
		target.Children.Add(Ui.Text("Must have (up to 5)", "FieldLabel"));
		target.Children.Add(Row(all, "All of these (off: any of these)"));
		foreach (var chip in chips) target.Children.Add(Row(required[chip.Id], chip.Label));
		target.Children.Add(Ui.Text("Not for me (up to 10, works both ways)", "FieldLabel"));
		foreach (var chip in chips) target.Children.Add(Row(excluded[chip.Id], chip.Label));
		target.Children.Add(Ui.Labelled("Visitors in Travel", visitors));
		target.Children.Add(Ui.Text("Plus", "FieldLabel", "PlusFilters"));
		target.Children.Add(Ui.Labelled("Within (km of my home city)", distance));
		target.Children.Add(Row(common, "Most in common first"));
		if (!filter.PlusAvailable) target.Children.Add(Ui.Text("These filters are part of Plus.", "FooterClaims", "PlusUnavailable"));
		target.Children.Add(Ui.Button("Save filters", "SaveFilters", async () =>
		{
			static int? Number(string? text) => int.TryParse(text, out var n) ? n : null;
			var saved = await Ui.Members!.SaveFiltersAsync(modeId, new FilterUpdate(Number(minAge.Text), Number(maxAge.Text),
				required.Where(x => x.Value.IsToggled).Select(x => x.Key).ToList(), all.IsToggled ? "all" : "any",
				excluded.Where(x => x.Value.IsToggled).Select(x => x.Key).ToList(), idOnly.IsToggled, visitorValues[Math.Max(0, visitors.SelectedIndex)],
				filter.PlusAvailable ? Number(distance.Text) : null, filter.PlusAvailable && common.IsToggled ? "compatibility" : "recent"));
			message.Text = saved.Ok ? "Filters saved." : saved.Code == ProblemCodes.PlusRequired ? "That filter is part of Plus."
				: "Check the filters: ages 18–120, up to 5 must-haves and 10 not-for-me, never the same intention in both.";
		}));
		target.Children.Add(message);
	}

	private static Switch Toggle(string id, bool on) => new() { AutomationId = id, IsToggled = on };

	private static View Row(Switch toggle, string label) => new HorizontalStackLayout { Spacing = 8, Children = { Ui.Detach(toggle), Ui.Text(label) } };
}
