using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Identity;

/// <summary>Explicit opt-in probe against the integration runner's owned loopback provider, never a configurable hosted realm.</summary>
static class ContactIdentityProviderProbe
{
    public static async Task Run()
    {
        string Required(string key)=>Environment.GetEnvironmentVariable(key) is {Length:>0} value?value:throw new Exception("Missing owned-provider fixture input.");
        var baseUri=new Uri(Required("SWOOVR_TEST_CONTACT_BASE"));
        var realm=Required("SWOOVR_TEST_CONTACT_REALM");
        if(!baseUri.IsLoopback||baseUri.Scheme!=Uri.UriSchemeHttp||realm!="swoovr-contact-retirement-test")
            throw new Exception("Contact probe accepts only the owned loopback fixture realm.");
        var subject=Guid.Parse(Required("SWOOVR_TEST_CONTACT_SUBJECT"));
        var operation=Guid.Parse(Required("SWOOVR_TEST_CONTACT_OPERATION"));
        using var handler=new SocketsHttpHandler{AllowAutoRedirect=false};using var http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(15)};
        IContactIdentityAdmin provider=new KeycloakIdentityAdmin(http,new(baseUri.ToString().TrimEnd('/'),realm,"swoovr-api-accounts",Required("SWOOVR_TEST_CONTACT_SECRET")),TimeProvider.System,
            NullLogger<KeycloakIdentityAdmin>.Instance,new(ApiContract.Header,ApiContract.Initial,baseUri.ToString().TrimEnd('/')+"/realms/"+realm,true));
        var checks=0;void Check(bool value,string message){if(!value)throw new Exception("Owned provider adapter: "+message);checks++;}
        var read=await provider.ReadEmailBindingAsync(subject,CancellationToken.None);
        Check(read.State==IdentityContactState.Available&&read.Binding is not null,"genuine raw provider snapshot available");
        Check(read.Binding!.Subject==subject&&read.Binding.Email?.EndsWith("@example.test",StringComparison.Ordinal)==true,"only fictional expected source read");
        Check(await provider.EmailRetirementStatusAsync(subject,operation,CancellationToken.None)==IdentityContactState.NotRecorded,"missing receipt does not confirm completion");
        Check(await provider.RetireEmailBindingAsync(read.Binding,operation,CancellationToken.None)==IdentityContactState.Retired,"real provider attempt confirmed through a separate current-state read");
        Check(await provider.EmailRetirementStatusAsync(subject,operation,CancellationToken.None)==IdentityContactState.Retired,"committed status remains explicit");
        Check(await provider.RetireEmailBindingAsync(read.Binding,operation,CancellationToken.None)==IdentityContactState.Retired,"same opaque command retries through typed adapter");
        var after=await provider.ReadEmailBindingAsync(subject,CancellationToken.None);
        Check(after.State==IdentityContactState.Available&&after.Binding!.Email=="retired."+operation+"@swoovr.invalid"&&!after.Binding.EmailVerified,"reserved sink is never verified ownership");
        Console.WriteLine($"PASS: typed contact adapter against owned real Keycloak ({checks} checks; no app authorization/browser/ownership/delivery claim).");
    }
}
