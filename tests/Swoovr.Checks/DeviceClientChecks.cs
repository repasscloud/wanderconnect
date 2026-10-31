using System.Net;
using Swoovr.Client;
static class DeviceClientChecks
{
 public static async Task Run(){
  var id=Guid.NewGuid();
  foreach(var status in new[]{204,401,503}){
   using var http=new HttpClient(new Handler(status,id)){BaseAddress=new Uri("https://api.example.test/")};
   object actual=await new MemberClient(http).RemoveDeviceAsync(id);
   if(!Equals(actual,status))throw new Exception("Device deletion must return its status without reading an empty response.");
  }
  Console.WriteLine("PASS device removal client: 3 empty-response/status fake HTTP checks; no device/provider claim.");
 }
 private sealed class Handler(int status,Guid id):HttpMessageHandler{
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   if(request.Method!=HttpMethod.Delete||request.RequestUri?.AbsolutePath!="/api/me/devices/"+id)throw new Exception("Device deletion route/method mismatch.");
   return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
  }
 }
}
