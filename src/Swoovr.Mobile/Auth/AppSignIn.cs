using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>
/// Members sign in in the system browser (ASWebAuthenticationSession on iOS, Custom Tabs on Android) with
/// authorization code + PKCE (D31–D46). Keycloak issues every token; the API only validates them (D35).
/// </summary>
public sealed class AppSignIn
{
	private readonly NativeSignIn? signIn;
	private readonly NativeSession? session;

	public AppSignIn()
	{
		if (MobileEndpoints.SignIn is not { } options || MobileEndpoints.Api is not { } api) return;
		signIn = new NativeSignIn(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }, options);
		session = new NativeSession(signIn, new SecureTokenStore());
		Members = new MemberClient(new HttpClient(new BearerHandler(session) { InnerHandler = new HttpClientHandler() })
		{
			BaseAddress = api, Timeout = TimeSpan.FromSeconds(20),
		});
	}

	/// <summary>False in builds with no sign-in endpoints (release, until a hosted environment exists).</summary>
	public bool Available => signIn is not null;
	public MemberClient? Members { get; }

	public Task<bool> IsSignedInAsync() => session?.IsSignedInAsync() ?? Task.FromResult(false);

	/// <summary>Live messages from the API's hub (D143) with this phone's bearer token; null when sign-in isn't available.</summary>
	public MessagesHubClient? MessagesHub() =>
		session is null || MobileEndpoints.Api is not { } api ? null : new MessagesHubClient(new Uri(api, "hubs/messages"), () => session.AccessTokenAsync());

	/// <summary>True when signed in; false when the member closed the browser or the sign-in was refused.</summary>
	public async Task<bool> SignInAsync(bool reauthenticate = false)
	{
		if (signIn is null || session is null) return false;
		var request = await signIn.BeginAsync(reauthenticate: reauthenticate);
		WebAuthenticatorResult result;
		try
		{
			result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
			{
				Url = request.AuthorizeUri, CallbackUrl = MobileEndpoints.SignIn!.RedirectUri,
			});
		}
		catch (TaskCanceledException) { return false; }
		await session.SaveAsync(await signIn.CompleteAsync(request, result.Properties));
		PushRegistration.Resume();
		await PushRegistration.RegisterAsync();
		await PushRegistration.OpenPendingAsync();
		return true;
	}

	/// <summary>
	/// D170: after a deletion request Keycloak has already ended every session; this forgets the tokens on this phone
	/// without opening the browser again.
	/// </summary>
	public void ForgetLocally() { NativeAppearance.ForgetAccount();session?.Forget(); }

	/// <summary>Forgets the tokens on this phone first, then ends the Keycloak session in the browser.</summary>
	public async Task SignOutAsync()
	{
        NativeAppearance.ForgetAccount();
		await PushRegistration.RemoveAsync();
		if (signIn is null || session?.Forget() is not { } tokens) return;
		try
		{
			await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
			{
				Url = await signIn.EndSessionUriAsync(tokens), CallbackUrl = MobileEndpoints.SignIn!.SignedOutUri,
			});
		}
		catch (Exception error) when (error is TaskCanceledException or HttpRequestException or NativeSignInException)
		{
			// Signed out on this phone either way; the Keycloak session then ends at its own expiry.
		}
	}
}
