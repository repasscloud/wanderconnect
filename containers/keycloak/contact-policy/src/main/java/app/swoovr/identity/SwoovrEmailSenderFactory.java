package app.swoovr.identity;

import org.keycloak.Config;
import org.keycloak.email.*;
import org.keycloak.models.*;
import java.net.http.HttpClient;
import java.time.Duration;

/**
 * The UAT image's only email sender (selected at build as swoovr-email). The service is chosen at startup by
 * KC_SPI_EMAIL_SENDER__SWOOVR_EMAIL__SERVICE: twilio (default, #316) or mailersend (D281), so a provider change is
 * configuration plus a restart, not a new image. Local Keycloak retains its SMTP mail sink.
 */
public final class SwoovrEmailSenderFactory implements EmailSenderProviderFactory {
    private HttpClient client;private String service,from,name,keySid,keySecret,apiToken;
    @Override public void init(Config.Scope config){
        service=config.get("service","twilio");from=config.get("from");name=config.get("from-name","Swoovr");
        keySid=config.get("twilio-key-sid");keySecret=config.get("twilio-key-secret");apiToken=config.get("mailersend-api-token");
        if(!"twilio".equals(service)&&!"mailersend".equals(service))throw new IllegalArgumentException("Email service must be twilio or mailersend.");
        client=HttpClient.newBuilder().connectTimeout(Duration.ofSeconds(5)).followRedirects(HttpClient.Redirect.NEVER).build();
    }
    @Override public EmailSenderProvider create(KeycloakSession session){
        return "mailersend".equals(service)
            ?new MailerSendEmailSender(apiToken,from,name,client,MailerSendEmailSender.ENDPOINT)
            :new TwilioEmailSender(keySid,keySecret,from,name,client,TwilioEmailSender.ENDPOINT);
    }
    @Override public void postInit(KeycloakSessionFactory factory){}
    @Override public void close(){if(client!=null)client.shutdownNow();}
    @Override public String getId(){return "swoovr-email";}
}
