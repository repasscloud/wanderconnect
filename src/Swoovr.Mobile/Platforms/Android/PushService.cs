#if SWOOVR_FCM
using Android.App;
using Android.Content;
using Android.OS;
using Firebase.Messaging;

namespace Swoovr.Mobile;

[Service(Exported = false)]
[IntentFilter(["com.google.firebase.MESSAGING_EVENT"])]
public sealed class PushService : FirebaseMessagingService
{
    public override void OnNewToken(string token) => MainThread.BeginInvokeOnMainThread(async () => await PushRegistration.SaveTokenAsync(token));
    public override void OnMessageReceived(RemoteMessage message)
    {
        var notification = message.GetNotification();
        if (notification is null) return;
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (manager is null) return;
        if (OperatingSystem.IsAndroidVersionAtLeast(26)) manager.CreateNotificationChannel(new NotificationChannel("swoovr", "Swoovr", NotificationImportance.Default));
        var intent = new Intent(this, typeof(MainActivity));
        intent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        if (message.Data.TryGetValue("swoovr_path", out var path)) intent.PutExtra("swoovr_path", path);
        var notificationId = message.MessageId ?? Guid.NewGuid().ToString();
        intent.SetData(Android.Net.Uri.Parse("swoovr-push://notification/" + System.Uri.EscapeDataString(notificationId)));
        var pending = PendingIntent.GetActivity(this, 0, intent, PendingIntentFlags.UpdateCurrent | (OperatingSystem.IsAndroidVersionAtLeast(23) ? PendingIntentFlags.Immutable : 0));
        var builder = OperatingSystem.IsAndroidVersionAtLeast(26) ? new Notification.Builder(this, "swoovr") : new Notification.Builder(this);
        builder.SetContentTitle(notification.Title).SetContentText(notification.Body).SetSmallIcon(Resource.Mipmap.appicon).SetAutoCancel(true).SetContentIntent(pending);
        manager.Notify(notificationId, 0, builder.Build());
    }
}
#endif
