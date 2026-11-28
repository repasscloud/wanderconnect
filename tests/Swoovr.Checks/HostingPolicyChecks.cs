using Swoovr.Hosting;
static class HostingPolicyChecks
{
    public static void Run()
    {
        var count=0;void Expect(bool ok,string name){if(!ok)throw new Exception(name);count++;}
        static Dictionary<string,string?> Api()=>new(StringComparer.OrdinalIgnoreCase){
            ["Hosting:PublicOrigin"]="https://api-uat.swoovr.app",["Hosting:TrustedProxyNetworks"]="10.42.0.0/23",["AllowedHosts"]="api-uat.swoovr.app",["Email:From"]="Swoovr <no-reply@example.test>",["Moderation:EscalationEmail"]="safety@example.test",["Moderation:EscalationBackupEmail"]="backup@example.test",["ConnectionStrings:Swoovr"]="fictional-connection-validated-by-database-adapter",["Keycloak:BaseUrl"]="https://auth-uat.swoovr.app",["Keycloak:MembersRealm"]="swoovr-members",["Keycloak:VenuesRealm"]="swoovr-venues",["Keycloak:StaffRealm"]="swoovr-staff",["Keycloak:StaffSsoAlias"]="entra",
            ["Email:Provider"]="mailersend",["Email:MailerSend:ApiToken"]="fictional-provider-validated-separately",["Sms:Provider"]="twilio",["Sms:Twilio:AccountSid"]="fictional",["Sms:Twilio:AuthToken"]="fictional",["Sms:Twilio:From"]="+15005550006",["Push:Provider"]="disabled",
            ["Media:Store"]="r2",["Media:R2:Endpoint"]="https://objects.example.test",["Media:R2:Bucket"]="fictional-private",["Media:R2:AccessKeyId"]="fictional",["Media:R2:SecretAccessKey"]="fictional",["Media:Screening:BaseUrl"]="https://screening.internal.example.test",["Media:Screening:ApiKey"]=new('s',48),["Attachments:Scanning:ClamAv:Host"]="clamav.internal.example.test",
            ["Didit:ApiKey"]="fictional",["Didit:Callbacks:Web"]="https://uat.swoovr.app/me/verification/return",["Didit:WorkflowId"]="fictional",["Didit:WebhookSecret"]="fictional",["MemberAdmin:ClientId"]="swoovr-api-accounts",["MemberAdmin:ClientSecret"]="fictional",
            ["Founders:Secrets:1"]=new('a',64),["Phone:CodeKey"]=new('b',44),["Bans:HmacKeys:1"]=Convert.ToBase64String(new byte[32]),["ContactHistory:Keys:1"]=Convert.ToBase64String(new byte[32]),["ContactHistory:LookupKey"]=Convert.ToBase64String(new byte[32]),["Evidence:Keys:1"]=Convert.ToBase64String(new byte[32]),["IdentityStore:Keys:1"]=Convert.ToBase64String(new byte[32]),["IdentityStore:HmacKey"]=Convert.ToBase64String(new byte[32]),["Push:TokenKeys:1"]=Convert.ToBase64String(new byte[32]),["Push:TokenLookupKey"]=Convert.ToBase64String(new byte[32]),["Media:LinkKey"]=Convert.ToBase64String(new byte[32]),["Location:Keys:1"]=Convert.ToBase64String(new byte[32]),["Accounts:HoldKey"]=new('h',48)};
        static string[] Errors(Dictionary<string,string?> c,HostKind h=HostKind.Api,string env="Uat",bool serving=true)=>HostingPolicy.Validate(h,env,c,serving).ToArray();
        Expect(Errors(Api()).Length==0,"complete fictional API configuration passes pure policy only");
        foreach(var env in new[]{"Uat","Production","Staging","development-typo"})foreach(var host in Enum.GetValues<HostKind>())
        {var c=Api();c["Staff:LocalPasswords"]="true";Expect(Errors(c,host,env).Contains("development-only:Staff:LocalPasswords"),"local staff refusal on every hosted host/environment");}
        foreach(var pair in new[]{("Prototype:Enabled","true"),("Evidence:HoldCheckUnavailable","true"),("DetailedErrors","true"),("Media:Screening:Simulate","blocked"),("Media:Screening:RequireKnownContentCheck","false"),("Media:Publication:BarrierDirectory","/fictional"),("Media:Preservation:Fault","fictional"),("Keycloak:RequireHttpsMetadata","false"),("Phone:Required","false"),("Retention:Worker","false"),("Media:Screening:Worker","false"),("Sms:Provider","simulated"),("Push:Provider","simulated"),("Email:Provider","smtp"),("Media:Store","local")})
        {var c=Api();c[pair.Item1]=pair.Item2;Expect(Errors(c).Length>0,"unsafe setting refused: "+pair.Item1);Expect(Errors(c,env:"Development").Length==0,"Development fixtures preserved");}
        foreach(var key in new[]{"ConnectionStrings:Swoovr","Keycloak:BaseUrl","Keycloak:StaffSsoAlias","Didit:ApiKey","MemberAdmin:ClientSecret","Founders:Secrets:1","Phone:CodeKey","Bans:HmacKeys:1","ContactHistory:Keys:1","ContactHistory:LookupKey","Evidence:Keys:1","IdentityStore:Keys:1","IdentityStore:HmacKey","Push:TokenKeys:1","Push:TokenLookupKey","Media:LinkKey","Location:Keys:1","Accounts:HoldKey"})
        {var c=Api();c.Remove(key);Expect(Errors(c).Length>0,"missing critical configuration refused: "+key);}
        foreach(var value in new[]{"http://auth.example.test","https://localhost","https://mailpit","https://auth.example.test/realms/swoovr-members-dev","https://user:secret@auth.example.test","https://auth.example.test/?private=value"})
        {var c=Api();c["Keycloak:BaseUrl"]=value;Expect(Errors(c).Contains("unsafe-url:Keycloak:BaseUrl"),"unsafe hosted authority refused");}
        {var c=Api();c["Keycloak:MembersRealm"]="swoovr-members-dev";Expect(Errors(c).Contains("hosted-realm:Keycloak:MembersRealm"),"development realm refused");c=Api();c["Phone:Required"]="yes";Expect(Errors(c).Contains("invalid-boolean:Phone:Required"),"invalid boolean fails closed");}
        foreach(var key in new[]{"ContactHistory:Keys:1","ContactHistory:LookupKey","Evidence:Keys:1","IdentityStore:HmacKey","Media:LinkKey","Location:Keys:1"}){var c=Api();c[key]="not-base64";Expect(Errors(c).Length>0,"invalid key bytes refused");}
        foreach(var h in new[]{HostKind.Web,HostKind.Admin})
        {var c=new Dictionary<string,string?>{{"Hosting:PublicOrigin","https://uat.swoovr.app"},{"Hosting:TrustedProxyNetworks","10.42.0.0/23"},{"AllowedHosts","uat.swoovr.app"},{"Keycloak:Authority","https://auth-uat.swoovr.app/realms/"+(h==HostKind.Web?"swoovr-members":"swoovr-staff")},{"Keycloak:ClientId",h==HostKind.Web?"swoovr-web":"swoovr-admin-web"},{"Keycloak:ClientSecret","fictional"},{"Api:BaseUrl","https://api.internal.example.test/"}};Expect(Errors(c,h).Length==0,"complete fictional portal policy");c["Api:BaseUrl"]="http://localhost:5080";Expect(Errors(c,h).Contains("unsafe-url:Api:BaseUrl"),"portal never defaults to local API in hosted mode");}
        foreach(var value in new[]{"0.0.0.0/0","::/0","not-a-network"}){var c=Api();c["Hosting:TrustedProxyNetworks"]=value;Expect(Errors(c).Contains("trusted-proxy-networks-required"),"unbounded/invalid proxy trust refused");}
        {var c=Api();c["AllowedHosts"]="*";Expect(Errors(c).Contains("explicit-allowed-hosts-required"),"wildcard hosts refused");c=Api();c["Hosting:PublicOrigin"]="https://api-uat.swoovr.app/path";Expect(Errors(c).Contains("public-origin-required"),"public origin cannot carry path");}
        // Issue 488 (D413): ID images need the notice in force and named hosts.
        {string[] With(string hosts,string notice,string env="Production"){var c=Api();c["Didit:IdImages:Enabled"]="true";c["Didit:IdImages:Hosts"]=hosts;return HostingPolicy.Validate(HostKind.Api,env,c,true,notice,new(2026,12,10)).ToArray();}
         Expect(With("media.example.test","0.21, 3 December 2026").Length==0,"ID images with the notice in force");
         Expect(With("media.example.test","0.20, 17 November 2026").SequenceEqual(["notice-required:Didit:IdImages:Enabled"])&&With("media.example.test","0.21, 11 December 2026").Length==1,"not before the notice");
         foreach(var bad in new[]{"localhost","127.0.0.1","*.example.test","https://media.example.test","media.example.test:8443","a.localhost",""})Expect(With(bad,"0.21, 3 December 2026").Length>0,"image host refused: "+bad);
         // D414: UAT is private (the owner's own ID only), so it may run before the notice, and with no host yet to learn its name.
         Expect(With("media.example.test","0.20, 17 November 2026","Uat").Length==0&&With("","0.20, 17 November 2026","Uat").Length==0,"UAT before the notice");
         foreach(var env in new[]{"Production","Staging","uat-typo"})Expect(With("media.example.test","0.20, 17 November 2026",env).Contains("notice-required:Didit:IdImages:Enabled"),"never elsewhere: "+env);
         foreach(var bad in new[]{"localhost","127.0.0.1","*.example.test"})Expect(With(bad,"0.20, 17 November 2026","Uat").Length>0,"UAT image host refused: "+bad);}
        // D427 (#530): Photo Verified needs notice 0.22 in force; private UAT may run it first (as D414).
        {string[] With(string notice,string env="Production"){var c=Api();c["Verification:PhotoVerified:Enabled"]="true";return HostingPolicy.Validate(HostKind.Api,env,c,true,notice,new(2026,12,10)).ToArray();}
         Expect(With("0.22, 3 December 2026").Length==0,"Photo Verified with notice 0.22 in force");
         Expect(With("0.21, 26 November 2026").SequenceEqual(["notice-required:Verification:PhotoVerified:Enabled"])&&With("0.22, 11 December 2026").Length==1,"not before notice 0.22");
         Expect(With("0.21, 26 November 2026","Uat").Length==0,"private UAT before the notice");
         {var c=Api();Expect(Errors(c).Length==0,"off by default needs nothing");}
         // D453: the notice in force now is 0.22, so production may turn it on from 28 November 2026.
         Expect(Swoovr.Contracts.PrivacyNoticeVersion.CoversPhotoVerification(Swoovr.Contracts.PrivacyNoticeVersion.Current,new(2026,11,28)),"0.22 is in force");}
        // D336/D337: scanning always on with a scanner; a configured helpdesk needs its retention identity.
        {var c=Api();c["Attachments:Scanning:Enabled"]="false";Expect(Errors(c).Contains("required-enabled:Attachments:Scanning:Enabled"),"attachment scanning can't be switched off");Expect(Errors(c,env:"Development").Length==0,"Development may switch scanning off");}
        {var c=Api();c["Attachments:Scanning:Worker"]="false";Expect(Errors(c).Contains("required-enabled:Attachments:Scanning:Worker"),"scan worker required");}
        {var c=Api();c.Remove("Attachments:Scanning:ClamAv:Host");Expect(Errors(c).Contains("required:Attachments:Scanning:ClamAv:Host"),"scanner host required");}
        {var c=Api();c["Ticketing:Zammad:BaseUrl"]="https://help.example.test";Expect(Errors(c).Contains("required:Ticketing:Zammad:RetentionToken"),"helpdesk needs a retention identity");c["Ticketing:Zammad:RetentionToken"]="fictional";Expect(Errors(c).Length==0,"retention identity satisfies the helpdesk rule");}
        // Photo screening: a private single-label service name (the UAT single server) or HTTPS, and always its key.
        {var c=Api();c["Media:Screening:BaseUrl"]="http://image-moderation:8080";Expect(Errors(c).Length==0,"private screening service on the same network passes");}
        foreach(var value in new[]{"http://image-moderation","http://localhost:8080","http://127.0.0.1:8080","http://screening.example.test:8080","http://10.0.0.5:8080","http://user:secret@image-moderation:8080","http://image-moderation:8080/v1","http://image-moderation:8080/?k=v","ftp://image-moderation:8080"})
        {var c=Api();c["Media:Screening:BaseUrl"]=value;Expect(Errors(c).Contains("unsafe-url:Media:Screening:BaseUrl"),"unsafe screening address refused: "+value);}
        {var c=Api();c.Remove("Media:Screening:ApiKey");Expect(Errors(c).Contains("invalid-key:Media:Screening:ApiKey"),"screening key required");c["Media:Screening:ApiKey"]="short";Expect(Errors(c).Contains("invalid-key:Media:Screening:ApiKey"),"short screening key refused");Expect(Errors(c,env:"Development").Length==0,"Development may screen without a key");}
        // Issue 407: founder1 must be hex of at least 32 bytes, as Founders.ReadSecrets reads it; the old generated password is refused.
        foreach(var value in new[]{"aBcD1234efGH5678ijKL9012mnOP3456qrST7890uvWX1234yzAB","a"+new string('b',63)+"z",new string('a',62)," "})
        {var c=Api();c["Founders:Secrets:1"]=value;Expect(Errors(c).Contains("invalid-key:Founders:Secrets:1"),"non-hex or short founder1 refused: "+value.Length);}
        {var c=Api();c["Founders:Secrets:1"]=Convert.ToHexString(new byte[32]).ToLowerInvariant();Expect(!Errors(c).Contains("invalid-key:Founders:Secrets:1"),"lower-case 64-hex founder1 passes");c["Founders:Secrets:1"]=" "+Convert.ToHexString(new byte[48])+" ";Expect(!Errors(c).Contains("invalid-key:Founders:Secrets:1"),"longer upper-case founder1 passes");}
        Expect(Swoovr.Hosting.HostingPolicy.FounderKey(new string('a',64)) is {Length:32}&&Swoovr.Hosting.HostingPolicy.FounderKey("zz")==null,"founder key parser");
        // Issue 315: Twilio SMS takes a restricted API key (both parts, SK SID) or the Auth Token.
        {var c=Api();if(c.GetValueOrDefault("Sms:Provider")=="twilio"){c.Remove("Sms:Twilio:AuthToken");Expect(Errors(c).Contains("required:Sms:Twilio:AuthToken"),"Twilio needs the Auth Token without an API key");
            c["Sms:Twilio:ApiKeySid"]="SK"+new string('c',32);Expect(Errors(c).Contains("required:Sms:Twilio:ApiKeySecret"),"an API key SID needs its secret");
            c["Sms:Twilio:ApiKeySecret"]="fictional-key-secret";Expect(!Errors(c).Any(e=>e.Contains("Sms:Twilio",StringComparison.Ordinal)),"an API key replaces the Auth Token");
            c["Sms:Twilio:ApiKeySid"]="AC"+new string('c',32);Expect(Errors(c).Contains("invalid-key:Sms:Twilio:ApiKeySid"),"the API key SID starts with SK");}}
        // Issue 438: the Didit return page is explicit and public; never missing or localhost on a hosted API.
        {var c=Api();c.Remove("Didit:Callbacks:Web");Expect(Errors(c).Contains("unsafe-url:Didit:Callbacks:Web"),"missing Didit web callback refused");Expect(Errors(c,env:"Development").Length==0,"Development reads its local callback from appsettings");}
        foreach(var value in new[]{"http://localhost:5081/me/verification/return","https://localhost/me/verification/return","http://uat.swoovr.app/me/verification/return"})
        {var c=Api();c["Didit:Callbacks:Web"]=value;Expect(Errors(c).Contains("unsafe-url:Didit:Callbacks:Web"),"unsafe Didit web callback refused: "+value);}
        Expect(Errors(new(),serving:false).Length==0,"minimal one-shot command does not demand unrelated live services");
        // #316: Twilio email, hosted SMTP relay, and the Mailpit sink only on UAT.
        // #357: Postmark email needs its server token.
        {var c=Api();c["Email:Provider"]="postmark";Expect(Errors(c).Contains("required:Email:Postmark:ServerToken"),"Postmark email needs its token");c["Email:Postmark:ServerToken"]="fictional";Expect(!Errors(c).Any(e=>e.Contains("Email:",StringComparison.Ordinal)),"Postmark email with its token passes");}
        {var c=Api();c["Email:Provider"]="twilio";Expect(Errors(c).Contains("required:Email:Twilio:KeySid")&&Errors(c).Contains("required:Email:Twilio:KeySecret"),"Twilio email needs its key");c["Email:Twilio:KeySid"]="SKfictional";c["Email:Twilio:KeySecret"]="fictional";Expect(Errors(c).Length==0,"complete Twilio email passes");c.Remove("Email:From");Expect(Errors(c).Contains("required:Email:From"),"every provider needs a sender");}
        {var c=Api();c["Email:Provider"]="smtp";Expect(Errors(c).Contains("development-only:Email:Provider"),"SMTP without credentials stays a development sink");c["Email:Smtp:Host"]="smtp.example.test";c["Email:Smtp:Port"]="587";c["Email:Smtp:Username"]="apikey";c["Email:Smtp:Password"]="fictional";Expect(Errors(c).Length==0,"complete SMTP relay passes");c["Email:Smtp:Host"]="mailpit";Expect(Errors(c).Contains("hosted-smtp-relay-required"),"relay cannot target the sink");}
        foreach(var key in new[]{"Email:Provider","Sms:Provider"})
        {var c=Api();c[key]="mailpit";c["Mailpit:Host"]="mailpit";Expect(Errors(c).Length==0,"UAT may use the Mailpit sink: "+key);Expect(Errors(c,env:"Production").Contains("uat-only:"+key),"Production refuses the Mailpit sink: "+key);Expect(Errors(c,env:"Production",serving:false).Contains("uat-only:"+key),"one-shot Production commands refuse it too: "+key);c.Remove("Mailpit:Host");Expect(Errors(c).Contains("required:Mailpit:Host"),"sink host required: "+key);}
        {var c=Api();c["Sms:Provider"]="simulated";Expect(Errors(c).Contains("development-only:Sms:Provider"),"UAT still refuses the development simulated sender");}
        Console.WriteLine($"PASS hosted configuration policy: {count} pure checks; no provider/network/hosted readiness claim.");
    }
}
