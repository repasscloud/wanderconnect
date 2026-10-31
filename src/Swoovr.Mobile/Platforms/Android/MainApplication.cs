using Android.App;
using Android.Runtime;

namespace Swoovr.Mobile;

#if DEBUG && !SWOOVR_UAT
// Debug builds talk to the local stack over plain HTTP (through `adb reverse`); release builds never do.
[Application(UsesCleartextTraffic = true)]
#else
[Application(UsesCleartextTraffic = false)]
#endif
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
