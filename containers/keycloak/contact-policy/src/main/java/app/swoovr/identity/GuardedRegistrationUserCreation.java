package app.swoovr.identity;

import java.util.List;
import org.keycloak.authentication.AuthenticationFlowError;
import org.keycloak.authentication.AuthenticationFlowException;
import org.keycloak.authentication.FormAction;
import org.keycloak.authentication.FormContext;
import org.keycloak.authentication.ValidationContext;
import org.keycloak.authentication.forms.RegistrationUserCreation;
import org.keycloak.models.KeycloakSession;
import org.keycloak.models.utils.FormMessage;

/** Replaces the built-in creation action, preserving its validation and profile semantics. */
public final class GuardedRegistrationUserCreation extends RegistrationUserCreation {
    @Override public int order(){return 100;}
    @Override public String getDisplayType(){return "Swoovr guarded registration user creation";}
    @Override public FormAction create(KeycloakSession session){return this;}
    @Override public void validate(ValidationContext context) {
        var form=context.getHttpRequest().getDecodedFormParameters();
        if(!RegistrationAdmission.allows(context.getSession(),form.getFirst("email"))) {
            context.error("registration_disabled");
            context.validationError(form,List.of(new FormMessage(null,"swoovrRegistrationInviteOnly")));
            return;
        }
        if(RegistrationAdmission.applies(context.getSession())&&!RegisteredNameValidation.valid(form.getFirst("firstName"),form.getFirst("lastName"),"true".equals(form.getFirst("swoovr_mononym")))) {
            context.error("invalid_registration");context.validationError(form,List.of(new FormMessage(null,"swoovrRegisteredNamesRequired")));return;
        }
        super.validate(context);
    }
    @Override public void success(FormContext context) {
        // Recheck immediately before the inherited action writes the identity, never from a stale rendered form.
        if(!RegistrationAdmission.allows(context.getSession(),context.getHttpRequest().getDecodedFormParameters().getFirst("email")))
            throw new AuthenticationFlowException(RegistrationAdmission.MESSAGE,AuthenticationFlowError.ACCESS_DENIED);
        super.success(context);
        if(RegistrationAdmission.applies(context.getSession()))context.getUser().setSingleAttribute("swoovr_mononym",Boolean.toString("true".equals(context.getHttpRequest().getDecodedFormParameters().getFirst("swoovr_mononym"))));
    }
}
