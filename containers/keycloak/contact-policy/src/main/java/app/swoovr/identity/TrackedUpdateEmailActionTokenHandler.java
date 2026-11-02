package app.swoovr.identity;

import jakarta.ws.rs.core.Response;
import org.keycloak.authentication.actiontoken.ActionTokenContext;
import org.keycloak.authentication.actiontoken.updateemail.UpdateEmailActionToken;
import org.keycloak.authentication.actiontoken.updateemail.UpdateEmailActionTokenHandler;

/** Pinned update-email succeeds before setting emailVerified; observe only AFTER the inherited verified-token handler. */
public final class TrackedUpdateEmailActionTokenHandler extends UpdateEmailActionTokenHandler {
    @Override public int order(){return 100;}
    @Override public Response handleToken(UpdateEmailActionToken token,ActionTokenContext<UpdateEmailActionToken> context) {
        Response response=super.handleToken(token,context);
        if(RegistrationAdmission.applies(context.getSession())&&response.getStatus()==200)
            EmailClockEvents.verified(context.getAuthenticationSession().getAuthenticatedUser(),token.getNewEmail(),System.currentTimeMillis());
        return response;
    }
}
