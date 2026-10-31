using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Swoovr.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
#if SWOOVR_FCM
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        Firebase.FirebaseApp.InitializeApp(this);
        OpenPush(Intent);
    }
    protected override void OnNewIntent(Android.Content.Intent? intent) { base.OnNewIntent(intent); OpenPush(intent); }
    private static void OpenPush(Android.Content.Intent? intent)
    {
        var path = intent?.GetStringExtra("swoovr_path");
        if (path is not null) MainThread.BeginInvokeOnMainThread(async () => await PushRegistration.OpenAsync(path));
    }
#endif
}
