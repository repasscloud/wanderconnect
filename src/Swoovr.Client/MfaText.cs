namespace Swoovr.Client;
public static class MfaText
{
    public static string Status(string state)=>state switch{"enabled"=>"Enabled","disabled"=>"Disabled","required"=>"Required","setup-pending"=>"Setup pending",_=>"Unavailable"};
    public static string Method(string method)=>method switch{"otp"=>"Authenticator app","recovery-authn-codes"=>"Recovery codes",_=>"Other authenticator"};
}
