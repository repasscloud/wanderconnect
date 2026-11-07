package app.swoovr.identity;

import org.keycloak.authentication.AuthenticationFlowContext;
import org.keycloak.authentication.authenticators.conditional.ConditionalAuthenticator;
import org.keycloak.authentication.authenticators.conditional.ConditionalUserConfiguredAuthenticator;
import org.keycloak.authentication.authenticators.conditional.ConditionalUserConfiguredAuthenticatorFactory;

/** Preserve the native configured-credential condition, additionally entering it when server policy requires enrolment. */
public final class MfaConfiguredConditionFactory extends ConditionalUserConfiguredAuthenticatorFactory {
    private static final ConditionalAuthenticator INSTANCE=new ConditionalUserConfiguredAuthenticator(){
        @Override public boolean matchCondition(AuthenticationFlowContext context){
            var user=context.getUser();
            if(user!=null&&otpFlow(context)&&MfaRequirement.required(context.getRealm().getAttribute("swoovr.mfa.required"),user.getFirstAttribute("swoovrMfaRequired"))){
                if(!user.credentialManager().isConfiguredFor("otp")){
                    // The native alternative-factor flow cannot enrol an unconfigured credential. Queue its native
                    // required action, then skip this challenge until that action has confirmed the new authenticator.
                    user.addRequiredAction(org.keycloak.models.UserModel.RequiredAction.CONFIGURE_TOTP);
                    return false;
                }
                return true;
            }
            return super.matchCondition(context);
        }
    };
    static boolean otpFlow(AuthenticationFlowContext context){return context.getRealm().getAuthenticationExecutionsStream(context.getExecution().getParentFlow()).anyMatch(e->"auth-otp-form".equals(e.getAuthenticator()));}
    @Override public int order(){return 100;}
    @Override public ConditionalAuthenticator getSingleton(){return INSTANCE;}
}
