import java.io.*;
import java.net.*;
import java.net.http.*;
import java.time.Duration;
/** Private stdin/stdout protocol used only by the isolated Keycloak test driver. No logging or external network. */
class HttpProbe {
 public static void main(String[] args)throws Exception{
  var input=new BufferedReader(new InputStreamReader(System.in,java.nio.charset.StandardCharsets.UTF_8));
  var uri=URI.create(input.readLine());if(!uri.getHost().matches("swoovr-uat-image-[a-f0-9]{12}-kc")||!uri.getScheme().equals("http")||uri.getPort()!=8080)throw new IllegalArgumentException("Owned test target required.");
  String method=input.readLine(),contentType=input.readLine(),token=input.readLine(),body=input.readLine();
  var request=HttpRequest.newBuilder(uri).timeout(Duration.ofSeconds(25));
  if(!contentType.isEmpty())request.header("Content-Type",contentType);if(!token.isEmpty())request.header("Authorization","Bearer "+token);
  request.method(method,body==null||body.isEmpty()?HttpRequest.BodyPublishers.noBody():HttpRequest.BodyPublishers.ofString(body));
  try(var client=HttpClient.newBuilder().followRedirects(HttpClient.Redirect.NEVER).build()){
   var response=client.send(request.build(),HttpResponse.BodyHandlers.ofString());System.out.println(response.statusCode());System.out.print(response.body());
  }
 }
}
