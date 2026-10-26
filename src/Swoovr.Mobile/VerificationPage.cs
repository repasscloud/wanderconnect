using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → ID verification (D126–D129, D147), as on the web: the member's status in the web's words, a permanent test-mode
/// label, and a start that asks the API for a Didit session with the "app" client kind (the API picks the return address
/// from its configuration). Didit's hosted page opens in the system browser; coming back to this page re-reads the result
/// through the API, so a return link proves nothing by itself.
/// </summary>
public sealed class VerificationPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label message = Ui.Text(null, id: "VerificationMessage");

	public VerificationPage()
	{
		Title = "ID verification";
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
		body.Children.Add(Ui.Text("ID verification", "PageTitle"));
		var result = await Ui.Members!.VerificationAsync();
		if (result.Value is not { } view) { body.Children.Add(Ui.Barred(result.Code, "VerificationBarred") ?? Ui.Text("ID verification isn't available right now.", id: "VerificationRefused")); return; }
		if (view.TestMode) body.Children.Add(Ui.Text("Test mode: Didit checks a sample ID. No real verification is made.", "FieldLabel", "VerificationTestMode"));
		if (view.AgeCheckRequired)
			body.Children.Add(Ui.Card(Ui.Text("Please verify your age", "SectionHeading"),
				Ui.Text("We need to confirm you're 18 or over before you can keep using Swoovr. Verify your ID below, or contact Swoovr support.")));
		var rows = new List<View>
		{
			Ui.Text("Your ID check", "SectionHeading"),
			Ui.Text("Optional and free. Didit checks a government photo ID and a live selfie. Swoovr keeps only the result, never your document or photos. The ID Verified badge says your ID was checked; it doesn't say your profile photos show you.", "FooterClaims"),
			Ui.Text(MemberText.VerificationState(view), id: "VerificationState"),
		};
		if (view.Url is { } url)
		{
			rows.Add(Ui.Button("Continue on Didit", "VerificationContinue", () => Browser.Default.OpenAsync(url, BrowserLaunchMode.External)));
			rows.Add(Ui.Text("When you're done, come back here and choose Check result.", "FooterClaims"));
			rows.Add(Ui.Button("Check result", "VerificationRefresh", async () => { await LoadAsync(); message.Text = "Updated."; }, primary: false));
		}
		else if (view.CanStart && view.Available)
			rows.Add(Ui.Button("Verify my ID", "VerificationStart", StartAsync));
		else if (view.CanStart) rows.Add(Ui.Text("ID verification isn't available right now.", "FooterClaims", "VerificationUnavailable"));
		else if (view.Refusal == "attemptUsed") rows.Add(Ui.Text("You've used your ID check. Contact Swoovr support if you'd like another.", "FooterClaims", "VerificationUsed"));
		rows.Add(Ui.Detach(message));
		body.Children.Add(Ui.Card([.. rows]));
	}

	private async Task StartAsync()
	{
		var result = await Ui.Members!.StartVerificationAsync("app");
		if (result.Value is { Url: { } url })
		{
			await LoadAsync();
			message.Text = "Your check is ready. It opens on Didit in your browser.";
			await Browser.Default.OpenAsync(url, BrowserLaunchMode.External);
			return;
		}
		await LoadAsync();
		message.Text = MemberText.VerificationProblem(result.Code);
	}
}
