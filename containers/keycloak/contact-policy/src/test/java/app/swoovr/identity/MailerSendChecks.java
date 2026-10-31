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

/** Real Java adapter against a loopback HTTP fake. No provider/mailbox/delivery claim. */
public final class MailerSendChecks {
    private static int checks;
    private static void check(boolean ok,String message){if(!ok)throw new AssertionError(message);checks++;}
    public static void main(String[] args)throws Exception{
        var server=HttpServer.create(new InetSocketAddress("127.0.0.1",0),0);
        var handlerFailure=new AtomicReference<Throwable>();var calls=new AtomicInteger();var status=new AtomicInteger(202);var ack=new String[]{"fictional_ack-1"};var body=new String[]{""};
        var formats=new boolean[]{true,true};var slow=new boolean[]{false};
        var mapper=new ObjectMapper();
        server.createContext("/v1/email",exchange->{
            calls.incrementAndGet();
            try{
                check(exchange.getRequestMethod().equals("POST"),"POST contract");
                check("Bearer fictional-private-token".equals(exchange.getRequestHeaders().getFirst("Authorization")),"private bearer contract");
                var value=mapper.readTree(exchange.getRequestBody());
                check(value.path("from").path("email").asText().equals("no-reply@example.test"),"fixed configured sender");
                check(value.path("to").get(0).path("email").asText().equals("fictional.adult@example.test"),"recipient serialization");
                check(value.has("text")==formats[0]&&value.has("html")==formats[1],"Keycloak optional body formats retained");
                if(formats[0])check(value.path("text").asText().contains("fictional-link"),"text template retained");
                if(formats[1])check(value.path("html").asText().contains("fictional-link"),"HTML template retained");
                if(ack[0]!=null)exchange.getResponseHeaders().add("x-message-id",ack[0]);
                var bytes=body[0].getBytes(StandardCharsets.UTF_8);exchange.sendResponseHeaders(status.get(),bytes.length==0?-1:bytes.length);
                if(slow[0]){exchange.getResponseBody().write(bytes,0,1);exchange.getResponseBody().flush();try{Thread.sleep(16000);}catch(InterruptedException e){Thread.currentThread().interrupt();}}
                else exchange.getResponseBody().write(bytes);
            }catch(Throwable error){handlerFailure.set(error);}finally{exchange.close();}
        });server.start();
        try(var client=HttpClient.newBuilder().followRedirects(HttpClient.Redirect.NEVER).build()){
            var sender=new MailerSendEmailSender("fictional-private-token","no-reply@example.test","Swoovr",client,URI.create("http://127.0.0.1:"+server.getAddress().getPort()+"/v1/email"));
            sender.send(Map.of(),"fictional.adult@example.test","Verify your email","fictional-link","<a>fictional-link</a>");check(calls.get()==1,"single accepted send");
            formats[1]=false;sender.send(Map.of(),"fictional.adult@example.test","Verify your email","fictional-link",null);checks++;
            formats[0]=false;formats[1]=true;sender.send(Map.of(),"fictional.adult@example.test","Verify your email",null,"<a>fictional-link</a>");checks++;
            formats[0]=true;
            for(int code:new int[]{200,201,302,400,401,403,429,500,503}){
                status.set(code);int before=calls.get();refused(sender);check(calls.get()==before+1,"no automatic replay");
            }
            status.set(202);
            for(String id:new String[]{null,"","invalid ack","x".repeat(129)}){ack[0]=id;refused(sender);}
            ack[0]=null;body[0]="{\"message\":\"ALL_SUPPRESSED\"}";refused(sender);
            ack[0]="fictional_ack-1";body[0]="x".repeat(8193);refused(sender);
            int before=calls.get();
            try{sender.send(Map.of(),"invalid\r\naddress","subject","text","html");throw new AssertionError("invalid recipient accepted");}catch(EmailException expected){check(calls.get()==before,"invalid input refused before HTTP");}
            try{new MailerSendEmailSender("","no-reply@example.test","Swoovr",client,URI.create("http://127.0.0.1/"));throw new AssertionError("missing token accepted");}catch(IllegalArgumentException expected){checks++;}
            body[0]="{}";slow[0]=true;long started=System.nanoTime();refused(sender);check((System.nanoTime()-started)/1_000_000<20000,"finite deadline includes incomplete response body");
        }finally{server.stop(0);}
        if(handlerFailure.get()!=null)throw new AssertionError("Loopback request contract assertion failed.",handlerFailure.get());
        System.out.println("PASS Keycloak MailerSend: "+checks+" loopback HTTP assertions; no real provider/mailbox/delivery evidence.");
    }
    private static void refused(MailerSendEmailSender sender)throws Exception{
        try{sender.send(Map.of(),"fictional.adult@example.test","Verify your email","fictional-link","<a>fictional-link</a>");throw new AssertionError("unconfirmed provider response accepted");}
        catch(EmailException expected){check(!expected.getMessage().contains("fictional")&&!expected.getMessage().contains("example.test"),"private failure text");}
    }
}
