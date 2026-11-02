package app.swoovr.identity;

import java.net.URI;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.time.Duration;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import org.keycloak.models.KeycloakSession;
import org.keycloak.util.JsonSerialization;

/** No cache: unavailable/malformed authority closes creation; existing authentication never calls this guard. */
public final class RegistrationAdmission {
    private RegistrationAdmission() { }
    static final String MESSAGE="Swoovr is currently invite-only";
    private static final HttpClient HTTP=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(3)).followRedirects(HttpClient.Redirect.NEVER).build();
    public static boolean applies(KeycloakSession session) {
        var realm=session.getContext().getRealm();
        return realm!=null&&(realm.getName().startsWith("swoovr-members")||"required".equals(realm.getAttribute("swoovr.registration-policy")));
    }
    public static boolean validReply(int status,Map<?,?> body) {
        return status==200&&body!=null&&Boolean.TRUE.equals(body.get("allowed"))
            &&(body.get("revision") instanceof Integer||body.get("revision") instanceof Long)
            &&((Number)body.get("revision")).longValue()>=0;
    }
    public static boolean validAvailabilityReply(int status,Map<?,?> body) {
        return status==200&&body!=null&&body.get("betaEnabled") instanceof Boolean&&body.get("publicEnabled") instanceof Boolean
            &&(body.get("revision") instanceof Integer||body.get("revision") instanceof Long)
            &&((Number)body.get("revision")).longValue()>=0
            &&(Boolean.TRUE.equals(body.get("betaEnabled"))||Boolean.TRUE.equals(body.get("publicEnabled")));
    }
    /** Display availability only; reads no email and grants no creation permission. Every POST rechecks admission. */
    public static boolean available(KeycloakSession session) {
        if(!applies(session))return session.getContext().getRealm().isRegistrationAllowed();
        CompletableFuture<HttpResponse<byte[]>> pending=null;
        try {
            String endpoint=System.getenv("SWOOVR_REGISTRATION_AUTHORITY_URL");
            if(endpoint==null)return false;
            URI admission=URI.create(endpoint);
            boolean local="local-development-only".equals(session.getContext().getRealm().getAttribute("swoovr.environment"));
            if(admission.getHost()==null||admission.getUserInfo()!=null||admission.getQuery()!=null||admission.getFragment()!=null||!admission.getPath().equals("/api/registration/admission")
                ||!("https".equals(admission.getScheme())||local&&"http".equals(admission.getScheme())))return false;
            URI uri=admission.resolve("/api/registration");
            var request=HttpRequest.newBuilder(uri).timeout(Duration.ofSeconds(5)).header(ProviderProtocol.HEADER,ProviderProtocol.INITIAL).GET().build();
            pending=HTTP.sendAsync(request,info->new MailerSendEmailSender.LimitedBody());
            var reply=pending.get(5,TimeUnit.SECONDS);
            return reply.body().length<=4096&&validAvailabilityReply(reply.statusCode(),JsonSerialization.readValue(reply.body(),Map.class));
        }catch(Exception refused){if(refused instanceof InterruptedException)Thread.currentThread().interrupt();return false;}
        finally{if(pending!=null&&!pending.isDone())pending.cancel(true);}
    }
    public static boolean allows(KeycloakSession session,String email) {
        if(!applies(session))return true;
        CompletableFuture<HttpResponse<byte[]>> pending=null;
        try {
            String endpoint=System.getenv("SWOOVR_REGISTRATION_AUTHORITY_URL"),token=System.getenv("SWOOVR_REGISTRATION_AUTHORITY_TOKEN");
            if(endpoint==null||token==null||token.length()<32||token.length()>512||email==null||email.length()>254)return false;
            URI uri=URI.create(endpoint);
            boolean local="local-development-only".equals(session.getContext().getRealm().getAttribute("swoovr.environment"));
            if(uri.getHost()==null||uri.getUserInfo()!=null||uri.getQuery()!=null||uri.getFragment()!=null||!uri.getPath().equals("/api/registration/admission")
                ||!("https".equals(uri.getScheme())||local&&"http".equals(uri.getScheme())))return false;
            byte[] payload=JsonSerialization.writeValueAsBytes(Map.of("email",email));
            var request=HttpRequest.newBuilder(uri).timeout(Duration.ofSeconds(5)).header("Content-Type","application/json")
                .header("X-Swoovr-Registration",token).header(ProviderProtocol.HEADER,ProviderProtocol.INITIAL).POST(HttpRequest.BodyPublishers.ofByteArray(payload)).build();
            pending=HTTP.sendAsync(request,info->new MailerSendEmailSender.LimitedBody());
            var reply=pending.get(5,TimeUnit.SECONDS);
            return reply.body().length<=4096&&validReply(reply.statusCode(),JsonSerialization.readValue(reply.body(),Map.class));
        } catch(Exception refused) { if(refused instanceof InterruptedException)Thread.currentThread().interrupt();return false; }
        finally { if(pending!=null&&!pending.isDone())pending.cancel(true); }
    }
}
