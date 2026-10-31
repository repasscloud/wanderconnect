using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Swoovr.Infrastructure.Messaging;
static class SmsTransportChecks
{
    public static async Task Run()
    {
        var count=0;var options=new TwilioOptions("ACfictional0000","fictional-private-token","+15005550006",new Uri("https://api.twilio.com/"));
        var sid="SM"+new string('a',32);string Ack(string state)=>"{\"sid\":\""+sid+"\",\"status\":\""+state+"\"}";
        async Task Check(HttpStatusCode status,string body,bool accepted)
        {
            var handler=new Handler(()=>new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")});var logger=new PrivateLogger();using var http=new HttpClient(handler);
            var actual=await new TwilioMessageSender(http,options,logger).SendCodeAsync("+15005550009","123456",default);
            if(actual!=accepted||handler.Calls!=1)throw new Exception("SMS acceptance/single-attempt contract failed: "+(int)status);
            if(logger.Messages.Any(m=>m.Contains("123456")||m.Contains("15005550009")||m.Contains("fictional-private-token")||m.Contains(sid)))throw new Exception("Private SMS detail reached logs.");count++;
        }
        // Unexpected 2xx statuses must not be accepted as a created message.
        foreach(var status in new[]{HttpStatusCode.OK,HttpStatusCode.Accepted,HttpStatusCode.NoContent,HttpStatusCode.Redirect,HttpStatusCode.BadRequest,HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden,HttpStatusCode.TooManyRequests,HttpStatusCode.InternalServerError,HttpStatusCode.ServiceUnavailable})await Check(status,Ack("queued"),false);
        foreach(var state in new[]{"accepted","queued","sending","sent","delivered"})await Check(HttpStatusCode.Created,Ack(state),true);
        foreach(var body in new[]{"","{broken","[]","{}","{\"sid\":\"invalid\",\"status\":\"queued\"}","{\"sid\":\""+sid+"\"}",Ack("failed"),Ack("undelivered"),Ack("canceled"),new string('x',8193)})await Check(HttpStatusCode.Created,body,false);
        {var handler=new Handler(()=>throw new HttpRequestException("fictional-private-token 123456 +15005550009"));var logger=new PrivateLogger();using var http=new HttpClient(handler);if(await new TwilioMessageSender(http,options,logger).SendCodeAsync("+15005550009","123456",default)||handler.Calls!=1||logger.Messages.Any(m=>m.Contains("123456")||m.Contains("fictional-private-token")))throw new Exception("SMS unknown failure privacy/retry contract.");count++;}
        Console.WriteLine($"PASS Twilio acceptance transport: {count} fake HTTP/private-log checks; acceptance is not recipient delivery; no external request.");
    }
    private sealed class Handler(Func<HttpResponseMessage> reply):HttpMessageHandler
    {public int Calls;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;return Task.FromResult(reply());}}
    private sealed class PrivateLogger:ILogger<TwilioMessageSender>
    {
        public List<string> Messages{get;}=[];public IDisposable? BeginScope<TState>(TState state)where TState:notnull=>null;public bool IsEnabled(LogLevel level)=>true;
        public void Log<TState>(LogLevel level,EventId id,TState state,Exception? error,Func<TState,Exception?,string> formatter)=>Messages.Add(formatter(state,error));
    }
}
