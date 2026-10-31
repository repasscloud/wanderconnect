using System.Security.Cryptography;
using System.Text;
namespace Swoovr.Contracts;

/// <summary>Opaque cache/session precondition only. It grants no access; the server still authenticates every call.</summary>
public static class AppearanceOwner
{
    public const string Header="Swoovr-Appearance-Owner";
    public static string ForIdentity(string issuer,string subject)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(issuer+"\0"+subject)));
    public static bool Valid(string? value)=>value is{Length:64}&&value.All(c=>c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
