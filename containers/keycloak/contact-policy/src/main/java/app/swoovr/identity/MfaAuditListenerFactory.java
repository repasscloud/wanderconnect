package app.swoovr.identity;

import org.keycloak.Config;
import org.keycloak.common.Version;
import org.keycloak.events.Event;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventListenerProviderFactory;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;

/** IDs and event kinds only: never event details (which can contain submitted inputs), codes, secrets or representations. */
public final class MfaAuditListenerFactory implements EventListenerProviderFactory {
    private static final System.Logger LOG=System.getLogger("app.swoovr.identity.mfa");
    @Override public String getId(){return "swoovr-mfa-audit";}
    @Override public void init(Config.Scope config){
        if(!"26.8.1".equals(Version.VERSION))throw new IllegalStateException("Swoovr MFA audit requires tested Keycloak26.8.1.");
    }
    @Override public void postInit(KeycloakSessionFactory factory) { }
    @Override public void close() { }
    @Override public EventListenerProvider create(KeycloakSession session){return new EventListenerProvider(){
        @Override public void onEvent(Event event){
            String type=event.getType().name();
            if(type.contains("TOTP")||type.contains("CREDENTIAL")||type.contains("REQUIRED_ACTION")||type.equals("LOGIN")||type.equals("LOGIN_ERROR"))
                LOG.log(System.Logger.Level.INFO,"MFA security event={0} realm={1} subject={2} client={3} at={4} outcome={5}",type,event.getRealmId(),event.getUserId(),event.getClientId(),event.getTime(),event.getError()==null?"accepted":"refused");
        }
        @Override public void onEvent(AdminEvent event,boolean includeRepresentation){ }
        @Override public void close(){ }
    };}
}
