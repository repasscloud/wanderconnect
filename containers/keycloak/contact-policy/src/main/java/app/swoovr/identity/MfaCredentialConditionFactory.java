package app.swoovr.identity;

import org.keycloak.authentication.AuthenticationFlowContext;
import org.keycloak.authentication.authenticators.conditional.ConditionalAuthenticator;
import org.keycloak.authentication.authenticators.conditional.ConditionalCredentialAuthenticator;
import org.keycloak.authentication.authenticators.conditional.ConditionalCredentialAuthenticatorFactory;

/** An enforced OTP requirement cannot be skipped by the native passkey credential condition. Other flows are unchanged. */
public final class MfaCredentialConditionFactory extends ConditionalCredentialAuthenticatorFactory {
    private static final ConditionalAuthenticator INSTANCE=new ConditionalCredentialAuthenticator(){
        @Override public boolean matchCondition(AuthenticationFlowContext context){
            var user=context.getUser();
            return user!=null&&MfaConfiguredConditionFactory.otpFlow(context)
                &&MfaRequirement.required(context.getRealm().getAttribute("swoovr.mfa.required"),user==null?null:user.getFirstAttribute("swoovrMfaRequired"))
                ||super.matchCondition(context);
        }
    };
    @Override public int order(){return 100;}
    @Override public ConditionalAuthenticator getSingleton(){return INSTANCE;}
}
