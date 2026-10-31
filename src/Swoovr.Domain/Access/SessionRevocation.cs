using System.Globalization;

namespace Swoovr.Domain;

/// <summary>One issuer-skew allowance for JWT validation and conservative application session retirement.</summary>
public static class SessionRevocationPolicy
{
    public static readonly TimeSpan IssuerClockSkew=TimeSpan.FromSeconds(30);
    public static DateTimeOffset Cutoff(DateTimeOffset now)=>now.ToUniversalTime()+IssuerClockSkew;

    /// <summary>Fresh authentication AND issuance are required after a cutoff; malformed clocks never resume access.</summary>
    public static bool Refuses(DateTimeOffset? cutoff,DateTimeOffset authenticatedAt,string? issuedAt,DateTimeOffset now)
    {
        if(cutoff is null)return false;
        if(authenticatedAt<=cutoff)return true;
        if(!long.TryParse(issuedAt,NumberStyles.None,CultureInfo.InvariantCulture,out var seconds)
            ||seconds<=0||seconds>DateTimeOffset.MaxValue.ToUnixTimeSeconds())return true;
        var issued=DateTimeOffset.FromUnixTimeSeconds(seconds);
        return issued<=cutoff||issued>now+IssuerClockSkew||authenticatedAt>issued;
    }
}
