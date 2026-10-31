using Swoovr.Client;
namespace Swoovr.Mobile;

/// <summary>Nonsecret endpoints selected at build time. UAT hosting/credentials are separate operator prerequisites.</summary>
public static class MobileEndpoints
{
    public const string Scheme="com.swoovr.app";
#if SWOOVR_UAT
    public const string Environment="Uat";
    public static NativeSignInOptions? SignIn { get; }=new(new Uri("https://auth.uat.swoovr.app/realms/swoovr-members"),"swoovr-mobile",new Uri(Scheme+":/oauth2redirect"),new Uri(Scheme+":/signedout"));
    public static Uri? Api { get; }=new("https://api.uat.swoovr.app/");
#else
    public const string Environment="Development";
    public static NativeSignInOptions? SignIn { get; }=new(new Uri("http://localhost:8180/realms/swoovr-members-dev"),"swoovr-mobile",new Uri(Scheme+":/oauth2redirect"),new Uri(Scheme+":/signedout"));
#if SWOOVR_OWNED_API
    public static Uri? Api { get; }=new("http://127.0.0.1:5400/");
#else
    public static Uri? Api { get; }=new("http://127.0.0.1:5080/");
#endif
#endif
}
