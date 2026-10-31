using Microsoft.Extensions.Logging;

namespace Swoovr.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			// No bundled fonts: Swoovr uses the platform's Helvetica Neue / Arial (see Resources/Styles/Styles.xaml).
			.UseMauiApp<App>();

#if DEBUG && !SWOOVR_UAT
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
