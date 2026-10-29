using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Swoovr.Contracts;
using Swoovr.Domain;
using Swoovr.Infrastructure.Identity;

static class ContactIdentityChecks
{
    public static async Task Run()
    {
        var checks=0;void Check(bool value,string message){if(!value)throw new Exception("Contact provider: "+message);checks++;}
        using var handler=new Fixture();using var http=new HttpClient(handler);
        var contract=new ContactPolicyContract(ApiContract.Header,ApiContract.Initial,Fixture.Issuer,true);
        IContactIdentityAdmin provider=new KeycloakIdentityAdmin(http,new("https://provider.example.test","swoovr-members-test","swoovr-api-accounts","fictional-service-secret"),TimeProvider.System,NullLogger<KeycloakIdentityAdmin>.Instance,contract);
        var read=await provider.ReadEmailBindingAsync(handler.Subject,CancellationToken.None);
        Check(read.State==IdentityContactState.Available&&read.Binding?.Username==Fixture.Username,"raw snapshot preserves unrelated login");
        Check(read.Binding?.Email==Fixture.Email,"SDK never normalizes exact provider snapshot");
        var operation=Guid.NewGuid();
        Check(await provider.RetireEmailBindingAsync(read.Binding!,operation,CancellationToken.None)==IdentityContactState.Retired,"attempt confirmed through separate current-state read");
        Check(handler.StatusReads>0&&handler.LastBody.Contains("expectedUsername"),"confirmation and raw request fields are explicit");
        Check(handler.Paths.All(p=>!p.Contains('@')&&!p.Contains("?")),"no queried contact in any URI");
        handler.Recorded=false;handler.SaveOnPost=false;
        Check(await provider.RetireEmailBindingAsync(read.Binding!,Guid.NewGuid(),CancellationToken.None)!=IdentityContactState.Retired,"HTTP200 alone never falsely completes retirement");
        handler.Recorded=false;handler.SaveOnPost=true;handler.ThrowAfterPost=true;
        Check(await provider.RetireEmailBindingAsync(read.Binding!,Guid.NewGuid(),CancellationToken.None)==IdentityContactState.Retired,"ambiguous transport outcome reconciles known committed operation");
        handler.ThrowAfterPost=false;handler.WrongOperation=true;
        Check(await provider.EmailRetirementStatusAsync(handler.Subject,handler.Operation,CancellationToken.None)==IdentityContactState.Unavailable,"wrong opaque operation cannot confirm completion");
        handler.WrongOperation=false;handler.BadVersion=true;
        Check((await provider.ReadEmailBindingAsync(handler.Subject,CancellationToken.None)).State==IdentityContactState.Unavailable,"bad date response refuses raw data");
        handler.BadVersion=false;handler.WrongIssuer=true;
        Check((await provider.ReadEmailBindingAsync(handler.Subject,CancellationToken.None)).State==IdentityContactState.Unavailable,"different authority cannot be mistaken for source issuer");
        handler.WrongIssuer=false;
        handler.BadSchema=true;
        Check((await provider.ReadEmailBindingAsync(handler.Subject,CancellationToken.None)).State==IdentityContactState.Unavailable,"malformed capability schema is unavailable, not an unhandled exception");
        handler.BadSchema=false;handler.BadSubject=true;
        Check((await provider.ReadEmailBindingAsync(handler.Subject,CancellationToken.None)).State==IdentityContactState.Unavailable,"wrong provider subject cannot become source snapshot");
        handler.BadSubject=false;handler.Oversize=true;
        Check((await provider.ReadEmailBindingAsync(handler.Subject,CancellationToken.None)).State==IdentityContactState.Unavailable,"bounded provider JSON refuses oversized response");
        handler.Oversize=false;
        using(var cancelled=new CancellationTokenSource()) {
            cancelled.Cancel();var refused=false;
            try{await provider.ReadEmailBindingAsync(handler.Subject,cancelled.Token);}catch(OperationCanceledException){refused=true;}
            Check(refused,"caller cancellation remains cancellation, not an invented completed effect");
        }

        using var disabledHttp=new HttpClient(handler);
        IContactIdentityAdmin disabled=new KeycloakIdentityAdmin(disabledHttp,new("https://provider.example.test","swoovr-members-test","swoovr-api-accounts","fictional-service-secret"),TimeProvider.System,NullLogger<KeycloakIdentityAdmin>.Instance,contract with {Enabled=false});
        var before=handler.Paths.Count;
        Check(!disabled.ContactRetirementConfigured&&(await disabled.ReadEmailBindingAsync(handler.Subject,CancellationToken.None)).State==IdentityContactState.NotSupported,"explicitly disabled provider is unavailable");
        Check(handler.Paths.Count==before,"disabled mode never silently falls back to standard user PUT");
        Console.WriteLine($"PASS: contact provider adapter ({checks} fake HTTP checks; no real provider/authorization claim).");
    }
    private sealed class Fixture:HttpMessageHandler
    {
        public const string Issuer="https://auth.example.test/realms/swoovr-members-test",Email=" Fictional.Member+tag@Example.Test ",Username="unrelated-fictional-login";
        public Guid Subject=Guid.NewGuid(),Operation;public bool Recorded,SaveOnPost=true,ThrowAfterPost,WrongOperation,BadVersion,WrongIssuer,BadSchema,BadSubject,Oversize;
        public int StatusReads;public string LastBody="";public List<string> Paths=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var path=request.RequestUri!.AbsolutePath;Paths.Add(path);
            if(path.EndsWith("/token"))return new(HttpStatusCode.OK){Content=JsonContent.Create(new{access_token="fictional-service-access-token",expires_in=300})};
            if(!request.Headers.TryGetValues(ApiContract.Header,out var versions)||versions.Single()!=ApiContract.Initial)throw new Exception("Missing canonical provider date.");
            HttpResponseMessage Reply(HttpStatusCode status,object body){var response=new HttpResponseMessage(status){Content=JsonContent.Create(body)};response.Headers.Add(ApiContract.Header,BadVersion?"2099-01-01":ApiContract.Initial);return response;}
            if(path.EndsWith("/capabilities")) {
                if(BadSchema)return Reply(HttpStatusCode.OK,new{issuer=Issuer,realm="swoovr-members-test",schema="unexpected",retireEmail=true});
                if(Oversize)return Reply(HttpStatusCode.OK,new{issuer=Issuer,realm="swoovr-members-test",schema=1,retireEmail=true,padding=new string('x',64000)});
                return Reply(HttpStatusCode.OK,new{issuer=WrongIssuer?"https://other.example.test/realms/swoovr-members-test":Issuer,realm="swoovr-members-test",keycloak="26.8.1",schema=1,retireEmail=true});
            }
            if(path.EndsWith("/email-binding"))return Reply(HttpStatusCode.OK,new{userId=BadSubject?Guid.NewGuid():Subject,email=Email,username=Username,emailVerified=true,enabled=true});
            if(request.Method==HttpMethod.Post){LastBody=await request.Content!.ReadAsStringAsync(token);using var body=JsonDocument.Parse(LastBody);Operation=body.RootElement.GetProperty("operationId").GetGuid();Recorded=SaveOnPost;if(ThrowAfterPost)throw new HttpRequestException("Fictional ambiguous secret/contact detail must never be logged.");return Reply(HttpStatusCode.OK,new{status="retired",userId=Subject,operationId=Operation});}
            StatusReads++;
            return Recorded?Reply(HttpStatusCode.OK,new{status="retired",userId=Subject,operationId=WrongOperation?Guid.NewGuid():Operation}):Reply(HttpStatusCode.NotFound,new{code="retirement-not-found"});
        }
    }
}
