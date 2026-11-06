package app.swoovr.identity;

import jakarta.ws.rs.core.Response;
import org.keycloak.authentication.AuthenticationFlowContext;
import org.keycloak.authentication.AuthenticationFlowError;
import org.keycloak.authentication.Authenticator;
import org.keycloak.authentication.authenticators.broker.IdpCreateUserIfUniqueAuthenticator;
import org.keycloak.authentication.authenticators.broker.IdpCreateUserIfUniqueAuthenticatorFactory;
import org.keycloak.authentication.authenticators.broker.util.SerializedBrokeredIdentityContext;
import org.keycloak.broker.provider.BrokeredIdentityContext;
import org.keycloak.models.KeycloakSession;

/** Replaces only the creation action. Existing identities retain the inherited linking/authentication path. */
public final class GuardedBrokerCreationFactory extends IdpCreateUserIfUniqueAuthenticatorFactory {
    @Override public int order(){return 100;}
    @Override public String getDisplayType(){return "Swoovr guarded broker user creation";}
    @Override public Authenticator create(KeycloakSession session){return new Guard();}
    static final class Guard extends IdpCreateUserIfUniqueAuthenticator {
        @Override protected void authenticateImpl(AuthenticationFlowContext context,SerializedBrokeredIdentityContext serialized,BrokeredIdentityContext broker) {
            String username=getUsername(context,serialized,broker);
            boolean existing=context.getAuthenticationSession().getAuthNote(EXISTING_USER_INFO)!=null
                ||username!=null&&checkExistingUser(context,username,serialized,broker)!=null;
            if(!existing&&!RegistrationAdmission.allows(context.getSession(),broker.getEmail())) {
                context.failureChallenge(AuthenticationFlowError.ACCESS_DENIED,context.form().setError(RegistrationAdmission.refusalKey(context.getSession())).createErrorPage(Response.Status.FORBIDDEN));
                return;
            }
            super.authenticateImpl(context,serialized,broker);
        }
    }
}
