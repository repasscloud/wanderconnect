using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Mobile number (D148, D181), as on the web's account page and in the same words (MemberText): send a code, then
/// confirm it. While the sender is simulated (D174) the page says so; nothing is texted.
/// </summary>
public sealed class PhonePage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label message = Ui.Text(null, id: "PhoneMessage");
	private readonly Entry number = new() { AutomationId = "PhoneNumber", Placeholder = "0412 345 678", Keyboard = Keyboard.Telephone, MaxLength = 32 };
	private readonly Entry code = new() { AutomationId = "PhoneCode", Placeholder = "6-digit code", Keyboard = Keyboard.Numeric, MaxLength = 6 };
	private string? sentTo;

	public PhonePage()
	{
		Title = "Mobile number";
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
		body.Children.Add(Ui.Text("Mobile number", "PageTitle"));
		var result = await Ui.Members!.PhoneAsync();
		if (result.Value is not { } view) { body.Children.Add(Ui.Barred(result.Code, "PhoneBarred") ?? Ui.Text("This isn't available right now.", id: "PhoneRefused")); return; }
		var rows = new List<View> { Ui.Text(MemberText.PhoneIntro, "FooterClaims") };
		if (view.Sender == "simulated") rows.Add(Ui.Text(MemberText.PhoneSimulated, "FieldLabel", "PhoneSimulated"));
		if (view.Verified) rows.Add(Ui.Text("Verified: " + view.Number, id: "PhoneVerified"));
		rows.Add(Ui.Text(view.Verified ? "Change number" : "Mobile number", "FieldLabel"));
		rows.Add(Ui.Detach(number));
		rows.Add(Ui.Button("Send code", "PhoneSend", SendAsync, primary: view.PendingNumber is null));
		if (view.PendingNumber is not null)
		{
			rows.Add(Ui.Text("Code sent to " + view.PendingNumber, "FieldLabel", "PhonePending"));
			rows.Add(Ui.Detach(code));
			rows.Add(Ui.Button("Confirm", "PhoneConfirm", ConfirmAsync));
		}
		rows.Add(Ui.Detach(message));
		body.Children.Add(Ui.Card([.. rows]));
	}

	private async Task SendAsync()
	{
		var sent = await Ui.Members!.SendPhoneCodeAsync(number.Text ?? "");
		if (sent.Value is null) { message.Text = MemberText.PhoneProblem(sent.Code); return; }
		sentTo = number.Text;
		code.Text = null;
		await LoadAsync();
		message.Text = "Code sent. It expires in 5 minutes.";
	}

	private async Task ConfirmAsync()
	{
		var confirmed = await Ui.Members!.ConfirmPhoneAsync(sentTo ?? number.Text ?? "", code.Text ?? "");
		if (confirmed.Value is null) { message.Text = MemberText.PhoneProblem(confirmed.Code); return; }
		(code.Text, number.Text) = (null, null);
		await LoadAsync();
		message.Text = "Your mobile number is verified.";
	}
}
