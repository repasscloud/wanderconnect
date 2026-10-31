package app.swoovr.identity;

import java.util.Locale;
import java.util.Objects;
import java.util.UUID;

/** No provider effects. Provider compare-and-set must use exact observed raw fields, not this contact normalization. */
public final class RetirementPolicy {
    private RetirementPolicy() { }
    public static String sink(UUID operation) {
        if(operation==null)throw new IllegalArgumentException("Operation identity required.");
        return "retired."+operation+"@swoovr.invalid";
    }
    private static String canonical(String value) {
        if(value==null)return null;
        String email=value.strip().toLowerCase(Locale.ROOT);
        int at=email.indexOf('@');
        if(email.length()>254||at<=0||at!=email.lastIndexOf('@')||at==email.length()-1||!email.substring(at+1).contains("."))return null;
        if(email.codePoints().anyMatch(c->Character.isWhitespace(c)||Character.isSpaceChar(c)||Character.isISOControl(c)))return null;
        return email;
    }
    public static boolean sameContact(String left,String right) {
        String first=canonical(left);
        return first!=null&&first.equals(canonical(right));
    }
    public static String nextUsername(String username,String email,String sink) {
        return sameContact(username,email)?sink:username;
    }
    /** Raw provider snapshots are exact: normalization must not hide a concurrently changed binding. */
    public static boolean unchanged(String email,String username,String expectedEmail,String expectedUsername) {
        return email!=null&&Objects.equals(email,expectedEmail)&&Objects.equals(username,expectedUsername);
    }
}
