package app.swoovr.identity;

import org.keycloak.Config;
import org.keycloak.events.Event;
import org.keycloak.events.EventListenerProvider;
import org.keycloak.events.EventListenerProviderFactory;
import org.keycloak.events.EventType;
import org.keycloak.events.Details;
import org.keycloak.events.admin.AdminEvent;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;

/** Only successful server verification events; broker/admin-asserted verified status never invents email clocks. */
public final class EmailClockListenerFactory implements EventListenerProviderFactory {
    @Override public String getId(){return "swoovr-contact-clocks";}
    @Override public void init(Config.Scope config){ContactEntityProviderFactory.requirePinnedVersion();}
    @Override public void postInit(KeycloakSessionFactory factory) { }
    @Override public void close() { }
    @Override public EventListenerProvider create(KeycloakSession session) {
        return new EventListenerProvider() {
            @Override public void onEvent(Event event) {
                if(!RegistrationAdmission.applies(session)||event.getError()!=null||event.getUserId()==null||event.getDetails()==null)return;
                if(event.getType()!=EventType.SEND_VERIFY_EMAIL&&event.getType()!=EventType.VERIFY_EMAIL)return;
                var user=session.users().getUserById(session.getContext().getRealm(),event.getUserId());if(user==null)return;
                String email=event.getDetails().get(Details.EMAIL);
                if(event.getType()==EventType.SEND_VERIFY_EMAIL)EmailClockEvents.sent(user,email,event.getTime(),event.getSessionId());
                else EmailClockEvents.verified(user,email,event.getTime());
            }
            @Override public void onEvent(AdminEvent event,boolean includeRepresentation) { }
            @Override public void close() { }
        };
    }
}
