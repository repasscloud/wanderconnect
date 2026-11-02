package app.swoovr.identity;

import org.keycloak.authentication.AuthenticationFlowContext;
import org.keycloak.authentication.Authenticator;
import org.keycloak.authentication.authenticators.browser.UsernamePasswordForm;
import org.keycloak.authentication.authenticators.browser.UsernamePasswordFormFactory;
import org.keycloak.forms.login.LoginFormsProvider;
import org.keycloak.models.KeycloakSession;
import jakarta.ws.rs.core.Response;

/** Only changes registration-link presentation; inherits password, passkey and recovery authentication unchanged. */
public final class GuardedLoginFormFactory extends UsernamePasswordFormFactory {
    @Override public int order(){return 100;}
    @Override public String getDisplayType(){return "Swoovr registration-aware username password form";}
    @Override public Authenticator create(KeycloakSession session){return new Form(session);}
    private static final class Form extends UsernamePasswordForm {
        private final KeycloakSession session;
        private Form(KeycloakSession session){super(session);this.session=session;}
        @Override protected Response createLoginForm(LoginFormsProvider form){
            form.setAttribute("swoovrRegistrationAvailable",RegistrationAdmission.available(session));
            return super.createLoginForm(form);
        }
        @Override public void authenticate(AuthenticationFlowContext context){
            context.form().setAttribute("swoovrRegistrationAvailable",RegistrationAdmission.available(session));
            super.authenticate(context);
        }
    }
}
