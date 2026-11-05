using System.Security.Cryptography;
using System.Text;
using Swoovr.Domain;
using Swoovr.Infrastructure.Ticketing;

internal static class TicketingChecks
{
    public static void Run()
    {
        void Check(bool value, string name) { if (!value) throw new Exception(name); }
        var id = Guid.Parse("AAAAAAAA-BBBB-CCCC-DDDD-EEEEEEEEEEEE");
        Check(TicketingPolicy.MemberLogin(id) == "swoovr-aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", "canonical identity");
        Check(!TicketingPolicy.CanAccessGroup(AccountRole.Admin, "Legal & Law Enforcement"), "admin has no implicit Legal access");
        Check(!TicketingPolicy.CanAccessGroup(AccountRole.Support, "Trust & Safety — Restricted"), "restricted support denial");
        Check(!TicketingPolicy.CanAccessGroup(AccountRole.Legal, "unknown"), "unknown grants fail closed");
        Check(TicketingPolicy.CanAccessGroup(AccountRole.Support, "General Support"), "routine support");
        var bytes = Encoding.UTF8.GetBytes("{\"ticket\":{\"id\":1}}");
        var secret = new string('a', 64);
        var signature = "sha1=" + Convert.ToHexStringLower(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), bytes));
        Check(ZammadSignature.Verify(bytes, signature, secret), "raw signature accepted");
        Check(!ZammadSignature.Verify(Encoding.UTF8.GetBytes("{}"), signature, secret), "mutated body denied");
        Check(!ZammadSignature.Verify(bytes, signature, ""), "unconfigured denied");
        Check(!ZammadSignature.Verify(bytes, signature + "," + signature, secret), "ambiguous signature denied");
        Console.WriteLine("PASS: ticketing canonical identity, fail-closed groups and raw webhook signature (9 checks).");
    }
}
