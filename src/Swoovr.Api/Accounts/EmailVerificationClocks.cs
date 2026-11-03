using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Swoovr.Api.Accounts;

/// <summary>DEFAULT pinned provider contract: event clocks bound to the exact current email and validated issuer/subject.</summary>
public static class EmailVerificationClocks
{
    public static string Binding(string subject,string email)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(subject+"\0"+email)));
    public static (DateTimeOffset? Requested,DateTimeOffset? Verified) Read(ClaimsPrincipal user,DateTimeOffset now)
    {
        var subject=user.FindFirstValue("sub");var email=user.FindFirstValue("email");
        if(subject is null||subject.Length>255||email is null||email.Length>254||user.FindFirstValue("email_verified")!="true")return(null,null);
        var binding=Binding(subject,email);
        DateTimeOffset? Clock(string claim,string bound)
        {
            if(user.FindFirstValue(bound)!=binding||!long.TryParse(user.FindFirstValue(claim),NumberStyles.None,CultureInfo.InvariantCulture,out var milliseconds)
                ||milliseconds<=0||milliseconds>now.ToUnixTimeMilliseconds())return null;
            if(long.TryParse(user.FindFirstValue("iat"),NumberStyles.None,CultureInfo.InvariantCulture,out var issued)
                &&issued>0&&issued<DateTimeOffset.MaxValue.ToUnixTimeSeconds()&&milliseconds>issued*1000+999)return null;
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }
        var requested=Clock("swoovr_email_requested_ms","swoovr_email_request_binding");
        var verified=Clock("swoovr_email_verified_ms","swoovr_email_verified_binding");
        return requested>verified?(null,null):(requested,verified);
    }
}
