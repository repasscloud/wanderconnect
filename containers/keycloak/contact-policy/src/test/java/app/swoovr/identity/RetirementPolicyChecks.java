package app.swoovr.identity;

import java.util.UUID;

/** Pure fictional policy checks; not provider, mailbox ownership or delivery evidence. */
public final class RetirementPolicyChecks {
    private static int checks;
    private static void check(boolean value,String message) {
        if(!value)throw new AssertionError(message);
        checks++;
    }
    public static void main(String[] args) {
        UUID operation=UUID.fromString("1a041e4c-dfca-4cad-913a-819dc0b81264");
        String sink=RetirementPolicy.sink(operation);
        check(sink.equals("retired."+operation+"@swoovr.invalid"),"System sink is unique to the opaque operation and uses a reserved invalid domain.");
        check(!sink.contains("fictional.member"),"Sink does not copy the retired contact.");
        check(RetirementPolicy.sameContact(" Fictional.Member+tag@Example.Test ","fictional.member+tag@example.test"),"Only conservative case and surrounding-space normalization.");
        check(!RetirementPolicy.sameContact("fictional.member+tag@example.test","fictional.member@example.test"),"Do not discard plus tags.");
        check(!RetirementPolicy.sameContact("fictional.member@example.test","fictionalmember@example.test"),"Do not discard dots.");
        check(!RetirementPolicy.sameContact(null,"fictional.member@example.test"),"Absent binding is not a match.");
        check(!RetirementPolicy.sameContact("", ""),"Empty contacts never match.");
        check(RetirementPolicy.nextUsername("Fictional.Member@Example.Test","fictional.member@example.test",sink).equals(sink),"Retire the matching email-login binding.");
        check(RetirementPolicy.nextUsername("unrelated-fictional-login","fictional.member@example.test",sink).equals("unrelated-fictional-login"),"Preserve unrelated login credentials.");
        check(!RetirementPolicy.sameContact(sink,"fictional.member@example.test"),"A completed retirement cannot be mistaken for the original binding.");
        check(RetirementPolicy.unchanged("fictional.member@example.test","unrelated-login","fictional.member@example.test","unrelated-login"),"Exact provider snapshots match.");
        check(!RetirementPolicy.unchanged("Fictional.Member@example.test","unrelated-login","fictional.member@example.test","unrelated-login"),"Raw compare-and-set never normalizes away a changed provider email.");
        check(!RetirementPolicy.unchanged("fictional.member@example.test","later-login","fictional.member@example.test","unrelated-login"),"Concurrent unrelated username changes require another review.");
        check(!RetirementPolicy.unchanged(null,null,null,null),"An absent provider contact cannot be retired.");
        boolean refused=false;try { RetirementPolicy.sink(null); } catch(IllegalArgumentException expected) { refused=true; }
        check(refused,"An opaque operation identity is required.");
        System.out.println("PASS: "+checks+" fictional contact-retirement policy checks; no provider/ownership/delivery claim.");
    }
}
