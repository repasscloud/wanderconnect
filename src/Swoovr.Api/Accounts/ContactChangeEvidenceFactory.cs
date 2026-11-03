using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Swoovr.Domain;
using Swoovr.Api.Auth;

namespace Swoovr.Api.Accounts;

public static class ContactChangeEvidenceFactory
{
    public static string? Session(ClaimsPrincipal user,byte[]? key)
    {
        var sid=user.FindFirstValue("sid");
        if(key is null||sid is null||sid.Length>255)return null;
        // Purpose separation prevents equality with contact lookup hashes; no raw session id/token is persisted.
        var correlated=Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(user.FindFirstValue("sub")+"\0"+sid)));
        return SessionCorrelation(user,key,correlated);
    }
    private static string SessionCorrelation(ClaimsPrincipal user,byte[] key,string correlated)=>Convert.ToHexStringLower(HMACSHA256.HashData(key,
        Encoding.UTF8.GetBytes("swoovr:contact-change-session:v1\0"+user.FindFirstValue("iss")+"\0"+correlated)));
    private static string? ProviderSession(ClaimsPrincipal user,ContactHistoryKeys keys,DateTimeOffset now)
    {
        var correlated=user.FindFirstValue("swoovr_email_request_session_sha256");
        return keys.Lookup is{}key&&EmailVerificationClocks.Read(user,now).Requested is not null&&correlated is{Length:64}&&correlated.All(char.IsAsciiHexDigit)
            ?SessionCorrelation(user,key,correlated):null;
    }
    public static ContactChangeEvidence Capture(CurrentAccount current,ClaimsPrincipal user,ContactHistoryKeys keys,DateTimeOffset now,
        string source,DateTimeOffset? requested=null,DateTimeOffset? verified=null,DateTimeOffset? activated=null)=>
        Capture(current.Account!.Id,current.Facts,current.Evidence,user,keys,now,source,requested,verified,activated);
    public static ContactChangeEvidence Capture(Guid id,ValidatedSessionFacts facts,SessionEvidence evidence,ClaimsPrincipal user,ContactHistoryKeys keys,
        DateTimeOffset now,string source,DateTimeOffset? requested=null,DateTimeOffset? verified=null,DateTimeOffset? activated=null)
    {
        var session=Session(user,keys.Lookup);
        if(source=="validated-email-claim")
        {
            (requested,verified)=EmailVerificationClocks.Read(user,now);
            return new(id,requested,verified,Micro(activated),Micro(now)!.Value,source,ProviderSession(user,keys,now),evidence.Method.ToString(),
                evidence.MultiFactor,evidence.IsFresh,facts.AuthenticatedAt,Issued(user,now),session);
        }
        return new(id,Micro(requested),Micro(verified),Micro(activated),Micro(now)!.Value,source,session,evidence.Method.ToString(),
            evidence.MultiFactor,evidence.IsFresh,facts.AuthenticatedAt,Issued(user,now));
    }
    private static DateTimeOffset? Micro(DateTimeOffset? value)=>value is{}at?new DateTimeOffset(at.UtcTicks/10*10,TimeSpan.Zero):null;
    private static DateTimeOffset? Issued(ClaimsPrincipal user,DateTimeOffset now)=>long.TryParse(user.FindFirstValue("iat"),out var seconds)
        &&seconds>0&&seconds<=now.AddSeconds(5).ToUnixTimeSeconds()?DateTimeOffset.FromUnixTimeSeconds(seconds):null;
}
