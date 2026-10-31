package app.swoovr.identity;

import java.util.List;
import org.keycloak.Config;
import org.keycloak.common.Version;
import org.keycloak.connections.jpa.entityprovider.JpaEntityProvider;
import org.keycloak.connections.jpa.entityprovider.JpaEntityProviderFactory;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.KeycloakSessionFactory;

/** Explicitly pinned unsupported JPA extension API: upgrades require an isolated compatibility test. */
public final class ContactEntityProviderFactory implements JpaEntityProviderFactory {
    public static final String ID="swoovr-contact-policy";
    public static final String KEYCLOAK_VERSION="26.8.1";
    public static void requirePinnedVersion() {
        if(!KEYCLOAK_VERSION.equals(Version.VERSION))throw new IllegalStateException("Swoovr contact policy requires tested Keycloak26.8.1.");
    }
    @Override public void init(Config.Scope config) { requirePinnedVersion(); }
    @Override public void postInit(KeycloakSessionFactory factory) { }
    @Override public void close() { }
    @Override public String getId() { return ID; }
    @Override public JpaEntityProvider create(KeycloakSession session) {
        return new JpaEntityProvider() {
            @Override public List<Class<?>> getEntities() { return List.of(RetiredContact.class); }
            @Override public String getChangelogLocation() { return "META-INF/swoovr-contact-policy.xml"; }
            @Override public String getFactoryId() { return ID; }
            @Override public void close() { }
        };
    }
}
