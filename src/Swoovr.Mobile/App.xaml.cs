using Microsoft.Extensions.DependencyInjection;

namespace Swoovr.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	// Cold start only: the launch splash plays once per process, never when a window is recreated.
	private static bool launched;

	protected override Window CreateWindow(IActivationState? activationState)
	{
		if (launched) return new Window(new AppShell());
		launched = true;
		var window = new Window();
		// Sign-in will hand off to Keycloak from here (Q09); until then the splash leads to the shell.
		window.Page = new LaunchPage(() => window.Page = new AppShell());
		return window;
	}
}
