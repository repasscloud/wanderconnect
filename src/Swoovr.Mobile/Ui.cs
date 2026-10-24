using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Small builders for the member screens that are drawn from API answers (profile, Explore, Travel), using the keyed
/// styles in Styles.xaml. Every rule stays on the server; these only show what MemberClient returns.
/// </summary>
internal static class Ui
{
	public static Style Style(string key) => (Style)Application.Current!.Resources[key];

	public static Label Text(string? text, string? style = null, string? id = null)
	{
		var label = new Label { Text = text };
		if (style is not null) label.Style = Style(style);
		if (id is not null) label.AutomationId = id;
		return label;
	}

	/// <summary>
	/// Pages rebuild their cards on every load but keep their input views; a view must leave its old parent before it
	/// joins a new one (Android throws "The specified child already has a parent" otherwise).
	/// </summary>
	public static T Detach<T>(T view) where T : View
	{
		switch (view.Parent)
		{
			case Layout layout: layout.Remove(view); break;
			case Border border: border.Content = null; break;
			case ContentView content: content.Content = null; break;
			case ScrollView scroll: scroll.Content = null; break;
		}
		return view;
	}

	public static Border Card(params View[] children)
	{
		var stack = new VerticalStackLayout { Spacing = 8 };
		foreach (var child in children) stack.Children.Add(Detach(child));
		return new Border { Style = Style("Card"), Content = stack };
	}

	public static Button Button(string text, string id, Func<Task> clicked, bool primary = true)
	{
		var button = new Button { Text = text, AutomationId = id, Style = Style(primary ? "PrimaryButton" : "SecondaryButton") };
		button.Clicked += async (_, _) => { button.IsEnabled = false; try { await clicked(); } finally { button.IsEnabled = true; } };
		return button;
	}

	/// <summary>A chip that can be tapped: primary fill when on (Q16 picker).</summary>
	public static Button Chip(string text, bool on, string id, Func<Task> clicked, bool enabled = true)
	{
		var button = Button(text, id, clicked, primary: on);
		(button.FontSize, button.Padding, button.MinimumHeightRequest, button.IsEnabled, button.CornerRadius) = (12, new Thickness(10, 4), 32, enabled, 16);
		return button;
	}

	public static Entry Entry(string id, string placeholder, int max) => new() { AutomationId = id, Placeholder = placeholder, MaxLength = max };

	public static View Labelled(string label, View input) =>
		new VerticalStackLayout { Spacing = 4, Children = { Text(label, "FieldLabel"), new Border { Style = Style("InputBorder"), Content = Detach(input) } } };

	/// <summary>Badges as words in the API's order, as the account screen does (no MAUI badge artwork yet).</summary>
	public static string Badges(IReadOnlyList<BadgeView> badges) => string.Join(" · ", badges.Select(b => AccountText.BadgeLabel(b.Badge)));

	public static string Dates(DateOnly from, DateOnly to) => $"{from:d MMM yyyy} – {to:d MMM yyyy}";

	public const string GeoNamesCredit = "Place data © GeoNames (CC BY 4.0)";

	public static MemberClient? Members => MainPage.Auth.Members;

	public static async Task<bool> SignedInAsync() => MainPage.Auth.Available && await MainPage.Auth.IsSignedInAsync();
}

/// <summary>
/// City search over Swoovr's GeoNames copy (D111): type two letters, tap "City, Region, Country"; shows the GeoNames credit.
/// </summary>
internal sealed class CitySearchView : VerticalStackLayout
{
	private readonly Entry entry;
	private readonly VerticalStackLayout results = new() { Spacing = 2 };
	private readonly Label picked;
	private int version;

	public CitySearchView(string id, string label)
	{
		Spacing = 6;
		entry = Ui.Entry(id, "Start typing a city", 60);
		picked = Ui.Text(null, id: id + "Picked");
		entry.TextChanged += async (_, e) => await SearchAsync(e.NewTextValue ?? "");
		Children.Add(Ui.Labelled(label, entry));
		Children.Add(picked);
		Children.Add(results);
		Children.Add(Ui.Text(Ui.GeoNamesCredit, "FooterClaims", id + "Credit"));
	}

	public PlaceView? Selected { get; private set; }

	public void Show(PlaceView? place) => (Selected, picked.Text, picked.IsVisible) = (place, place is null ? null : "Selected: " + place.Label, place is not null);

	private async Task SearchAsync(string text)
	{
		var mine = ++version;
		results.Children.Clear();
		if (text.Trim().Length < 2 || Ui.Members is null) return;
		await Task.Delay(250);
		if (mine != version) return;
		var found = await Ui.Members.PlacesAsync(text.Trim());
		if (mine != version) return;
		foreach (var place in found.Value ?? [])
		{
			var button = new Button { Text = place.Label, AutomationId = $"City{place.Id}", Style = Ui.Style("SecondaryButton") };
			button.Clicked += (_, _) => { Show(place); results.Children.Clear(); entry.Text = null; };
			results.Children.Add(button);
		}
	}
}
