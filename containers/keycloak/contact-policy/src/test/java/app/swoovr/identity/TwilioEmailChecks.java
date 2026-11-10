package app.swoovr.identity;

import com.sun.net.httpserver.HttpServer;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.keycloak.email.EmailException;
import java.net.*;
import java.net.http.HttpClient;
import java.nio.charset.StandardCharsets;
import java.util.*;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

/** Real Java Twilio adapter against a loopback HTTP fake (#316). No provider/mailbox/delivery claim. */
public final class TwilioEmailChecks {
    private static int checks;
    private static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);checks++;}
    public static void main(String[] args)throws Exception{
        var server=HttpServer.create(new InetSocketAddress("127.0.0.1",0),0);
        var handlerFailure=new AtomicReference<Throwable>();var calls=new AtomicInteger();var status=new AtomicInteger(202);
        var body=new String[]{"{\"operationId\":\"comms_operation_01h9krwprkeee8fzqspvwy6nq8\"}"};var hasText=new boolean[]{true};var givenHtml=new boolean[]{true};
        var mapper=new ObjectMapper();
        server.createContext("/v1/Emails",exchange->{
            calls.incrementAndGet();
            try{
                check(exchange.getRequestMethod().equals("POST"),"POST contract");
                var expected="Basic "+Base64.getEncoder().encodeToString("SKfictional:fictional-private-secret".getBytes(StandardCharsets.US_ASCII));
                check(expected.equals(exchange.getRequestHeaders().getFirst("Authorization")),"Basic key SID and secret");
                var value=mapper.readTree(exchange.getRequestBody());
                check(value.path("from").path("address").asText().equals("no-reply@example.test")&&value.path("from").path("name").asText().equals("Swoovr"),"fixed sender with required name");
                check(value.path("to").size()==1&&value.path("to").get(0).path("address").asText().equals("fictional.adult@example.test"),"one recipient");
                var content=value.path("content");
                check(content.path("subject").asText().equals("Verify your email"),"subject");
                check(content.has("text")==hasText[0],"text template retained only when supplied");
                check(content.path("html").asText().contains(givenHtml[0]?"<a>fictional-link</a>":"<p>fictional-link &amp; more</p>"),"HTML template, or escaped text when Keycloak supplies none");
                var bytes=body[0].getBytes(StandardCharsets.UTF_8);exchange.sendResponseHeaders(status.get(),bytes.length==0?-1:bytes.length);exchange.getResponseBody().write(bytes);
            }catch(Throwable error){handlerFailure.set(error);}finally{exchange.close();}
        });server.start();
        try(var client=HttpClient.newBuilder().followRedirects(HttpClient.Redirect.NEVER).build()){
            var sender=new TwilioEmailSender("SKfictional","fictional-private-secret","no-reply@example.test","Swoovr",client,URI.create("http://127.0.0.1:"+server.getAddress().getPort()+"/v1/Emails"));
            sender.send(Map.of(),"fictional.adult@example.test","Verify your email","fictional-link","<a>fictional-link</a>");check(calls.get()==1,"single accepted send");
            hasText[0]=true;givenHtml[0]=false;sender.send(Map.of(),"fictional.adult@example.test","Verify your email","fictional-link & more",null);checks++;
            hasText[0]=false;givenHtml[0]=true;sender.send(Map.of(),"fictional.adult@example.test","Verify your email",null,"<a>fictional-link</a>");checks++;
            hasText[0]=true;
            for(int code:new int[]{200,201,302,400,401,403,429,500,503}){status.set(code);int before=calls.get();refused(sender);check(calls.get()==before+1,"no automatic replay");}
            status.set(202);
            for(String reply:new String[]{"","not-json","{}","{\"operationId\":\"\"}","{\"operationId\":\"bad id\"}","{\"operationId\":7}","x".repeat(8193)}){body[0]=reply;refused(sender);}
            int before=calls.get();
            try{sender.send(Map.of(),"invalid\r\naddress","subject","text","html");throw new AssertionError("invalid recipient accepted");}catch(EmailException expected){check(calls.get()==before,"invalid input refused before HTTP");}
            for(var bad:new String[][]{{"","secret"},{"SK1",""},{"SK1","se:cret"},{"SK 1","secret"}}){
                try{new TwilioEmailSender(bad[0],bad[1],"no-reply@example.test","Swoovr",client,URI.create("http://127.0.0.1/"));throw new AssertionError("unsafe key accepted");}catch(IllegalArgumentException expected){checks++;}
            }
            try{new TwilioEmailSender("SK1","secret","Swoovr <no-reply@example.test>","Swoovr",client,URI.create("http://127.0.0.1/"));throw new AssertionError("display name in sender accepted");}catch(IllegalArgumentException expected){checks++;}
        }finally{server.stop(0);}
        if(handlerFailure.get()!=null)throw new AssertionError("Loopback request contract assertion failed.",handlerFailure.get());
        System.out.println("PASS Keycloak Twilio email: "+checks+" loopback HTTP assertions; no real provider/mailbox/delivery evidence.");
    }
    private static void refused(TwilioEmailSender sender)throws Exception{
        try{sender.send(Map.of(),"fictional.adult@example.test","Verify your email","fictional-link","<a>fictional-link</a>");throw new AssertionError("unconfirmed provider response accepted");}
        catch(EmailException expected){check(!expected.getMessage().contains("fictional")&&!expected.getMessage().contains("example.test"),"private failure text");}
    }
}
