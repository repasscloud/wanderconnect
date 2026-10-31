using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Swoovr.Api;
using Swoovr.Domain;
using Swoovr.Infrastructure.Email;
static class SmsSelectionChecks
{
    public static async Task Run()
    {
        var count=0;
        static async Task<string> Selected(string environment,string? selected, bool credentials=false,string? endpoint=null)
        {
            var builder=WebApplication.CreateBuilder(new WebApplicationOptions{EnvironmentName=environment});builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{["Sms:Provider"]=selected,["Sms:Twilio:AccountSid"]=credentials?"ACfictional0000":null,["Sms:Twilio:AuthToken"]=credentials?"fictional":null,["Sms:Twilio:From"]=credentials?"+15005550006":null,["Sms:Twilio:BaseUrl"]=endpoint});
            builder.Services.AddSingleton<IEmailSender,UnconfiguredEmailSender>();builder.Services.AddSwoovrSms(builder.Configuration);await using var app=builder.Build();var sender=app.Services.GetRequiredService<IVerificationMessageSender>();
            if(sender.Kind=="disabled"&&await sender.SendCodeAsync("+15005550006","123456",default))throw new Exception("Disabled SMS claimed acceptance.");return sender.Kind;
        }
        foreach(var scenario in new[]{("Development",(string?)null,"simulated"),("Uat",(string?)null,"disabled"),("Development",(string?)"disabled","disabled"),("Uat",(string?)"disabled","disabled")})
        {if(await Selected(scenario.Item1,scenario.Item2)!=scenario.Item3)throw new Exception("SMS selection fallback mismatch.");count++;}
        foreach(var scenario in new[]{("Uat","simulated",false,(string?)null),("Development","twilio",false,(string?)null),("Development","unknown",false,(string?)null),("Uat","twilio",true,(string?)"http://api.twilio.com/"),("Uat","twilio",true,(string?)"https://third-party.example.test/")})
        {var refused=false;try{await Selected(scenario.Item1,scenario.Item2,scenario.Item3,scenario.Item4);}catch(InvalidOperationException){refused=true;}if(!refused)throw new Exception("Unsafe SMS selection accepted.");count++;}
        if(await Selected("Uat","twilio",true)!="twilio")throw new Exception("Explicit complete Twilio selection failed.");count++;
        Console.WriteLine($"PASS SMS provider selection: {count} DI/config checks; no outbound request/delivery.");
    }
}
