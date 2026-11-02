using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Api.Accounts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;

static class ContactChangeEvidenceChecks
{
    public static void Run()
    {
        static void Check(string label,bool ok){if(!ok)throw new Exception("Contact change evidence: "+label);}
        var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{
            ["ContactHistory:Keys:1"]=Convert.ToBase64String(Enumerable.Repeat((byte)7,32).ToArray()),
            ["ContactHistory:LookupKey"]=Convert.ToBase64String(Enumerable.Repeat((byte)11,32).ToArray())}).Build();
        using var services=new ServiceCollection().BuildServiceProvider();
        var keys=new ContactHistoryKeys(config,new Founders(services,config,new UnconfiguredEmailSender(),NullLogger<Founders>.Instance));
        var now=DateTimeOffset.FromUnixTimeSeconds(1793635200).AddTicks(12347);var id=Guid.NewGuid();
        ClaimsPrincipal User(string issuer,string? sid)=>new(new ClaimsIdentity(new[]{new Claim("iss",issuer),new Claim("sub","fictional-subject"),new Claim("iat",now.ToUnixTimeSeconds().ToString())}.Concat(sid is null?[]:new[]{new Claim("sid",sid)}),"validated-fictional"));
        var user=User("https://issuer.example.test","fictional-session");
        var facts=new ValidatedSessionFacts(AccountRealm.Members,"fictional-subject",true,["pwd","otp"],null,now.AddMinutes(-2),now.AddDays(-1));
        var receipt=ContactChangeEvidenceFactory.Capture(id,facts,new(SignInMethod.EmailPassword,true,true),user,keys,now,"phone-code",requested:now);
        Check("request clock matches PostgreSQL microsecond precision",receipt.RequestedAt!.Value.UtcTicks%10==0&&receipt.ObservedAt.UtcTicks%10==0);
        Check("session correlation is purpose-separated and never raw",receipt.RequestSessionHmac is{Length:64}&&receipt.RequestSessionHmac!="fictional-session");
        Check("session correlation binds issuer and subject",receipt.RequestSessionHmac!=ContactChangeEvidenceFactory.Session(User("https://other.example.test","fictional-session"),keys.Lookup));
        Check("missing session claim is never invented",ContactChangeEvidenceFactory.Session(User("https://issuer.example.test",null),keys.Lookup)is null);
        var encrypted=keys.SealRequest(receipt)!;
        Check("temporary request receipt roundtrips with the configured contact key",keys.OpenRequest(encrypted)==receipt);
        Check("temporary receipt is encrypted",!System.Text.Encoding.UTF8.GetString(encrypted).Contains("fictional-session"));
        encrypted[^1]^=1;Check("tampered receipt refuses activation",keys.OpenRequest(encrypted)is null);
        Check("missing legacy receipt remains unknown",keys.OpenRequest(null)is null&&keys.OpenRequest([])is null);
        var email=ContactChangeEvidenceFactory.Capture(id,facts,new(SignInMethod.EmailPassword,true,true),user,keys,now,"validated-email-claim",activated:now);
        Check("email observations never fabricate provider request or verification clocks",email.RequestedAt is null&&email.VerifiedAt is null&&email.ActivatedAt is not null);
        const string address="Case.Exact@example.test";
        var bound=EmailVerificationClocks.Binding("fictional-subject",address);
        ClaimsPrincipal Provider(string binding,string requested,string verified)=>new(new ClaimsIdentity(user.Claims.Concat(new[]{new Claim("email",address),new Claim("email_verified","true"),
            new Claim("swoovr_email_request_binding",binding),new Claim("swoovr_email_verified_binding",binding),new Claim("swoovr_email_requested_ms",requested),new Claim("swoovr_email_verified_ms",verified)}),"validated-fictional"));
        var req=now.AddMinutes(-2).ToUnixTimeMilliseconds().ToString();var done=now.AddSeconds(-1).ToUnixTimeMilliseconds().ToString();
        var clocks=EmailVerificationClocks.Read(Provider(bound,req,done),now);
        Check("signed provider clocks bind exact current email and subject",clocks.Requested==DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(req))&&clocks.Verified==DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(done)));
        Check("stale binding cannot decorate another current email",EmailVerificationClocks.Read(Provider(EmailVerificationClocks.Binding("fictional-subject",address.ToLowerInvariant()),req,done),now)==(null,null));
        Check("impossible provider clock ordering is rejected",EmailVerificationClocks.Read(Provider(bound,done,req),now)==(null,null));
        Check("future provider clocks remain unknown",EmailVerificationClocks.Read(Provider(bound,now.AddDays(1).ToUnixTimeMilliseconds().ToString(),now.AddDays(2).ToUnixTimeMilliseconds().ToString()),now)==(null,null));
        var verifiedReceipt=ContactChangeEvidenceFactory.Capture(id,facts,new(SignInMethod.EmailPassword,true,true),Provider(bound,req,done),keys,now,"validated-email-claim",activated:now);
        Check("provider clock receipt distinguishes unknown upstream request session from observed API activation session",verifiedReceipt.RequestSessionHmac is null&&verifiedReceipt.ActivationSessionHmac is{Length:64}&&verifiedReceipt.VerifiedAt==clocks.Verified);
        var sourceSession=Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("fictional-subject\0fictional-session")));
        var requestedSession=new ClaimsPrincipal(new ClaimsIdentity(Provider(bound,req,done).Claims.Concat(new[]{new Claim("swoovr_email_request_session_sha256",sourceSession)}),"validated-fictional"));
        var tied=ContactChangeEvidenceFactory.Capture(id,facts,new(SignInMethod.EmailPassword,true,true),requestedSession,keys,now,"validated-email-claim",activated:now);
        Check("trusted provider request session correlates through a keyed hash without raw session storage",tied.RequestSessionHmac==tied.ActivationSessionHmac&&tied.RequestSessionHmac!=sourceSession);
        Console.WriteLine("PASS bounded encrypted contact change receipts: exact observed clocks, keyed session correlation, scope, tamper refusal and provider-clock uncertainty.");
    }
}
