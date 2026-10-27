using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// The member's own profile (D92, D106, D110), as on the web: core fields and home city, then each mode with its own
/// switches and intention chips ("n of 5"). The API checks every value again.
/// </summary>
public sealed class ProfilePage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "ProfileStatus");
	private readonly Entry name = Ui.Entry("DisplayName", "Display name", 40), area = Ui.Entry("Area", "For example, Adelaide CBD", 60);
	private readonly Editor about = new() { AutomationId = "About", MaxLength = 500, AutoSize = EditorAutoSizeOption.TextChanges };
	private readonly CitySearchView home = new("HomeCity", "Home city");
	private List<ConnectionModeView> modes = [];
	private MyProfileView? profile;

	public ProfilePage()
	{
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
		body.Children.Clear();
		body.Children.Add(Ui.Text("Your profile", "PageTitle"));
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to edit your profile.", id: "ProfileSignedOut")); return; }
		var (catalogue, own) = (await Ui.Members!.ModesAsync(), await Ui.Members.ProfileAsync());
		if (catalogue.Value is null || own.Value is null)
		{
			body.Children.Add(Ui.Text(catalogue.Code == ProblemCodes.BetaNotEnrolled || own.Code == ProblemCodes.BetaNotEnrolled
				? "Your profile opens once your email is on the private beta list." : "We couldn't load your profile.", id: "ProfileRefused"));
			return;
		}
		(modes, profile) = (catalogue.Value, own.Value);
		(name.Text, area.Text, about.Text) = (profile.DisplayName, profile.Area, profile.About);
		home.Show(profile.HomeCity);
		body.Children.Add(Ui.Card(Ui.Text("About you", "SectionHeading"), Ui.Text("The same in every mode.", "FooterClaims"),
			Ui.Labelled("Display name", name), Ui.Labelled("Area", area), Ui.Labelled("About me", about), home,
			Ui.Text("Only you see your home city. It decides who counts as local when people visit.", "FooterClaims"),
			// D150: optional; one reading, and Swoovr keeps only whether it matched.
			Ui.Button("Check my location matches", "HomeCheck", async () =>
			{
				var reading = await Ui.ReadLocationAsync();
				var result = reading is null ? null : await Ui.Members!.HomeCheckAsync(reading);
				status.Text = result?.Value is { } check ? MemberText.HomeCheck(check.Outcome) : MemberText.ArrivalProblem(ProblemCodes.LocationUnusable);
			}, primary: false),
			Ui.Button("Save", "SaveProfile", SaveProfileAsync), status));
		body.Children.Add(Ui.Text("Connection modes", "SectionHeading"));
		foreach (var mode in modes) body.Children.Add(ModeCard(mode));
	}

	private View ModeCard(ConnectionModeView mode)
	{
		var setting = profile!.Modes.FirstOrDefault(m => m.Id == mode.Id) ?? new MyModeView(mode.Id, false, false, []);
		var use = new Switch { IsToggled = setting.Enabled, AutomationId = $"Use_{mode.Id}" };
		var show = new Switch { IsToggled = setting.Visible, IsEnabled = setting.Enabled, AutomationId = $"Show_{mode.Id}" };
		use.Toggled += async (_, e) => await SaveModeAsync(mode.Id, e.Value, setting.Visible || e.Value, setting.Intentions, setting.About);
		show.Toggled += async (_, e) => await SaveModeAsync(mode.Id, setting.Enabled, e.Value, setting.Intentions, setting.About);
		// D175: an about for this mode only. #91: the main about shows here only if the member says so.
		var modeAbout = new Editor { Text = setting.About, Placeholder = setting.ShareCoreAbout ? "Uses your main about" : $"Empty: no about shows in {mode.Label}", MaxLength = 500,
			AutoSize = EditorAutoSizeOption.TextChanges, AutomationId = $"ModeAbout_{mode.Id}" };
		modeAbout.Unfocused += async (_, _) => { if ((modeAbout.Text ?? "") != (setting.About ?? "")) await SaveModeAsync(mode.Id, setting.Enabled, setting.Visible, setting.Intentions, modeAbout.Text); };
		var share = new Switch { IsToggled = setting.ShareCoreAbout, AutomationId = $"ShareCore_{mode.Id}" };
		share.Toggled += async (_, e) => await SaveModeAsync(mode.Id, setting.Enabled, setting.Visible, setting.Intentions, setting.About, e.Value);
		var picked = setting.Intentions.Count(id => mode.Intentions.Any(i => i.Id == id));
		var chips = new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, AutomationId = $"Chips_{mode.Id}" };
		foreach (var intention in mode.Intentions)
		{
			var on = setting.Intentions.Contains(intention.Id);
			var chip = Ui.Chip(intention.Label, on, $"Chip_{intention.Id}", () => SaveModeAsync(mode.Id, setting.Enabled, setting.Visible,
				on ? setting.Intentions.Where(x => x != intention.Id).ToList() : [.. setting.Intentions, intention.Id], setting.About), enabled: on || picked < mode.MaxIntentions);
			chip.Margin = new Thickness(0, 0, 6, 6);
			chips.Children.Add(chip);
		}
		return Ui.Card(Ui.Text(mode.Label, "SectionHeading"),
			new HorizontalStackLayout { Spacing = 8, Children = { use, Ui.Text($"Use {mode.Label}") } },
			new HorizontalStackLayout { Spacing = 8, Children = { show, Ui.Text($"Show my profile in {mode.Label}") } },
			Ui.Text($"{picked} of {mode.MaxIntentions}", "FieldLabel", $"ChipCount_{mode.Id}"), chips,
			Ui.Text($"About me in {mode.Label} (only shown in {mode.Label})", "FieldLabel"), modeAbout,
			new HorizontalStackLayout { Spacing = 8, Children = { share, Ui.Text($"Show my main about in {mode.Label} when this is empty") } });
	}

	private async Task SaveProfileAsync()
	{
		var result = await Ui.Members!.SaveProfileAsync(new ProfileUpdate(name.Text, area.Text, about.Text, home.Selected?.Id));
		status.Text = result.Ok ? "Saved." : "Check the fields: no special characters, and keep within the lengths shown.";
	}

	private async Task SaveModeAsync(string modeId, bool enabled, bool visible, IReadOnlyList<Guid> intentions, string? about, bool? shareCoreAbout = null)
	{
		var result = await Ui.Members!.SaveModeAsync(modeId, new ModeUpdate(enabled, visible, intentions, about, shareCoreAbout));
		if (result.Value is null) { status.Text = "That change wasn't saved. Try again."; return; }
		await LoadAsync();
	}
}
