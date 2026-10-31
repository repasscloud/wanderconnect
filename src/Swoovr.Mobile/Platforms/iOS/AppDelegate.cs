using Foundation;

namespace Swoovr.Mobile;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
#if SWOOVR_APNS
    private readonly PushDelegate pushDelegate = new();
    public override bool FinishedLaunching(UIKit.UIApplication application, NSDictionary? launchOptions)
    {
        UserNotifications.UNUserNotificationCenter.Current.Delegate = pushDelegate;
        return base.FinishedLaunching(application, launchOptions);
    }
    [Export("application:didRegisterForRemoteNotificationsWithDeviceToken:")]
    public void RegisteredForRemoteNotifications(UIKit.UIApplication application, NSData deviceToken)
    {
        var token = Convert.ToHexStringLower(deviceToken.ToArray());
        MainThread.BeginInvokeOnMainThread(async () => await PushRegistration.SaveTokenAsync(token));
    }
#endif
	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
