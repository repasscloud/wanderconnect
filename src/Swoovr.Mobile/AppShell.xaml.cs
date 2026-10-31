namespace Swoovr.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
	}
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await PushRegistration.OpenPendingAsync();
    }
}
