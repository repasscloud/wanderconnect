package app.swoovr.identity;

import org.keycloak.Config;
import org.keycloak.email.*;
import org.keycloak.models.*;
import java.net.http.HttpClient;
import java.time.Duration;

/** Explicitly selected only by the UAT image. Local Keycloak retains its SMTP mail sink. */
public final class MailerSendEmailSenderFactory implements EmailSenderProviderFactory {
    private HttpClient client;private String token,from,name;
    @Override public void init(Config.Scope config){
        token=config.get("api-token");from=config.get("from");name=config.get("from-name","Swoovr");
        client=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(5)).followRedirects(HttpClient.Redirect.NEVER).build();
    }
    @Override public EmailSenderProvider create(KeycloakSession session){return new MailerSendEmailSender(token,from,name,client,MailerSendEmailSender.ENDPOINT);}
    @Override public void postInit(KeycloakSessionFactory factory){}
    @Override public void close(){if(client!=null)client.shutdownNow();}
    @Override public String getId(){return "swoovr-mailersend";}
}
