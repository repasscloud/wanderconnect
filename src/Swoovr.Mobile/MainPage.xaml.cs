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
			BetaNotice.IsVisible = LockedCard.IsVisible = RestrictedCard.IsVisible = MemberSections.IsVisible = false;
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
		var account = locked ? null : result?.Value;
		// D160: anything but active shows only the restricted card; notices and appeals stay open.
		var restricted = account is not null && MemberText.RestrictedTitle(account.AccountState) is not null;
		RestrictedCard.IsVisible = restricted;
		(RestrictedTitle.Text, RestrictedBody.Text) = (account is null ? null : MemberText.RestrictedTitle(account.AccountState), MemberText.RestrictedBody);
		MemberSections.IsVisible = account is not null && !restricted;
		if (account is null || restricted) return;
		AgeCheckCard.IsVisible = account.AgeStatus == "needsVerification";
		BadgeList.Children.Clear();
		if (account.Badges.Count == 0)
			BadgeList.Children.Add(new Label { Text = "No badges yet. Each badge shows one check, such as a photo or ID check, once it's done.", Style = Ui.Style("FooterClaims") });
		foreach (var badge in account.Badges)
			BadgeList.Children.Add(new HorizontalStackLayout
			{
				Spacing = 8,
				Children =
				{
					Ui.BadgeIcon(AccountText.BadgeKind(badge.Badge), 20),
					new Label { Text = AccountText.BadgeLabel(badge.Badge) + (badge.CheckedAt is { } at ? $" · checked {at:d MMM yyyy}" : ""), VerticalOptions = LayoutOptions.Center },
				},
			});
		if (account.DateOfBirth is { } dob) DateOfBirth.Date = dob.ToDateTime(TimeOnly.MinValue);
		DateOfBirthVerified.IsVisible = account.DateOfBirthVerified;
	}

	private async void OnProfile(object? sender, EventArgs e) => await Ui.PushAsync(new ProfilePage());
	private async void OnMatching(object? sender, EventArgs e) => await Ui.PushAsync(new MatchingPage());
	private async void OnTravel(object? sender, EventArgs e) => await Ui.PushAsync(new TravelPage());
	private async void OnVerification(object? sender, EventArgs e) => await Ui.PushAsync(new VerificationPage());
	private async void OnNotices(object? sender, EventArgs e) => await Ui.PushAsync(new NoticesPage());

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
		if (DateOfBirth.Date is not { } date) return;
		SaveAge.IsEnabled = false;
		var result = await Auth.Members!.SaveAgeAsync(new AgeUpdate(DateOnly.FromDateTime(date), null));
		SaveAge.IsEnabled = true;
		if (result.Code is ProblemCodes.InvalidAge or ProblemCodes.DateOfBirthRequired) { AgeMessage.Text = AccountText.InvalidAge; return; }
		if (result.Code == ProblemCodes.DateOfBirthLocked) { AgeMessage.Text = "Your age was confirmed by ID verification. Contact Swoovr support to change it."; return; }
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
