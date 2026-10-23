using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Swoovr.Mobile;

/// <summary>Receives Keycloak's redirect to the app's private-use scheme (DEFAULT) and hands it to WebAuthenticator.</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView], Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable], DataScheme = MobileEndpoints.Scheme)]
public class WebAuthenticationCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
