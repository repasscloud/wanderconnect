using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Your data and account (D144, D170–D173), as on the web's /account/data, in the same words (DeletionText).
/// Download my data saves the export to the app's cache and opens the share sheet. Asking for the export or a deletion,
/// cancelling and deleting now need a sign-in within five minutes: on stale-session the page offers "Sign in again",
/// which runs the system-browser sign-in with prompt=login. After a deletion request Keycloak has ended every session,
/// so the page forgets this phone's tokens and returns to Me.
/// </summary>
public sealed class DataPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label message = Ui.Text(null, id: "DataMessage");
	private readonly CheckBox immediately = new() { AutomationId = "DeleteImmediately" };
	private readonly CheckBox founderToken = new() { AutomationId = "DeleteFounderToken" };
	private readonly CheckBox confirm = new() { AutomationId = "DeleteConfirm" };
	private bool reauth;

	public DataPage()
	{
		Title = DeletionText.Title;
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
		var result = await Ui.Members!.DeletionAsync();
		if (result.Value is not { } view)
		{
			body.Children.Add(Ui.Text(DeletionText.Title, "PageTitle"));
			body.Children.Add(Ui.Text(DeletionText.Failed, id: "DataRefused"));
			return;
		}
		var pending = view.State == "pending";
		body.Children.Add(Ui.Text(pending ? DeletionText.PendingTitle(view) : DeletionText.Title, "PageTitle", "DataTitle"));
		if (reauth)
			body.Children.Add(Ui.Card(Ui.Text(DeletionText.SignInAgain), Ui.Button("Sign in again", "DataSignInAgain", SignInAgainAsync)));
		if (pending)
		{
			var rows = new List<View> { Ui.Text(DeletionText.PendingBody(view), id: "DeletionPending") };
			if (view.FounderCodeEmailed) rows.Add(Ui.Text("We've emailed your founder recovery code.", "FooterClaims"));
			rows.Add(Ui.Button("Cancel deletion", "DeletionCancel", () => ChangeAsync(Ui.Members!.CancelDeletionAsync)));
			if (view.DueAt is not { } due || due > DateTimeOffset.UtcNow)
				rows.Add(Ui.Button("Delete now", "DeletionNow", () => ChangeAsync(Ui.Members!.DeleteNowAsync), primary: false));
			body.Children.Add(Ui.Card([.. rows]));
		}
		body.Children.Add(Ui.Card(Ui.Text(DeletionText.ExportHeading, "SectionHeading"), Ui.Text(DeletionText.ExportBody, "FooterClaims"),
			Ui.Button("Download my data", "DataExport", ExportAsync, primary: false)));
		if (!pending)
		{
			var rows = new List<View>
			{
				Ui.Text(DeletionText.DeleteHeading, "SectionHeading"), Ui.Text(DeletionText.DeleteBody),
				Ui.Text(DeletionText.Kept, "FooterClaims"), Ui.Text(DeletionText.Subscriptions, "FooterClaims"),
			};
			if (view.Founder) rows.Add(Ui.Text(DeletionText.FounderKept, "FooterClaims", "DataFounderNotice"));
			rows.Add(Option(immediately, DeletionText.DeleteNowOption));
			if (view.Founder) rows.Add(Option(founderToken, DeletionText.FounderDeleteOption));
			rows.Add(Option(confirm, DeletionText.Confirm));
			rows.Add(Ui.Button("Delete my account", "DeleteAccount", RequestAsync));
			body.Children.Add(Ui.Card([.. rows]));
		}
		body.Children.Add(Ui.Detach(message));
	}

	private static View Option(CheckBox box, string text) => new HorizontalStackLayout
	{
		Spacing = 8, Children = { Ui.Detach(box), new Label { Text = text, VerticalOptions = LayoutOptions.Center, LineBreakMode = LineBreakMode.WordWrap, MaximumWidthRequest = 280 } },
	};

	private async Task ExportAsync()
	{
		(reauth, message.Text) = (false, null);
		var ticket = await Ui.Members!.RequestExportAsync();
		if (ticket.Value is null) { await RefusedAsync(ticket.Code); return; }
		var file = await Ui.Members.DownloadExportAsync(ticket.Value);
		if (file.Value is null) { await RefusedAsync(file.Code); return; }
		// The app's cache only; the share sheet lets the member keep it where they choose.
		var path = Path.Combine(FileSystem.CacheDirectory, $"swoovr-data-{DateTime.UtcNow:yyyy-MM-dd}.json");
		await File.WriteAllBytesAsync(path, file.Value);
		message.Text = "Your data is ready.";
		await Share.Default.RequestAsync(new ShareFileRequest { Title = DeletionText.ExportHeading, File = new ShareFile(path, "application/json") });
	}

	private async Task RequestAsync()
	{
		(reauth, message.Text) = (false, null);
		if (!confirm.IsChecked) { message.Text = "Tick the box to confirm."; return; }
		var result = await Ui.Members!.RequestDeletionAsync(new DeletionRequest(true, immediately.IsChecked, founderToken.IsChecked));
		if (result.Value is null) { await RefusedAsync(result.Code); return; }
		// D170: every session has ended in Keycloak; forget this phone's tokens and go back to Me (signed out).
		MainPage.Auth.ForgetLocally();
		await Shell.Current.Navigation.PopToRootAsync();
	}

	private async Task ChangeAsync(Func<CancellationToken, Task<ApiResult<DeletionView>>> call)
	{
		(reauth, message.Text) = (false, null);
		var result = await call(default);
		if (result.Value is null) { await RefusedAsync(result.Code); return; }
		await LoadAsync();
		message.Text = result.Value.State == "none" ? "Deletion cancelled. Your account is back." : "Your account will be deleted within the hour.";
	}

	private async Task RefusedAsync(string? code)
	{
		if (DeletionText.Refusal(code) is { } text) { message.Text = text; return; }
		reauth = true;
		await LoadAsync();
	}

	private async Task SignInAgainAsync()
	{
		try { reauth = !await MainPage.Auth.SignInAsync(reauthenticate: true); }
		catch (Exception error) when (error is NativeSignInException or HttpRequestException or TaskCanceledException) { reauth = true; }
		await LoadAsync();
		message.Text = reauth ? "Sign-in was cancelled." : "Signed in. Try again now.";
	}
}
