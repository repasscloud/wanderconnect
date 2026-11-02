package app.swoovr.identity;

import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.HexFormat;
import java.util.Objects;
import org.keycloak.models.UserModel;

/** Four bounded admin-only attributes for the latest email verification, never a contact/event history. */
public final class EmailClockEvents {
    public static final String REQUEST_BINDING="swoovr_email_request_binding",REQUESTED="swoovr_email_requested_ms";
    public static final String REQUEST_SESSION="swoovr_email_request_session_sha256";
    public static final String VERIFIED_BINDING="swoovr_email_verified_binding",VERIFIED="swoovr_email_verified_ms";
    private EmailClockEvents() { }
    public static String binding(String subject,String email) {
        if(subject==null||subject.length()>255||email==null||email.length()>254||email.isBlank())return null;
        try{return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest((subject+"\0"+email).getBytes(StandardCharsets.UTF_8)));}
        catch(java.security.NoSuchAlgorithmException impossible){throw new IllegalStateException("SHA-256 required.");}
    }
    public static String session(String subject,String session) {
        if(subject==null||session==null||session.length()>255)return null;
        try{return HexFormat.of().formatHex(MessageDigest.getInstance("SHA-256").digest((subject+"\0"+session).getBytes(StandardCharsets.UTF_8)));}
        catch(java.security.NoSuchAlgorithmException impossible){throw new IllegalStateException("SHA-256 required.");}
    }
    public static void sent(UserModel user,String email,long milliseconds){sent(user,email,milliseconds,null);}
    public static void sent(UserModel user,String email,long milliseconds,String sessionId) {
        String binding=binding(user.getId(),email);if(binding==null||milliseconds<=0)return;
        boolean pending=Objects.equals(binding,user.getFirstAttribute(REQUEST_BINDING))&&user.getFirstAttribute(VERIFIED)==null;
        if(!pending){user.setSingleAttribute(REQUEST_BINDING,binding);user.setSingleAttribute(REQUESTED,Long.toString(milliseconds));String correlated=session(user.getId(),sessionId);if(correlated==null)user.removeAttribute(REQUEST_SESSION);else user.setSingleAttribute(REQUEST_SESSION,correlated);}
        user.removeAttribute(VERIFIED_BINDING);user.removeAttribute(VERIFIED);
    }
    public static void verified(UserModel user,String email,long milliseconds) {
        if(!user.isEmailVerified()||!Objects.equals(email,user.getEmail())||milliseconds<=0)return;
        String binding=binding(user.getId(),email);if(binding==null)return;
        if(!Objects.equals(binding,user.getFirstAttribute(REQUEST_BINDING))){user.removeAttribute(REQUEST_BINDING);user.removeAttribute(REQUESTED);user.removeAttribute(REQUEST_SESSION);}
        user.setSingleAttribute(VERIFIED_BINDING,binding);user.setSingleAttribute(VERIFIED,Long.toString(milliseconds));
    }
}
