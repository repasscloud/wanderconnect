#if SWOOVR_APNS
using Foundation;
using UIKit;
using UserNotifications;

namespace Swoovr.Mobile;

public sealed class PushDelegate : UNUserNotificationCenterDelegate
{
    public override void DidReceiveNotificationResponse(UNUserNotificationCenter center, UNNotificationResponse response, Action completionHandler)
    {
        var path = response.Notification.Request.Content.UserInfo[new NSString("swoovr_path")]?.ToString();
        MainThread.BeginInvokeOnMainThread(async () => { try { await PushRegistration.OpenAsync(path); } finally { completionHandler(); } });
    }
    public override void WillPresentNotification(UNUserNotificationCenter center, UNNotification notification, Action<UNNotificationPresentationOptions> completionHandler)
        => completionHandler(UNNotificationPresentationOptions.Banner | UNNotificationPresentationOptions.Sound);
}
#endif
