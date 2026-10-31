package app.swoovr.identity;

import com.fasterxml.jackson.databind.ObjectMapper;
import jakarta.mail.internet.InternetAddress;
import org.keycloak.email.EmailException;
import org.keycloak.email.EmailSenderProvider;
import java.net.URI;
import java.net.http.*;
import java.nio.ByteBuffer;
import java.time.Duration;
import java.util.*;
import java.util.concurrent.*;
import java.io.ByteArrayOutputStream;

/** Single attempt: acknowledgement means provider acceptance, never mailbox delivery. No private logging. */
public final class MailerSendEmailSender implements EmailSenderProvider {
    static final URI ENDPOINT=URI.create("https://api.mailersend.com/v1/email"); // External provider contract, not a Swoovr API version.
    private final String token,from,name;
    private final HttpClient client;
    private final URI endpoint;
    private static final ObjectMapper JSON=new ObjectMapper();
    MailerSendEmailSender(String token,String from,String name,HttpClient client,URI endpoint){
        if(token==null||token.isBlank()||controls(token)||!address(from)||name==null||name.length()>100||controls(name))throw new IllegalArgumentException("Incomplete email provider configuration.");
        this.token=token;this.from=from;this.name=name;this.client=client;this.endpoint=endpoint;
    }
    private static boolean controls(String value){return value.codePoints().anyMatch(Character::isISOControl);}
    private static boolean address(String value){
        if(value==null||value.length()>320||controls(value)||!value.equals(value.strip()))return false;
        try{var parsed=new InternetAddress(value,true);parsed.validate();return parsed.getPersonal()==null&&value.equals(parsed.getAddress());}catch(Exception ignored){return false;}
    }
    @Override public void validate(Map<String,String> ignored)throws EmailException{
        // HTTP configuration is global SPI configuration. No SMTP substitution or test email side effect.
        if(!address(from))throw new EmailException("Email provider configuration unavailable.");
    }
    @Override public void send(Map<String,String> ignored,String recipient,String subject,String text,String html)throws EmailException{
        if(!address(recipient)||subject==null||subject.length()>998||controls(subject)||(text==null&&html==null)||(text!=null&&text.length()>65536)||(html!=null&&html.length()>131072))throw new EmailException("Email message is invalid.");
        CompletableFuture<HttpResponse<byte[]>> pending=null;
        try{
            var message=new HashMap<String,Object>();message.put("from",Map.of("email",from,"name",name));message.put("to",List.of(Map.of("email",recipient)));message.put("subject",subject);
            if(text!=null)message.put("text",text);if(html!=null)message.put("html",html);
            var payload=JSON.writeValueAsBytes(message);
            var request=HttpRequest.newBuilder(endpoint).timeout(Duration.ofSeconds(15)).header("Authorization","Bearer "+token).header("Accept","application/json").header("Content-Type","application/json").POST(HttpRequest.BodyPublishers.ofByteArray(payload)).build();
            pending=client.sendAsync(request,info->new LimitedBody());
            var response=pending.get(15,TimeUnit.SECONDS);
            var ids=response.headers().allValues("x-message-id");
            if(response.statusCode()!=202||ids.size()!=1||!ids.get(0).matches("[A-Za-z0-9_-]{1,128}"))throw new EmailException("Email provider acknowledgement unavailable.");
            // Missing acknowledgement (including ALL_SUPPRESSED) is never success. No provider ID/body is logged.
        }catch(InterruptedException interrupted){Thread.currentThread().interrupt();throw new EmailException("Email provider acknowledgement unavailable.");}
        catch(EmailException expected){throw expected;}
        catch(Exception ignoredFailure){throw new EmailException("Email provider acknowledgement unavailable.");}
        finally{if(pending!=null&&!pending.isDone())pending.cancel(true);}
    }
    @Override public void close(){} // Factory owns and closes the shared client.

    /** Completes only when the bounded body ends, so the outer deadline covers body reads as well as headers. */
    private static final class LimitedBody implements HttpResponse.BodySubscriber<byte[]> {
        private final CompletableFuture<byte[]> result=new CompletableFuture<>();
        private final ByteArrayOutputStream bytes=new ByteArrayOutputStream();private Flow.Subscription subscription;
        @Override public CompletionStage<byte[]> getBody(){return result;}
        @Override public void onSubscribe(Flow.Subscription value){subscription=value;value.request(1);}
        @Override public void onNext(List<ByteBuffer> buffers){
            for(var buffer:buffers){if(buffer.remaining()>8192-bytes.size()){subscription.cancel();result.completeExceptionally(new IllegalStateException("Response limit exceeded."));return;}var chunk=new byte[buffer.remaining()];buffer.get(chunk);bytes.writeBytes(chunk);}
            subscription.request(1);
        }
        @Override public void onError(Throwable ignored){result.completeExceptionally(new IllegalStateException("Response unavailable."));}
        @Override public void onComplete(){result.complete(bytes.toByteArray());}
    }
}
