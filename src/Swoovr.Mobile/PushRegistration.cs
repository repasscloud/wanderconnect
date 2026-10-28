#if ANDROID && SWOOVR_FCM
using Android.Gms.Extensions;
#endif

namespace Swoovr.Mobile;

/// <summary>Only platform-issued tokens reach hosted APIs. Local simulation is a separate Debug-only path.</summary>
public static class PushRegistration
{
    private const string TokenKey = "swoovr.push.token", DeviceKey = "swoovr.push.device", InstallKey = "swoovr.push.installation";
    private static readonly SemaphoreSlim registration = new(1, 1);
    private static bool signingOut;
    public static void Resume() => signingOut = false;
    public static string Status { get; private set; } = "Device push has not been enabled. Delivery is not guaranteed; check Swoovr for updates.";
    public static async Task SaveTokenAsync(string token)
    {
        await registration.WaitAsync();
        try { if (signingOut) return; await SecureStorage.Default.SetAsync(TokenKey, token); }
        catch (Exception) { Status = "The device token could not be stored securely. Check Swoovr for updates."; return; }
        finally { registration.Release(); }
        await RegisterAsync();
    }
    public static async Task RegisterAsync()
    {
        await registration.WaitAsync();
        try
        {
            if (signingOut || !await Ui.SignedInAsync()) return;
            var token = await SecureStorage.Default.GetAsync(TokenKey);
#if DEBUG && !SWOOVR_FCM && !SWOOVR_APNS
            token ??= "simulated-" + Installation();
#endif
            if (string.IsNullOrWhiteSpace(token)) return;
            var result = await Ui.Members!.RegisterDeviceAsync(DeviceInfo.Platform == DevicePlatform.iOS ? "ios" : "android", token, installationId: Installation());
            if (result.Value is { } device)
            {
                await SecureStorage.Default.SetAsync(DeviceKey, device.Id.ToString());
                Status = device.Simulated ? Swoovr.Client.NotificationText.PushSimulated : "This device is registered for push. Provider acceptance does not guarantee delivery; check Swoovr for updates.";
            }
            else Status = "This device could not be registered for push. Check Swoovr for updates.";
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException)
        { Status = "Device registration is unavailable. Check Swoovr for updates."; }
        finally { registration.Release(); }
    }
    private static Guid Installation()
    {
        if (!Guid.TryParse(Preferences.Default.Get(InstallKey, ""), out var id)) Preferences.Default.Set(InstallKey, (id = Guid.NewGuid()).ToString());
        return id;
    }
    public static async Task EnableAsync()
    {
        try
        {
#if IOS && SWOOVR_APNS
        var granted = await UserNotifications.UNUserNotificationCenter.Current.RequestAuthorizationAsync(UserNotifications.UNAuthorizationOptions.Alert | UserNotifications.UNAuthorizationOptions.Badge | UserNotifications.UNAuthorizationOptions.Sound);
        if (granted.Item1) UIKit.UIApplication.SharedApplication.RegisterForRemoteNotifications();
#elif ANDROID && SWOOVR_FCM
        if (OperatingSystem.IsAndroidVersionAtLeast(33) && await Permissions.RequestAsync<Permissions.PostNotifications>() != PermissionStatus.Granted) return;
        var result = await Firebase.Messaging.FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
        if (result is not null) await SaveTokenAsync(result.ToString()!);
#else
        await RegisterAsync();
#endif
        }
        catch (Exception) { Status = "Notifications could not be enabled on this device. Check Swoovr for updates."; }
    }
    public static async Task RemoveAsync()
    {
        signingOut = true;
        await registration.WaitAsync();
        try
        {
        try
        {
            if (Guid.TryParse(await SecureStorage.Default.GetAsync(DeviceKey), out var id) && Ui.Members is { } members) await members.RemoveDeviceAsync(id);
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException) { /* Local sign-out must still complete. */ }
        pendingPath = null;
        SecureStorage.Default.Remove(DeviceKey); SecureStorage.Default.Remove(TokenKey); Preferences.Default.Remove(InstallKey);
        Status = "Device push has not been enabled. Check Swoovr for updates.";
#if IOS && SWOOVR_APNS
        UIKit.UIApplication.SharedApplication.UnregisterForRemoteNotifications();
#elif ANDROID && SWOOVR_FCM
        try { await Firebase.Messaging.FirebaseMessaging.Instance.DeleteToken().AsAsync(); } catch (Exception) { /* Provider invalidation is best effort while offline. */ }
#endif
        }
        finally { registration.Release(); }
    }
    private static readonly SemaphoreSlim navigation = new(1, 1);
    private static string? pendingPath;
    public static async Task OpenAsync(string? path)
    {
        pendingPath = path;
        await OpenPendingAsync();
    }
    public static async Task OpenPendingAsync()
    {
        if (!await navigation.WaitAsync(0)) return;
        try
        {
        var path = pendingPath;
        // Never navigate external URLs or bypass sign-in/normal page authorization from a provider payload.
        if (Shell.Current is null || !await Ui.SignedInAsync() || path is null) return;
        pendingPath = null;
        if (path.StartsWith("/chats/", StringComparison.Ordinal) && Guid.TryParse(path[7..], out var chat)) await Ui.PushAsync(new ConversationPage(chat));
        else if (path == "/connections") await Shell.Current.GoToAsync("//ConnectionsPage");
        else if (path == "/travel") await Ui.PushAsync(new TravelPage());
        else if (path == "/me/notices") await Ui.PushAsync(new NoticesPage());
        else if (Uri.TryCreate("https://swoovr.invalid" + path, UriKind.Absolute, out var uri) && uri.AbsolutePath.StartsWith("/people/", StringComparison.Ordinal)
            && Guid.TryParse(uri.AbsolutePath[8..], out var person))
        {
            var mode = uri.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2))
                .Where(p => p.Length == 2 && p[0] == "mode").Select(p => Uri.UnescapeDataString(p[1])).FirstOrDefault();
            if (mode is null || mode.Length <= 32 && mode.All(c => c is >= 'a' and <= 'z' or '-')) await Ui.PushAsync(new PersonPage(person, mode));
        }
        }
        catch (Exception e) when (e is InvalidOperationException or HttpRequestException or TaskCanceledException)
        { Status = "Open Swoovr to view your updates."; }
        finally { navigation.Release(); }
    }
}
