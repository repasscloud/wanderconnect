namespace Swoovr.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
        // D290: a cold-start guest begins on the welcome/account screen.
        Items[0].CurrentItem = Items[0].Items.Last();
	}
    protected override async void OnNavigating(ShellNavigatingEventArgs args)
    {
        base.OnNavigating(args);
        var target = args.Target.Location.OriginalString;
        if (!args.CanCancel || !new[] { "ExplorePage", "ConnectionsPage", "ChatsPage" }.Any(target.Contains)) return;
        var pending = args.GetDeferral();
        var redirect = false;
        try {
            if (!await Ui.SignedInAsync()) { args.Cancel(); redirect = true; }
            else { var account=(await MainPage.Auth.Members!.MeAsync()).Value;if(account is not null&&(account.PhoneRequired||account.DateOfBirth is null||account.RegisteredNamesRequired)){args.Cancel();redirect=true;} }
        }
        catch(Exception error) when(error is HttpRequestException or TaskCanceledException) {args.Cancel();redirect=true;}
        finally { pending.Complete(); }
        if (redirect) await GoToAsync("//MainPage");
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await PushRegistration.AskOnceAsync();
        await PushRegistration.OpenPendingAsync();
    }
}
