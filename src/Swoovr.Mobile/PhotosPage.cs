using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// The member's photos (D203), as on the web: add a photo from the library, see its state (nobody sees it until it is
/// approved), pick each mode's independent main and gallery, delete. The API checks everything again.
/// </summary>
public sealed class PhotosPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "PhotosStatus");
	private MyPhotosView? photos;
	private List<ConnectionModeView> modes = [];
	private string selectedMode = "dating";

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
		var decisions = (await Ui.Members!.PhotoDecisionsAsync()).Value ?? [];
		foreach (var decision in decisions)
		{
			var rows = new List<View> { Ui.Text(decision.Reason, "FieldLabel"), Ui.Text(decision.AppealId is null ? "" : decision.AppealOutcome ?? "Appeal awaiting review", "FooterClaims") };
			if (decision.CanAppeal)
			{
				var explanation = new Editor { Placeholder = "Why should this decision be reviewed?", MaxLength = 1000, AutomationId = $"PhotoAppealText_{decision.Id}" };
				rows.Add(explanation); rows.Add(Ui.Button("Submit photo appeal", $"PhotoAppeal_{decision.Id}", async () => { var result = await Ui.Members.AppealPhotoAsync(decision.Id, explanation.Text ?? ""); await LoadAsync(); status.Text = result.Ok ? "Photo appeal submitted." : "The decision is not eligible or the explanation is missing."; }));
			}
			body.Children.Add(Ui.Card([.. rows]));
		}
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
			rows.Add(Ui.Text(PhotoText.State(photo.State), "FooterClaims", $"PhotoState_{photo.Id}"));
			rows.Add(Ui.Button("Delete from library and every mode", $"PhotoDelete_{photo.Id}", async () =>
			{
				if (await DisplayAlertAsync("Delete photo?", "This removes the photo from your library and every mode.", "Delete everywhere", "Keep photo")) await ShowAsync(await Ui.Members!.DeletePhotoAsync(photo.Id), "Deleted.");
			}, primary: false));
			list.Add(new VerticalStackLayout { Spacing = 6, Children = { } }.With(rows));
		}
		body.Children.Add(Ui.Card([.. list]));
		var approved = photos.Photos.Where(p => p.State == "approved").ToList();
		if (approved.Count == 0) return;
		var perMode = new List<View> { Ui.Text("Photos for each mode", "SectionHeading"),
			Ui.Text($"{photos.Photos.Count} of {photos.MaxKept} library photos. {photos.Tier}: {photos.MaxPerMode} per mode, including its main. Free 8 · Plus 16 · Premium 24.", "FooterClaims") };
		var picker = new Picker { Title = "Connection mode", AutomationId = "PhotoMode" };
		foreach (var item in modes) picker.Items.Add(item.Label);
		picker.SelectedIndex = Math.Max(0, modes.FindIndex(m => m.Id == selectedMode));
		picker.SelectedIndexChanged += async (_, _) => { if (picker.SelectedIndex >= 0 && modes[picker.SelectedIndex].Id != selectedMode) { selectedMode = modes[picker.SelectedIndex].Id; await LoadAsync(); } };
		perMode.Add(picker);
		foreach (var mode in modes.Where(m => m.Id == selectedMode))
		{
			var chosen = photos.Modes.GetValueOrDefault(mode.Id) ?? [];
			var main = photos.Mains?.GetValueOrDefault(mode.Id);
			perMode.Add(Ui.Text($"{mode.Label}: {Math.Min(chosen.Count, photos.MaxPerMode)} of {photos.MaxPerMode} visible", "FieldLabel"));
			if (chosen.Count > photos.MaxPerMode) perMode.Add(Ui.Text("Extra photos are kept and hidden. Move them earlier to show them.", "FooterClaims"));
			perMode.Add(main is null || !chosen.Take(photos.MaxPerMode).Contains(main.Value) ? Ui.Text("No main selected: a placeholder shows.", "FooterClaims")
				: Ui.Button("Use a placeholder", "ClearModeMain", () => PlaceAsync(mode.Id, chosen.ToList(), null, true), primary: false));
			foreach (var id in chosen)
			{
				var index = chosen.ToList().IndexOf(id); var photo = approved.FirstOrDefault(p => p.Id == id);
				if (photo?.Link is { } link) perMode.Add(Ui.Photo(link, $"Assigned_{id}", 120));
				perMode.Add(Ui.Text($"Photo {index + 1}" + (main == id ? " · Main" : "") + (index >= photos.MaxPerMode ? " · Hidden by your plan" : ""), "FieldLabel"));
				if (main != id && index < photos.MaxPerMode) perMode.Add(Ui.Button("Make main in " + mode.Label, $"PhotoMain_{mode.Id}_{id}", () => PlaceAsync(mode.Id, chosen.ToList(), id, true), primary: false));
				if (index > 0) perMode.Add(Ui.Button("Move earlier", $"PhotoEarlier_{id}", () => PlaceAsync(mode.Id, Move(chosen, index, -1)), primary: false));
				if (index + 1 < chosen.Count) perMode.Add(Ui.Button("Move later", $"PhotoLater_{id}", () => PlaceAsync(mode.Id, Move(chosen, index, 1)), primary: false));
				perMode.Add(Ui.Button("Remove from " + mode.Label, $"PhotoRemove_{id}", () => PlaceAsync(mode.Id, chosen.Where(c => c != id).ToList()), primary: false));
			}
			perMode.Add(Ui.Text("Add from your library. Removing from a mode keeps the library photo.", "FooterClaims"));
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
		var members = Ui.Members; if (members is null) return;
		var remaining = Math.Max(0, (photos?.MaxKept ?? 60) - (photos?.Photos.Count ?? 0));
		if (remaining == 0) { status.Text = "Your library is full. Delete a library photo first."; return; }
		var picked = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = remaining }))?.ToList();
		if (picked is null || picked.Count == 0) return;
		var failed = 0;
		for (var index = 0; index < picked.Count; index++)
		{
			status.Text = $"Uploading {index + 1} of {picked.Count}…";
			await using var stream = await picked[index].OpenReadAsync();
			using var buffer = new MemoryStream();
			var chunk = new byte[81920]; int read;
			while ((read = await stream.ReadAsync(chunk)) > 0 && buffer.Length <= 10 * 1024 * 1024) await buffer.WriteAsync(chunk.AsMemory(0, read));
			if (buffer.Length > 10 * 1024 * 1024) { failed++; continue; }
			var result = await members.UploadPhotoAsync(buffer.ToArray(), picked[index].FileName, picked[index].ContentType);
			if (!result.Ok) failed++;
		}
		status.Text = failed == 0 ? "Uploaded. Photos are checked before anyone can see them." : $"{failed} upload(s) weren't accepted. Check photo size, format and library allowance.";
		for (var i = 0; i < 20; i++)
		{
			await Task.Delay(1500);
			if ((await members.PhotosAsync()).Value is { } now && now.Photos.All(p => p.State != "checking")) break;
		}
		await LoadAsync();
		status.Text = failed == 0 ? "Uploaded." : $"{failed} upload(s) were not accepted.";
	}

	private static List<Guid> Move(IReadOnlyList<Guid> ids, int index, int offset) { var result = ids.ToList(); (result[index], result[index + offset]) = (result[index + offset], result[index]); return result; }
	private async Task PlaceAsync(string scope, List<Guid> ids, Guid? main = null, bool changeMain = false)
	{
		var selected = changeMain ? main : photos?.Mains?.GetValueOrDefault(scope);
		if (selected is { } id && !ids.Contains(id)) selected = null;
		await ShowAsync(await Ui.Members!.PlaceModePhotosAsync(scope, ids, selected), "Saved.");
	}

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
