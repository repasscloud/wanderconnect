package app.swoovr.identity;

/** Must run with the packaged MFA-only JAR, not /build/main, to catch excluded provider dependencies. */
public final class MfaAuditChecks {
    public static void main(String[] args) {
        var factory = new MfaAuditListenerFactory();
        factory.init(null);
        if (!"swoovr-mfa-audit".equals(factory.getId())) throw new AssertionError("MFA audit registration");
        factory.postInit(null);
        factory.close();
        System.out.println("PASS: packaged MFA audit factory initializes without unrelated providers.");
    }
}
