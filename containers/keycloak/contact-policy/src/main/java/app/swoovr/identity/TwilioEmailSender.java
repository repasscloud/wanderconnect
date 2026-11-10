package app.swoovr.identity;

import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.mail.internet.InternetAddress;
import org.keycloak.email.EmailException;
import org.keycloak.email.EmailSenderProvider;
import java.net.URI;
import java.net.http.*;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;

/**
 * Twilio Email API (#316): POST https://comms.twilio.com/v1/Emails, HTTP Basic with the email key SID and secret.
 * Exactly 202 with one bounded operationId is provider acceptance, never mailbox delivery. Single attempt, no private logging.
 */
public final class TwilioEmailSender implements EmailSenderProvider {
    static final URI ENDPOINT=URI.create("https://comms.twilio.com/v1/Emails"); // External provider contract.
    private final String authorization,from,name;
    private final HttpClient client;
    private final URI endpoint;
    private static final ObjectMapper JSON=new ObjectMapper();
    TwilioEmailSender(String keySid,String keySecret,String from,String name,HttpClient client,URI endpoint){
        if(!credential(keySid)||!credential(keySecret)||!address(from)||name==null||name.isBlank()||name.length()>100||controls(name))throw new IllegalArgumentException("Incomplete email provider configuration.");
        this.authorization="Basic "+Base64.getEncoder().encodeToString((keySid+":"+keySecret).getBytes(StandardCharsets.US_ASCII));
        this.from=from;this.name=name;this.client=client;this.endpoint=endpoint;
    }
    private static boolean credential(String value){return value!=null&&!value.isBlank()&&value.length()<=256&&!controls(value)&&value.chars().noneMatch(c->c==':'||Character.isWhitespace(c));}
    private static boolean controls(String value){return value.codePoints().anyMatch(Character::isISOControl);}
    static boolean address(String value){
        if(value==null||value.length()>320||controls(value)||!value.equals(value.strip()))return false;
        try{var parsed=new InternetAddress(value,true);parsed.validate();return parsed.getPersonal()==null&&value.equals(parsed.getAddress());}catch(Exception ignored){return false;}
    }
    /** Keycloak normally supplies HTML; text-only templates are escaped into the HTML part Twilio requires. */
    static String html(String text){
        var escaped=text.replace("&","&amp;").replace("<","&lt;").replace(">","&gt;").replace("\"","&quot;").replace("\r\n","\n");
        var body=new StringBuilder("<!doctype html><html><body>");
        for(var part:escaped.split("\n\n",-1))body.append("<p>").append(part.replace("\n","<br>")).append("</p>");
        return body.append("</body></html>").toString();
    }
    @Override public void validate(Map<String,String> ignored)throws EmailException{
        // Global SPI configuration. No SMTP substitution or test email side effect.
        if(!address(from))throw new EmailException("Email provider configuration unavailable.");
    }
    @Override public void send(Map<String,String> ignored,String recipient,String subject,String text,String html)throws EmailException{
        if(!address(recipient)||subject==null||subject.length()>998||controls(subject)||(text==null&&html==null)||(text!=null&&text.length()>65536)||(html!=null&&html.length()>131072))throw new EmailException("Email message is invalid.");
        CompletableFuture<HttpResponse<byte[]>> pending=null;
        try{
            var content=new LinkedHashMap<String,Object>();content.put("subject",subject);content.put("html",html!=null?html:html(text));if(text!=null)content.put("text",text);
            var message=new LinkedHashMap<String,Object>();message.put("from",Map.of("address",from,"name",name));message.put("to",List.of(Map.of("address",recipient)));message.put("content",content);
            var request=HttpRequest.newBuilder(endpoint).timeout(Duration.ofSeconds(15)).header("Authorization",authorization).header("Accept","application/json").header("Content-Type","application/json").POST(HttpRequest.BodyPublishers.ofByteArray(JSON.writeValueAsBytes(message))).build();
            pending=client.sendAsync(request,info->new MailerSendEmailSender.LimitedBody());
            var response=pending.get(15,TimeUnit.SECONDS);
            if(response.statusCode()!=202||!operationId(response.body()))throw new EmailException("Email provider acknowledgement unavailable.");
        }catch(InterruptedException interrupted){Thread.currentThread().interrupt();throw new EmailException("Email provider acknowledgement unavailable.");}
        catch(EmailException expected){throw expected;}
        catch(Exception ignoredFailure){throw new EmailException("Email provider acknowledgement unavailable.");}
        finally{if(pending!=null&&!pending.isDone())pending.cancel(true);}
    }
    private static boolean operationId(byte[] body){
        try{var value=JSON.readTree(body);var id=value==null?null:value.get("operationId");
            return id!=null&&id.isTextual()&&id.asText().matches("[A-Za-z0-9_-]{1,128}");}catch(Exception ignored){return false;}
    }
    @Override public void close(){} // Factory owns and closes the shared client.
}
