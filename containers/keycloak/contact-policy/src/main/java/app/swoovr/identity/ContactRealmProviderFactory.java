package app.swoovr.identity;

import org.keycloak.Config;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;
import org.keycloak.services.resource.RealmResourceProvider;
import org.keycloak.services.resource.RealmResourceProviderFactory;

public final class ContactRealmProviderFactory implements RealmResourceProviderFactory {
    @Override public String getId() { return ContactEntityProviderFactory.ID; }
    @Override public void init(Config.Scope config) {
        ContactEntityProviderFactory.requirePinnedVersion();
        if(ProviderProtocol.INITIAL.isBlank())throw new IllegalStateException("Swoovr contract required.");
    }
    @Override public void postInit(KeycloakSessionFactory factory) { }
    @Override public void close() { }
    @Override public RealmResourceProvider create(KeycloakSession session) {
        return new RealmResourceProvider() {
            @Override public Object getResource() { return new ContactRetirementResource(session); }
            @Override public void close() { }
        };
    }
}
