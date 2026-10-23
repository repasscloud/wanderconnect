using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>
/// Where the app signs in and which API it calls. Debug builds use the local stack on localhost: the iOS simulator
/// shares the Mac's loopback, and the Android emulator reaches it through `adb reverse` (DEFAULT), so tokens keep the
/// issuer the API trusts. Release builds have no endpoints yet (no hosted environment, D39), so sign-in stays off.
/// </summary>
public static class MobileEndpoints
{
	public const string Scheme = "com.swoovr.app";

#if DEBUG
	public static NativeSignInOptions? SignIn { get; } = new(
		new Uri("http://localhost:8180/realms/swoovr-members-dev"), "swoovr-mobile",
		new Uri(Scheme + ":/oauth2redirect"), new Uri(Scheme + ":/signedout"));
	public static Uri? Api { get; } = new("http://127.0.0.1:5080/");
#else
	public static NativeSignInOptions? SignIn => null;
	public static Uri? Api => null;
#endif
}
