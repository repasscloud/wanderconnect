using Swoovr.Client;
using Swoovr.Contracts;
using Shapes = Microsoft.Maui.Controls.Shapes;

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

	/// <summary>Badges as words in the API's order, for screen readers and the badge row's description.</summary>
	public static string Badges(IReadOnlyList<BadgeView> badges) => string.Join(" · ", badges.Select(b => AccountText.BadgeLabel(b.Badge)));

	/// <summary>
	/// One badge drawn from the shared artwork (BadgeArtwork, D141), the same shapes as the web's Badge.razor, in the
	/// Badge* colours that check_design.py generates from brand-colours.json. Each stroke is mapped from its view box to a
	/// square of the given size, centred (BadgeArtwork.Placed).
	/// </summary>
	public static View BadgeIcon(string kind, double size)
	{
		var shape = BadgeArtwork.Shape(kind);
		var colour = (Color)Application.Current!.Resources["Badge" + char.ToUpperInvariant(BadgeArtwork.ColourToken(kind)[0]) + BadgeArtwork.ColourToken(kind)[1..]];
		var tick = (Color)Application.Current.Resources["BadgeTick"];
		var scale = size / Math.Max(shape.Width, shape.Height);
		var grid = new Grid { WidthRequest = size, HeightRequest = size, VerticalOptions = LayoutOptions.Center, AutomationId = "Badge_" + kind };
		SemanticProperties.SetDescription(grid, BadgeArtwork.Label(kind));
		var converter = new Shapes.PathGeometryConverter();
		foreach (var stroke in shape.Strokes)
		{
			// The data is placed in the square here, not with a RenderTransform: MAUI clips a transformed path to its
			// untransformed bounds, which cut the ID card's left edge on Android.
			var path = new Shapes.Path
			{
				Data = (Shapes.Geometry)converter.ConvertFromInvariantString(BadgeArtwork.Placed(shape, stroke, size))!,
				WidthRequest = size, HeightRequest = size, Aspect = Stretch.None,
				StrokeLineCap = Shapes.PenLineCap.Round, StrokeLineJoin = Shapes.PenLineJoin.Round, InputTransparent = true,
			};
			switch (stroke.Part)
			{
				case "line": (path.Stroke, path.StrokeThickness) = (colour, 1.8 * scale); break;
				case "disc": path.Fill = colour; break;
				case "tick": (path.Stroke, path.StrokeThickness) = (tick, 1.8 * scale); break;
				default:
					path.Fill = colour;
					if (path.Data is Shapes.PathGeometry bird) bird.FillRule = Shapes.FillRule.EvenOdd;
					break;
			}
			grid.Children.Add(path);
		}
		return grid;
	}

	/// <summary>The badges the API returned for one person, in its order (Q11), drawn as artwork.</summary>
	public static View BadgeRow(IReadOnlyList<BadgeView> badges, string id, double size = 16, bool dated = false)
	{
		var row = new HorizontalStackLayout { Spacing = 4, AutomationId = id, IsVisible = badges.Count > 0 };
		SemanticProperties.SetDescription(row, Badges(badges));
		foreach (var badge in badges)
		{
			row.Children.Add(BadgeIcon(AccountText.BadgeKind(badge.Badge), size));
			if (dated && badge.CheckedAt is { } at) row.Children.Add(Text($"checked {at:d MMM yyyy}", "FooterClaims"));
		}
		return row;
	}

	/// <summary>A whole-screen refusal (restricted, suspended, age check, beta, locked) in the shared words, or null.</summary>
	public static View? Barred(string? code, string id)
	{
		if (MemberText.Barred(code) is not { } text) return null;
		var card = Card(Text(text, id: id));
		if (code is ProblemCodes.AccountRestricted or ProblemCodes.AccountSuspended)
			((VerticalStackLayout)card.Content!).Children.Add(Button("Read the notice", id + "Notices", () => PushAsync(new NoticesPage())));
		if (code == ProblemCodes.AgeVerificationRequired)
			((VerticalStackLayout)card.Content!).Children.Add(Button("Verify my ID", id + "Verify", () => PushAsync(new VerificationPage())));
		return card;
	}

	public static Task PushAsync(Page page) => Shell.Current.Navigation.PushAsync(page);

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
