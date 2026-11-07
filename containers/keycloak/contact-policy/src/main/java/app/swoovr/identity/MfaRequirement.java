package app.swoovr.identity;

/** Policy metadata only; Keycloak's built-in OTP/recovery providers own all credentials and verification. */
public final class MfaRequirement {
    private MfaRequirement() { }
    public static boolean required(String realmPolicy,String userPolicy){return "true".equals(realmPolicy)||"true".equals(userPolicy);}
}
