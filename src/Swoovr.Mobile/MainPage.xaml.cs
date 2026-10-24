using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Signed out: a sign-in button. Signed in: what the API says about this member, worded as on the web
/// (AccountText). The app never works out access, badges or beta status itself (D55).
/// </summary>
public partial class MainPage : ContentPage
{
	internal static readonly AppSignIn Auth = new();
	private bool busy;

	public MainPage()
	{
		InitializeComponent();
		DateOfBirth.MaximumDate = DateTime.Today;
		DateOfBirth.MinimumDate = DateTime.Today.AddYears(-120);
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		if (!Auth.Available) { ShowSignedOut("Sign-in isn't available in this build yet."); SignInButton.IsEnabled = false; return; }
		if (!await Auth.IsSignedInAsync()) { ShowSignedOut(null); return; }
		ShowStatus("Loading your account…");
		SessionSummary? session = null;
		ApiResult<MemberAccount>? me = null;
		try
		{
			var result = await Auth.Members!.SessionAsync();
			if (result.Status == 401) { await Auth.SignOutAsync(); ShowSignedOut("Your session ended. Sign in again."); return; }
			session = result.Value;
			if (session is not null) me = await Auth.Members.MeAsync();
		}
		catch (HttpRequestException) { }
		catch (TaskCanceledException) { }
		ShowAccount(session, me);
	}

	private void ShowSignedOut(string? message)
	{
		(Heading.Text, Intro.Text) = ("Swoovr", "Sign in to see your account.");
		(SignedOutPanel.IsVisible, AccountPanel.IsVisible) = (true, false);
		ShowStatus(message);
	}

	private void ShowStatus(string? message) => (Status.Text, Status.IsVisible) = (message, message is not null);

	private void ShowAccount(SessionSummary? session, ApiResult<MemberAccount>? me)
	{
		ShowStatus(null);
		(SignedOutPanel.IsVisible, AccountPanel.IsVisible) = (false, true);
		if (session is null)
		{
			(Heading.Text, Intro.Text) = ("Swoovr", "We couldn't confirm your session with the Swoovr API. Try signing in again.");
			BetaNotice.IsVisible = LockedCard.IsVisible = MemberSections.IsVisible = false;
			Method.Text = EmailVerified.Text = AdultAttested.Text = MemberArea.Text = "—";
			return;
		}
		var blocked = AccountText.BetaBlocked(session);
		(Heading.Text, Intro.Text) = blocked
			? ("Almost there.", "You're signed in. The Swoovr app opens once your email is on the private beta list.")
			: ("You're in.", "You're signed in to the Swoovr development preview. Profiles, matching and messages aren't connected to accounts yet.");
		BetaNotice.IsVisible = blocked;
		Method.Text = AccountText.MethodLabel(session.Method) + (session.MultiFactor ? " + one-time code" : "");
		EmailVerified.Text = session.EmailVerified ? "Verified" : "Not verified yet";
		AdultAttested.Text = session.AdultAttested ? "Confirmed when you joined" : "Not recorded";
		MemberArea.Text = session.MemberArea ? "Allowed" : "Not allowed";
		ShowMember(me);
	}

	private void ShowMember(ApiResult<MemberAccount>? result)
	{
		var locked = result?.Code == ProblemCodes.AccountLocked;
		LockedCard.IsVisible = locked;
		MemberSections.IsVisible = !locked && result?.Value is not null;
		if (locked || result?.Value is not { } account) return;
		BadgeList.Children.Clear();
		if (account.Badges.Count == 0)
			BadgeList.Children.Add(new Label { Text = "No badges yet. Each badge shows one check, such as a photo or ID check, once it's done.", Style = (Style)Application.Current!.Resources["FooterClaims"] });
		foreach (var badge in account.Badges)
			BadgeList.Children.Add(new HorizontalStackLayout
			{
				Spacing = 8,
				Children =
				{
					new Microsoft.Maui.Controls.Shapes.Ellipse { WidthRequest = 14, HeightRequest = 14, Fill = new SolidColorBrush(BadgeColour(badge.Badge)), VerticalOptions = LayoutOptions.Center },
					new Label { Text = AccountText.BadgeLabel(badge.Badge) + (badge.CheckedAt is { } at ? $" · checked {at:d MMM yyyy}" : "") },
				},
			});
		UseDateOfBirth.IsToggled = account.DateOfBirth is not null;
		if (account.DateOfBirth is { } dob) DateOfBirth.Date = dob.ToDateTime(TimeOnly.MinValue);
		Age.Text = account.DateOfBirth is null ? account.DeclaredAge?.ToString() : null;
	}

	private static Color BadgeColour(string badge) =>
		(Color)Application.Current!.Resources[AccountText.BadgeKind(badge) switch
		{
			"founding" => "BadgeFounding", "photo" => "BadgePhotoVerified", "id" => "BadgeIdVerified", "staff" => "BadgeStaff", _ => "BadgeBot",
		}];

	private void OnUseDateOfBirth(object? sender, ToggledEventArgs e)
	{
		DateOfBirth.IsVisible = e.Value;
		AgeLabel.IsVisible = AgeBorder.IsVisible = !e.Value;
	}

	private async void OnSignIn(object? sender, EventArgs e)
	{
		if (busy) return;
		(busy, SignInButton.IsEnabled) = (true, false);
		try
		{
			ShowStatus(await Auth.SignInAsync() ? null : "Sign-in was cancelled.");
		}
		catch (Exception error) when (error is NativeSignInException or HttpRequestException or TaskCanceledException)
		{
			ShowStatus("We couldn't sign you in. Try again.");
		}
		finally { (busy, SignInButton.IsEnabled) = (false, true); }
		if (await Auth.IsSignedInAsync()) await RefreshAsync();
	}

	private async void OnSignOut(object? sender, EventArgs e)
	{
		await Auth.SignOutAsync();
		ShowSignedOut("You're signed out.");
	}

	private async void OnBetaLink(object? sender, TappedEventArgs e) => await Launcher.Default.OpenAsync(AccountText.BetaUrl);

	private async void OnSaveAge(object? sender, EventArgs e)
	{
		int? age = null;
		if (!UseDateOfBirth.IsToggled && !string.IsNullOrWhiteSpace(Age.Text))
		{
			if (!int.TryParse(Age.Text, out var years)) { AgeMessage.Text = AccountText.InvalidAge; return; }
			age = years;
		}
		DateOnly? dob = UseDateOfBirth.IsToggled && DateOfBirth.Date is { } date ? DateOnly.FromDateTime(date) : null;
		SaveAge.IsEnabled = false;
		var result = await Auth.Members!.SaveAgeAsync(new AgeUpdate(dob, dob is null ? age : null));
		SaveAge.IsEnabled = true;
		if (result.Code == ProblemCodes.InvalidAge) { AgeMessage.Text = AccountText.InvalidAge; return; }
		ShowMember(result);
		AgeMessage.Text = result.Code == ProblemCodes.AccountLocked ? null : AccountText.AgeSaved(result.Value);
	}

	private async void OnApplyCoupon(object? sender, EventArgs e)
	{
		if (string.IsNullOrWhiteSpace(Coupon.Text)) return;
		ApplyCoupon.IsEnabled = false;
		var result = await Auth.Members!.RedeemAsync(Coupon.Text.Trim());
		ApplyCoupon.IsEnabled = true;
		CouponMessage.Text = AccountText.Coupon(result).Message;
		if (result.Value is not null) { ShowMember(result); Coupon.Text = null; }
	}
}
