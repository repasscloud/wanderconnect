using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Domain;
using Swoovr.Infrastructure.Identity;

static class IdentityDirectoryChecks
{
    public static async Task Run()
    {
        var count = 0;
        void Check(bool ok,string name) { if(!ok) throw new Exception("identity directory: "+name);count++; }
        using var provider = new DirectoryProvider(); using var http = new HttpClient(provider);
        IIdentityDirectory identity = new KeycloakStaffIdentityAdmin(http,new("https://auth.example.test","swoovr-staff-test","swoovr-api-staff","fictional"),TimeProvider.System,NullLogger<KeycloakIdentityAdmin>.Instance);
        var subject = provider.Subject.ToString();
        var info = await identity.AccountAsync(subject,CancellationToken.None);
        Check(info is { Enabled:true,EmailVerified:true } && info.CredentialTypes.SequenceEqual(["otp","password"]),"metadata omits credential secrets");
        var update = new IdentityAccountUpdate("Fictional","Employee","changed@example.test","+61400000000",false);
        Check(await identity.UpdateAccountAsync(subject,update,CancellationToken.None),"staff metadata update confirmed");
        Check(provider.Written.GetProperty("emailVerified").GetBoolean()==false,"email change never asserts verified ownership");
        Check(provider.Written.GetProperty("requiredActions").EnumerateArray().Select(e=>e.GetString()).Contains("VERIFY_EMAIL"),"email change requires verification");
        Check(provider.Written.GetProperty("requiredActions").EnumerateArray().Select(e=>e.GetString()).Contains("TERMS_AND_CONDITIONS"),"unrelated actions preserved");
        Check(provider.Written.GetProperty("attributes").GetProperty("unrelated")[0].GetString()=="preserve","unrelated attributes preserved");
        Check(!provider.Written.TryGetProperty("credentials",out _),"a profile write never echoes credentials");
        Check(await identity.RequireActionsAsync(subject,["CONFIGURE_TOTP"],CancellationToken.None),"MFA action added");
        Check(provider.Written.GetProperty("requiredActions").GetArrayLength()==2,"MFA action preserves unrelated action");
        var created = await identity.CreateAccountAsync(update,CancellationToken.None);
        Check(created==subject && !provider.Written.GetProperty("enabled").GetBoolean(),"creation returns confirmed provider id and stays disabled");
        Check(!provider.Written.GetProperty("emailVerified").GetBoolean() && !provider.Written.TryGetProperty("credentials",out _),"no invented verification or password");
        provider.Fail=true;
        Check(await identity.AccountAsync(subject,CancellationToken.None) is null,"provider outage is not an empty successful status");
        Check(!await identity.UpdateAccountAsync(subject,update,CancellationToken.None),"provider update fails closed");
        Check(await identity.CreateAccountAsync(update,CancellationToken.None) is null,"unconfirmed creation grants no guessed id");
        var calls=provider.Calls;
        Check(await identity.AccountAsync("../users",CancellationToken.None) is null && provider.Calls==calls,"malformed subject causes no HTTP call");
        Console.WriteLine($"PASS: staff identity directory ({count} fake HTTP checks; no live provider claim).");
    }
    private sealed class DirectoryProvider : HttpMessageHandler
    {
        public Guid Subject {get;}=Guid.NewGuid(); public bool Fail; public int Calls; public JsonElement Written;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Calls++;var path=request.RequestUri!.AbsolutePath;
            if(path.EndsWith("/token"))return Json(new{access_token="fictional-token",expires_in=300});
            if(Fail)return new(HttpStatusCode.ServiceUnavailable);
            if(request.Method==HttpMethod.Get)
            {
                if(path.EndsWith("/credentials"))return Json(new[]{new{id=Guid.NewGuid(),type="otp",secretData="must-never-echo"},new{id=Guid.NewGuid(),type="password",secretData="must-never-echo"}});
                return Json(new{id=Subject.ToString(),firstName="Fictional",lastName="Staff",email="fictional@example.test",emailVerified=true,enabled=true,
                    attributes=new Dictionary<string,string[]>{{"unrelated",["preserve"]}},requiredActions=new[]{"TERMS_AND_CONDITIONS"},credentials=new[]{new{type="password",secretData="must-never-echo"}}});
            }
            Written=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            var response=new HttpResponseMessage(request.Method==HttpMethod.Post?HttpStatusCode.Created:HttpStatusCode.NoContent);
            if(request.Method==HttpMethod.Post)response.Headers.Location=new Uri("https://auth.example.test/admin/realms/swoovr-staff-test/users/"+Subject);
            return response;
        }
        private static HttpResponseMessage Json(object body)=>new(HttpStatusCode.OK){Content=JsonContent.Create(body)};
    }
}
