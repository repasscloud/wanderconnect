using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// The member's photos (D203), as on the web: add a photo from the library, see its state (nobody sees it until it is
/// approved), pick the main photo (shown in every mode) and each mode's own photos, delete. The API checks everything again.
/// </summary>
public sealed class PhotosPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "PhotosStatus");
	private MyPhotosView? photos;
	private List<ConnectionModeView> modes = [];

	public PhotosPage()
	{
		Title = "Photos";
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
		body.Children.Add(Ui.Text("Your photos", "PageTitle"));
		body.Children.Add(Ui.Text(PhotoText.Rules, "FooterClaims", "PhotosRules"));
		if (!await Ui.SignedInAsync()) { body.Children.Add(Ui.Text("Sign in on the Me tab to manage your photos.", id: "PhotosSignedOut")); return; }
		var (own, catalogue) = (await Ui.Members!.PhotosAsync(), await Ui.Members.ModesAsync());
		if (own.Value is null) { body.Children.Add(Ui.Text("We couldn't load your photos.", id: "PhotosRefused")); return; }
		(photos, modes) = (own.Value, catalogue.Value ?? []);
		body.Children.Add(Ui.Card(Ui.Text("Add a photo", "SectionHeading"),
			Ui.Text($"JPEG, PNG or WebP, up to 10 MB. You can keep up to {photos.MaxKept} photos.", "FooterClaims"),
			Ui.Button("Choose a photo", "PhotoAdd", AddAsync), Ui.Detach(status)));
		var list = new List<View> { Ui.Text("Your photos", "SectionHeading") };
		if (photos.Photos.Count == 0) list.Add(Ui.Text("No photos yet.", id: "PhotosNone"));
		foreach (var photo in photos.Photos)
		{
			var rows = new List<View>();
			if (photo.Link is not null) rows.Add(Ui.Photo(photo.Link, $"Photo_{photo.Id}", 160));
			rows.Add(Ui.Text(PhotoText.State(photo.State) + (photos.Main == photo.Id ? " · Main photo" : ""), "FooterClaims", $"PhotoState_{photo.Id}"));
			if (photo.State == "approved" && photos.Main != photo.Id)
				rows.Add(Ui.Button("Make main photo", $"PhotoMain_{photo.Id}", () => PlaceAsync("main", [photo.Id]), primary: false));
			rows.Add(Ui.Button("Delete", $"PhotoDelete_{photo.Id}", async () => await ShowAsync(await Ui.Members!.DeletePhotoAsync(photo.Id), "Deleted."), primary: false));
			list.Add(new VerticalStackLayout { Spacing = 6, Children = { } }.With(rows));
		}
		body.Children.Add(Ui.Card([.. list]));
		var approved = photos.Photos.Where(p => p.State == "approved").ToList();
		if (approved.Count == 0) return;
		var perMode = new List<View> { Ui.Text("Photos for each mode", "SectionHeading"),
			Ui.Text($"Your main photo shows in every mode you use. Extra photos show only in the mode you pick (up to {photos.MaxPerMode}).", "FooterClaims") };
		foreach (var mode in modes)
		{
			var chosen = photos.Modes.GetValueOrDefault(mode.Id) ?? [];
			perMode.Add(Ui.Text(mode.Label, "TileName"));
			foreach (var photo in approved)
			{
				var on = chosen.Contains(photo.Id);
				perMode.Add(Ui.Chip($"{(on ? "Shown" : "Show")} photo {approved.IndexOf(photo) + 1} in {mode.Label}", on, $"ModePhoto_{mode.Id}_{photo.Id}",
					() => PlaceAsync(mode.Id, on ? chosen.Where(c => c != photo.Id).ToList() : [.. chosen, photo.Id]), enabled: on || chosen.Count < photos.MaxPerMode));
			}
		}
		body.Children.Add(Ui.Card([.. perMode]));
	}

	private async Task AddAsync()
	{
		var picked = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault();
		if (picked is null) return;
		await using var stream = await picked.OpenReadAsync();
		using var buffer = new MemoryStream();
		await stream.CopyToAsync(buffer);
		var result = await Ui.Members!.UploadPhotoAsync(buffer.ToArray(), picked.FileName, picked.ContentType);
		status.Text = result.Ok ? "Uploaded. We're checking it now — nobody can see it until it's approved." : PhotoText.Refusal(result.Code);
		if (!result.Ok) return;
		for (var i = 0; i < 20; i++)
		{
			await Task.Delay(1500);
			if ((await Ui.Members.PhotosAsync()).Value is { } now && now.Photos.All(p => p.State != "checking")) break;
		}
		await LoadAsync();
		status.Text = "Uploaded.";
	}

	private async Task PlaceAsync(string scope, List<Guid> ids) => await ShowAsync(await Ui.Members!.PlacePhotosAsync(scope, ids), "Saved.");

	private async Task ShowAsync(ApiResult<MyPhotosView> result, string ok)
	{
		await LoadAsync();
		status.Text = result.Ok ? ok : PhotoText.Refusal(result.Code);
	}
}

internal static class LayoutExtensions
{
	public static T WithDescription<T>(this T view, string description) where T : View
	{
		SemanticProperties.SetDescription(view, description);
		return view;
	}

	public static VerticalStackLayout With(this VerticalStackLayout layout, IEnumerable<View> children)
	{
		foreach (var child in children) layout.Children.Add(child);
		return layout;
	}
}
