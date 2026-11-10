using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swoovr.Api;
using Swoovr.Domain;
using System.Net;
using System.Text;
using System.Text.Json;
using Swoovr.Infrastructure.Email;

/// <summary>#316: Twilio Email API adapter against a fake HTTP handler, the hosted SMTP relay and Mailpit sink selection.
/// Fictional values only; no outbound request.</summary>
static class TwilioEmailChecks
{
    public static async Task Run()
    {
        var count=0;void Check(string name,bool ok){if(!ok)throw new Exception(name);count++;}
        var message=new EmailMessage("fictional@example.test","Fictional subject","Line <one> & more\nsecond line\n\nNew paragraph");
        var options=new TwilioEmailOptions("SKfictional0000","fictional-secret-never-log","Swoovr <sender@example.test>");
        var calls=0;
        using(var client=new HttpClient(new Fake(async(request,token)=>
        {
            calls++;
            Check("Twilio email canonical HTTPS endpoint",request.Method==HttpMethod.Post&&request.RequestUri!.AbsoluteUri=="https://comms.twilio.com/v1/Emails");
            Check("Twilio email Basic key SID and secret",request.Headers.Authorization?.Scheme=="Basic"&&Encoding.ASCII.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!))=="SKfictional0000:fictional-secret-never-log");
            Check("Twilio email JSON media type",request.Content!.Headers.ContentType?.MediaType=="application/json");
            using var payload=JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var root=payload.RootElement;
            Check("Twilio email sender address and required name",root.GetProperty("from").GetProperty("address").GetString()=="sender@example.test"&&root.GetProperty("from").GetProperty("name").GetString()=="Swoovr");
            Check("Twilio email one recipient",root.GetProperty("to").GetArrayLength()==1&&root.GetProperty("to")[0].GetProperty("address").GetString()==message.To);
            var content=root.GetProperty("content");
            Check("Twilio email subject and text",content.GetProperty("subject").GetString()==message.Subject&&content.GetProperty("text").GetString()==message.Text);
            Check("Twilio email HTML is the escaped text",content.GetProperty("html").GetString()=="<!doctype html><html><body><p>Line &lt;one&gt; &amp; more<br>second line</p><p>New paragraph</p></body></html>");
            return Response(202,"{\"operationId\":\"comms_operation_01h9krwprkeee8fzqspvwy6nq8\"}");
        })))
        {
            var outcome=await new TwilioEmailSender(client,options).SendOutcomeAsync(message,default);
            Check("Twilio email 202 with operationId is acceptance only",outcome.Status=="accepted"&&outcome.Code=="provider-accepted");
            Check("Twilio email sends exactly once",calls==1);
        }
        Check("Twilio email options redact runtime material",options.ToString()=="TwilioEmailOptions(redacted)");
        Check("Twilio email sender without a name is sent as Swoovr",new TwilioEmailOptions("SK1","secret","sender@example.test").FromName=="Swoovr");
        foreach(var (status,expected) in new[]{(200,"unknown"),(201,"unknown"),(204,"unknown"),(302,"unknown"),(400,"rejected"),(401,"rejected"),(403,"rejected"),(404,"rejected"),(413,"rejected"),(422,"rejected"),(429,"unknown"),(500,"unknown"),(502,"unknown"),(503,"unknown")})
        {
            var attempts=0;
            using var http=new HttpClient(new Fake((_,_)=>{attempts++;return Task.FromResult(Response(status,"provider-private-error-"+message.To));}));
            var result=await new TwilioEmailSender(http,options).SendOutcomeAsync(message,default);
            Check("Twilio email explicit outcome for HTTP "+status,result.Status==expected&&attempts==1);
            Check("Twilio email fixed codes never reflect the destination",!result.ToString().Contains(message.To,StringComparison.Ordinal));
        }
        foreach(var body in new[]{"","not-json","[]","{}","{\"operationId\":\"\"}","{\"operationId\":\"bad id with spaces\"}","{\"operationId\":42}"})
        {
            using var http=new HttpClient(new Fake((_,_)=>Task.FromResult(Response(202,body))));
            Check("Twilio email 202 without a valid operationId stays unknown",(await new TwilioEmailSender(http,options).SendOutcomeAsync(message,default)).Status=="unknown");
        }
        using(var http=new HttpClient(new Fake((_,_)=>Task.FromResult(Response(202,"{\"operationId\":\""+new string('a',9000)+"\"}")))))
            Check("Twilio email oversized acknowledgement stays unknown",(await new TwilioEmailSender(http,options).SendOutcomeAsync(message,default)).Status=="unknown");
        using(var http=new HttpClient(new Fake((_,_)=>throw new HttpRequestException("fictional private failure "+message.To))))
            Check("Twilio email transport failure is unknown, never retried blindly",(await new TwilioEmailSender(http,options).SendOutcomeAsync(message,default)).Status=="unknown");
        using(var http=new HttpClient(new Fake((_,_)=>throw new Exception("must not be called"))))
        {
            Check("Twilio email invalid recipient refused before HTTP",(await new TwilioEmailSender(http,options).SendOutcomeAsync(message with{To="not an address"},default)).Status=="rejected");
            Check("Twilio email control characters in subject refused before HTTP",(await new TwilioEmailSender(http,options).SendOutcomeAsync(message with{Subject="a\nb"},default)).Status=="rejected");
        }
        foreach(var (sid,secret) in new[]{("","secret"),("SK1",""),("SK 1","secret"),("SK1","se:cret")})
        {var refused=false;try{_=new TwilioEmailOptions(sid,secret,"sender@example.test");}catch(ArgumentException){refused=true;}Check("Twilio email incomplete or unsafe key refused",refused);}

        IConfiguration Config(Dictionary<string,string?> values)=>new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        IEmailSender Resolve(Dictionary<string,string?> values,bool development,string environment)
        {var services=new ServiceCollection();services.AddLogging();services.AddSwoovrApplicationEmail(Config(values),development,environment);return services.BuildServiceProvider().GetRequiredService<IEmailSender>();}
        bool Refused(Dictionary<string,string?> values,bool development,string environment)
        {try{Resolve(values,development,environment);return false;}catch(Exception error)when(error is ArgumentException or InvalidOperationException){return true;}}
        var twilio=new Dictionary<string,string?>{["Email:Provider"]="twilio",["Email:Twilio:KeySid"]="SKfictional",["Email:Twilio:KeySecret"]="fictional",["Email:From"]="Swoovr <sender@example.test>"};
        Check("Hosted Twilio email resolves the Twilio adapter",Resolve(twilio,false,"Uat") is TwilioEmailSender);
        twilio.Remove("Email:Twilio:KeySecret");Check("Twilio email without its secret refuses startup",Refused(twilio,false,"Uat"));
        var relay=new Dictionary<string,string?>{["Email:Provider"]="smtp",["Email:Smtp:Host"]="smtp.example.test",["Email:Smtp:Port"]="587",["Email:Smtp:Username"]="apikey",["Email:Smtp:Password"]="fictional",["Email:From"]="sender@example.test"};
        Check("Hosted SMTP relay resolves an authenticated, non-sink sender",Resolve(relay,false,"Production") is SmtpEmailSender{IsSink:false});
        foreach(var (key,value) in new[]{("Email:Smtp:Host","mailpit"),("Email:Smtp:Host","127.0.0.1"),("Email:Smtp:Port","0"),("Email:Smtp:Password",null),("Email:From",null)})
        {var copy=new Dictionary<string,string?>(relay){[key]=value};Check("Unsafe or incomplete SMTP relay refused: "+key,Refused(copy,false,"Production"));}
        var sink=new Dictionary<string,string?>{["Email:Provider"]="mailpit",["Mailpit:Host"]="mailpit",["Email:From"]="sender@example.test"};
        Check("UAT Mailpit email resolves a sink",Resolve(sink,false,"Uat") is SmtpEmailSender{IsSink:true});
        Check("Production refuses the Mailpit email sink",Refused(sink,false,"Production"));
        Check("Mailpit email sink must target mailpit or loopback",Refused(new(sink){["Mailpit:Host"]="smtp.example.test"},false,"Uat"));

        async Task<string?> Sms(string environment,Dictionary<string,string?> values)
        {
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName=environment});builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(values);builder.Services.AddSingleton<IEmailSender,UnconfiguredEmailSender>();builder.Services.AddSwoovrSms(builder.Configuration);
            await using var app=builder.Build();
            try{return app.Services.GetRequiredService<IVerificationMessageSender>().Kind;}catch(InvalidOperationException){return null;}
        }
        var smsSink=new Dictionary<string,string?>{["Sms:Provider"]="mailpit",["Mailpit:Host"]="mailpit"};
        Check("UAT Mailpit SMS is the labelled simulated sender",await Sms("Uat",smsSink)=="simulated");
        Check("Production refuses the Mailpit SMS sink",await Sms("Production",smsSink) is null);
        Console.WriteLine($"PASS Twilio email, SMTP relay and Mailpit sinks: {count} fake HTTP/DI checks; no outbound request or delivery claim.");
    }

    private static HttpResponseMessage Response(int status,string body="")=>new((HttpStatusCode)status){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    private sealed class Fake(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> send):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>send(request,cancellationToken);
    }
}
