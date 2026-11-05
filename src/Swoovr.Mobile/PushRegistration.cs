#if ANDROID && SWOOVR_FCM
using Android.Gms.Extensions;
#endif

namespace Swoovr.Mobile;

/// <summary>Only platform-issued tokens reach hosted APIs. Local simulation is a separate Debug-only path.</summary>
public static class PushRegistration
{
    private const string TokenKey = "swoovr.push.token", DeviceKey = "swoovr.push.device", InstallKey = "swoovr.push.installation", AskedKey = "swoovr.push.permission-asked";
    private static readonly SemaphoreSlim registration = new(1, 1);
    private static bool signingOut;
    private static bool ProviderEnabled =>
#if SWOOVR_FCM || SWOOVR_APNS
        true;
#else
        false;
#endif
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
#if SWOOVR_UAT
            if(!ProviderEnabled){Status="Device push is not enabled for this UAT build. Check Swoovr for updates.";return;}
#endif
            var token = await SecureStorage.Default.GetAsync(TokenKey);
#if SWOOVR_UAT
            if(token?.StartsWith("simulated-",StringComparison.Ordinal)==true){SecureStorage.Default.Remove(TokenKey);Status="A platform push token is required. Check Swoovr for updates.";return;}
#endif
#if DEBUG && !SWOOVR_UAT && !SWOOVR_FCM && !SWOOVR_APNS
            token ??= "simulated-" + Installation();
#endif
            if (string.IsNullOrWhiteSpace(token)) return;
            var result = await Ui.Members!.RegisterDeviceAsync(DeviceInfo.Platform == DevicePlatform.iOS ? "ios" : "android", token, installationId: Installation());
            if (result.Value is { } device)
            {
                await SecureStorage.Default.SetAsync(DeviceKey, device.Id.ToString());
                Status = "This device is registered for push. Provider acceptance does not guarantee delivery; check Swoovr for updates.";
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
    /// <summary>
    /// Android 13+ shows nothing until the app asks, while Firebase still issues a token and the device registers. So a signed-in
    /// member is asked once per install (after sign-in, or on first opening while signed in). Declining is respected:
    /// Android isn't asked again, and "Enable notifications on this device" or system settings still turn it on.
    /// </summary>
    public static async Task AskOnceAsync()
    {
#if ANDROID && SWOOVR_FCM
        try
        {
            if (!OperatingSystem.IsAndroidVersionAtLeast(33) || Preferences.Default.Get(AskedKey, false) || !await Ui.SignedInAsync()) return;
            if (await Permissions.CheckStatusAsync<Permissions.PostNotifications>() == PermissionStatus.Granted) { Preferences.Default.Set(AskedKey, true); return; }
            Preferences.Default.Set(AskedKey, true);
            await EnableAsync();
        }
        catch (Exception e) when (e is InvalidOperationException or PermissionException) { }
#else
        await Task.CompletedTask;
#endif
    }
    /// <summary>
    /// Signing out deletes this device's provider token. When the member signs in again and notifications are already allowed,
    /// fetch a fresh token and register it, so push resumes without tapping "Enable" (found on the owner's Galaxy A14: after
    /// sign-out and sign-in the device stayed unregistered). Never prompts; AskOnceAsync owns the one-time question.
    /// </summary>
    public static async Task RestoreTokenAsync()
    {
        try
        {
            if (!await Ui.SignedInAsync() || !string.IsNullOrEmpty(await SecureStorage.Default.GetAsync(TokenKey))) return;
#if ANDROID && SWOOVR_FCM
            if (OperatingSystem.IsAndroidVersionAtLeast(33) && await Permissions.CheckStatusAsync<Permissions.PostNotifications>() != PermissionStatus.Granted) return;
            var result = await Firebase.Messaging.FirebaseMessaging.Instance.GetToken().AsAsync<Java.Lang.String>();
            if (result is not null) await SaveTokenAsync(result.ToString()!);
#elif IOS && SWOOVR_APNS
            var settings = await UserNotifications.UNUserNotificationCenter.Current.GetNotificationSettingsAsync();
            if (settings.AuthorizationStatus == UserNotifications.UNAuthorizationStatus.Authorized)
                MainThread.BeginInvokeOnMainThread(UIKit.UIApplication.SharedApplication.RegisterForRemoteNotifications);
#endif
        }
        catch (Exception) { /* Best effort: "Enable notifications on this device" still registers. */ }
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
        else if (path == "/me/meeting-safety") await Ui.PushAsync(new MeetingSafetyPage());
        else if (path == "/me/notices") await Ui.PushAsync(new NoticesPage());
        // Every link the API sends (NotificationLinks): photo decisions and safety policy updates too.
        else if (path == "/me/photos") await Ui.PushAsync(new PhotosPage());
        else if (path == "/safety") await Ui.PushAsync(new SafetyPage());
        else if (path == "/safety/advice") await Ui.PushAsync(new SafetyAdvicePage());
        // Support replies (Zammad): the list, or one request by its number. The page loads it as the signed-in member, so a
        // number for someone else's request shows "This request isn't available".
        else if (path == Swoovr.Client.SupportTicketText.MemberPath) await Ui.PushAsync(new SupportPage());
        else if (path.StartsWith(Swoovr.Client.SupportTicketText.MemberPath + "/", StringComparison.Ordinal)
            && long.TryParse(path[(Swoovr.Client.SupportTicketText.MemberPath.Length + 1)..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var ticket) && ticket > 0)
            await Ui.PushAsync(new SupportTicketPage(ticket, []));
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
