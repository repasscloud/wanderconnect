using System.Net;
using System.Text;
namespace Swoovr.Hosting;

public enum HostKind { Api,Web,Admin }
/// <summary>Server-only linked source: fixed diagnostics contain names/codes, never configured values. Provider factories remain authoritative.</summary>
public static class HostingPolicy
{
    public static IReadOnlyList<string> Validate(HostKind host,string environment,IReadOnlyDictionary<string,string?> configuration,bool serving=true)
    {
        if(string.Equals(environment,"Development",StringComparison.OrdinalIgnoreCase))return [];
        var errors=new List<string>();string? Read(string key)=>configuration.GetValueOrDefault(key);bool Has(string key)=>!string.IsNullOrWhiteSpace(Read(key));
        void Require(string key){if(!Has(key))errors.Add("required:"+key);}
        bool Flag(string key,bool fallback=false){var text=Read(key);if(text is null)return fallback;if(bool.TryParse(text,out var value))return value;errors.Add("invalid-boolean:"+key);return fallback;}
        void Url(string key,bool required=true){if(!Has(key)&&!required)return;if(!SecureUrl(Read(key)))errors.Add("unsafe-url:"+key);}
        void Choice(string key,string expected){if(Read(key)!=expected)errors.Add("required-choice:"+key);}
        void Base64(string key,int minimum=32){try{var bytes=Convert.FromBase64String(Read(key)??"");if(bytes.Length<minimum||minimum==32&&bytes.Length!=32)errors.Add("invalid-key:"+key);}catch(FormatException){errors.Add("invalid-key:"+key);}}
        void Ring(string prefix,int maximum=255,bool longKey=false)
        {
            var entries=configuration.Where(p=>p.Key.StartsWith(prefix+":",StringComparison.OrdinalIgnoreCase)&&p.Value is not null).ToArray();
            if(entries.Length==0){errors.Add("required:"+prefix);return;}
            foreach(var entry in entries){var suffix=entry.Key[(prefix.Length+1)..];if(!int.TryParse(suffix,out var version)||version<1||version>maximum){errors.Add("invalid-key-version:"+prefix);continue;}try{var bytes=Convert.FromBase64String(entry.Value!);if(longKey?bytes.Length<32:bytes.Length!=32)errors.Add("invalid-key:"+prefix);}catch(FormatException){errors.Add("invalid-key:"+prefix);}}
        }
        if(string.IsNullOrWhiteSpace(environment))errors.Add("environment-required");
        if(!Enum.IsDefined(host))errors.Add("host-kind-required");
        foreach(var key in new[]{"Staff:LocalPasswords","Prototype:Enabled","Evidence:HoldCheckUnavailable","DetailedErrors"})if(Flag(key))errors.Add("development-only:"+key);
        foreach(var key in new[]{"Media:Screening:Simulate","Media:Publication:BarrierDirectory","Media:Publication:BarrierStage","Media:Preservation:Fault"})if(Has(key))errors.Add("development-only:"+key);
        if(!Flag("Keycloak:RequireHttpsMetadata",true))errors.Add("https-metadata-required");
        if(!Flag("Media:Screening:RequireKnownContentCheck",true))errors.Add("known-content-check-required");
        foreach(var pair in new[]{("Sms:Provider","simulated"),("Push:Provider","simulated"),("Email:Provider","smtp"),("Media:Store","local")})if(Read(pair.Item1)==pair.Item2)errors.Add("development-only:"+pair.Item1);
        if(!serving)return errors.Distinct(StringComparer.Ordinal).ToArray();
        Url("Hosting:PublicOrigin");
        if(Uri.TryCreate(Read("Hosting:PublicOrigin"),UriKind.Absolute,out var origin)&&(origin.AbsolutePath!="/"||origin.Query.Length>0||origin.Fragment.Length>0))errors.Add("public-origin-required");
        var hosts=(Read("AllowedHosts")??"").Split(';',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(hosts.Length==0||hosts.Any(h=>h.Contains('*'))||origin is null||!hosts.Contains(origin.Host,StringComparer.OrdinalIgnoreCase))errors.Add("explicit-allowed-hosts-required");
        var networks=(Read("Hosting:TrustedProxyNetworks")??"").Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(networks.Length==0||networks.Any(n=>!IPNetwork.TryParse(n,out var network)||network.PrefixLength==0))errors.Add("trusted-proxy-networks-required");
        if(host is HostKind.Web or HostKind.Admin)
        {
            Url("Api:BaseUrl");Url("Keycloak:Authority");Require("Keycloak:ClientId");Require("Keycloak:ClientSecret");
            var realm=host==HostKind.Web?"swoovr-members":"swoovr-staff";
            if(!Uri.TryCreate(Read("Keycloak:Authority"),UriKind.Absolute,out var authority)||authority.AbsolutePath.TrimEnd('/')!="/realms/"+realm)errors.Add("hosted-authority-realm-required");
            return errors.Distinct(StringComparer.Ordinal).ToArray();
        }
        Require("ConnectionStrings:Swoovr");Url("Keycloak:BaseUrl");
        foreach(var realm in new[]{"Members","Venues","Staff"})if(Read("Keycloak:"+realm+"Realm")!="swoovr-"+realm.ToLowerInvariant())errors.Add("hosted-realm:Keycloak:"+realm+"Realm");
        Require("Keycloak:StaffSsoAlias");Require("MemberAdmin:ClientId");Require("MemberAdmin:ClientSecret");
        foreach(var key in new[]{"Phone:Required","Retention:Worker","Media:Screening:Worker"})if(!Flag(key,true))errors.Add("required-enabled:"+key);
        Choice("Email:Provider","mailersend");Require("Email:MailerSend:ApiToken");Require("Email:From");
        if(Has("Sms:Twilio:BaseUrl")&&Uri.TryCreate(Read("Sms:Twilio:BaseUrl"),UriKind.Absolute,out var smsUri)&&!(smsUri.Scheme=="https"&&smsUri.AbsolutePath=="/"&&(smsUri.Host=="api.twilio.com"||smsUri.Host.StartsWith("api.",StringComparison.Ordinal)&&smsUri.Host.EndsWith(".twilio.com",StringComparison.Ordinal))))errors.Add("twilio-api-origin-required");
        Choice("Sms:Provider","twilio");foreach(var key in new[]{"AccountSid","AuthToken","From"})Require("Sms:Twilio:"+key);Url("Sms:Twilio:BaseUrl",false);
        var push=Read("Push:Provider")??"disabled";if(push is not("disabled" or "providers"))errors.Add("unknown:Push:Provider");
        if(push=="providers"&&!Flag("Push:Apns:Enabled")&&!Flag("Push:Fcm:Enabled"))errors.Add("push-platform-required");
        Choice("Media:Store","r2");foreach(var key in new[]{"Bucket","AccessKeyId","SecretAccessKey"})Require("Media:R2:"+key);Url("Media:R2:Endpoint");Url("Media:Screening:BaseUrl");
        foreach(var key in new[]{"ApiKey","WorkflowId","WebhookSecret"})Require("Didit:"+key);Url("Didit:BaseUrl",false);
        foreach(var key in new[]{"Moderation:EscalationEmail","Moderation:EscalationBackupEmail"})Require(key);
        if(Encoding.UTF8.GetByteCount(Read("Founders:Secrets:1")??"")<32)errors.Add("invalid-key:Founders:Secrets:1");
        if(Encoding.UTF8.GetByteCount(Read("Phone:CodeKey")??"")<32)errors.Add("invalid-key:Phone:CodeKey");
        Ring("Bans:HmacKeys",int.MaxValue,longKey:true);Ring("ContactHistory:Keys");Base64("ContactHistory:LookupKey");Ring("Evidence:Keys");Ring("IdentityStore:Keys");Base64("IdentityStore:HmacKey");Ring("Push:TokenKeys",int.MaxValue);Base64("Push:TokenLookupKey");
        foreach(var feature in new[]{"MeetingSafety","TextHarm"})if(Flag(feature+":Enabled"))
        {if(!Flag(feature+":Worker",true))errors.Add("required-enabled:"+feature+":Worker");if(feature=="MeetingSafety")Url("MeetingSafety:PublicBaseUrl");}
        return errors.Distinct(StringComparer.Ordinal).ToArray();
    }
    public static bool SecureUrl(string? value)
    {
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||uri.IsLoopback||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)return false;
        return !uri.Host.Equals("mailpit",StringComparison.OrdinalIgnoreCase)&&!uri.Host.EndsWith(".localhost",StringComparison.OrdinalIgnoreCase)&&!uri.Host.EndsWith(".local",StringComparison.OrdinalIgnoreCase)&&!uri.AbsolutePath.Split('/').Any(s=>s.EndsWith("-dev",StringComparison.OrdinalIgnoreCase));
    }
}
