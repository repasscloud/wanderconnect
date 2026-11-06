package app.swoovr.identity;
import java.util.Map;
import java.util.HashMap;
import java.lang.reflect.Proxy;
import org.keycloak.models.UserModel;
public final class RegistrationAdmissionChecks {
    private static void check(boolean ok){if(!ok)throw new AssertionError("Registration admission must fail closed");}
    private static UserModel user(String id,String email,boolean verified,Map<String,String> attributes) {
        return (UserModel)Proxy.newProxyInstance(UserModel.class.getClassLoader(),new Class<?>[]{UserModel.class},(proxy,method,args)->switch(method.getName()) {
            case "getId"->id;case "getEmail"->email;case "isEmailVerified"->verified;
            case "getFirstAttribute"->attributes.get((String)args[0]);
            case "setSingleAttribute"->{attributes.put((String)args[0],(String)args[1]);yield null;}
            case "removeAttribute"->{attributes.remove((String)args[0]);yield null;}
            default->throw new UnsupportedOperationException(method.getName());
        });
    }
    public static void main(String[] args){
        check(RegistrationAdmission.validReply(200,Map.of("allowed",true,"revision",0)));
        check(!RegistrationAdmission.validReply(503,Map.of("allowed",true,"revision",0)));
        check(!RegistrationAdmission.validReply(200,Map.of("allowed",false,"revision",0)));
        check(!RegistrationAdmission.validReply(200,Map.of("allowed","true","revision",0)));
        check(!RegistrationAdmission.validReply(200,Map.of("allowed",true,"revision",-1)));
        check(!RegistrationAdmission.validReply(200,Map.of("allowed",true)));
        check(!RegistrationAdmission.validReply(200,null));
        check(RegistrationAdmission.validAvailabilityReply(200,Map.of("betaEnabled",true,"publicEnabled",false,"revision",1)));
        check(!RegistrationAdmission.validAvailabilityReply(200,Map.of("betaEnabled",false,"publicEnabled",false,"revision",1)));
        check(!RegistrationAdmission.validAvailabilityReply(503,Map.of("betaEnabled",true,"publicEnabled",true,"revision",1)));
        check(!RegistrationAdmission.validAvailabilityReply(200,Map.of("publicEnabled",true,"revision",1)));
        check(!RegistrationAdmission.validAvailabilityReply(200,null));
        // D335: only beta-on/public-off names invite-only; closed, public and unreadable refusals are neutral.
        check(RegistrationAdmission.refusalKeyFor(Map.of("betaEnabled",true,"publicEnabled",false,"revision",1)).equals("swoovrRegistrationInviteOnly"));
        check(RegistrationAdmission.refusalKeyFor(Map.of("betaEnabled",true,"publicEnabled",true,"revision",1)).equals("swoovrRegistrationClosed"));
        check(RegistrationAdmission.refusalKeyFor(Map.of("betaEnabled",false,"publicEnabled",true,"revision",1)).equals("swoovrRegistrationClosed"));
        check(RegistrationAdmission.refusalKeyFor(null).equals("swoovrRegistrationClosed"));
        check(RegisteredNameValidation.valid("Zoë","李",false));
        check(RegisteredNameValidation.valid("Ari",null,true));
        check(!RegisteredNameValidation.valid("Ari",null,false));
        check(!RegisteredNameValidation.valid("Ari","Ignored",true));
        check(!RegisteredNameValidation.valid("<script>","Example",false));
        check(!RegisteredNameValidation.valid("Ari\u202e","Example",false));
        var attrs=new HashMap<String,String>();var member=user("fictional-subject","Case@Example.test",true,attrs);
        EmailClockEvents.sent(member,"Case@Example.test",1000,"fictional-session");EmailClockEvents.sent(member,"Case@Example.test",2000);
        check("1000".equals(attrs.get(EmailClockEvents.REQUESTED)));
        check(EmailClockEvents.session("fictional-subject","fictional-session").equals(attrs.get(EmailClockEvents.REQUEST_SESSION)));
        EmailClockEvents.verified(member,"Other@Example.test",3000);check(!attrs.containsKey(EmailClockEvents.VERIFIED));
        EmailClockEvents.verified(member,"Case@Example.test",3000);check("3000".equals(attrs.get(EmailClockEvents.VERIFIED)));
        check(EmailClockEvents.binding("fictional-subject","Case@Example.test").equals(attrs.get(EmailClockEvents.VERIFIED_BINDING)));
        check(!EmailClockEvents.binding("other-subject","Case@Example.test").equals(attrs.get(EmailClockEvents.VERIFIED_BINDING)));
        EmailClockEvents.sent(member,"new@example.test",4000);check(!attrs.containsKey(EmailClockEvents.VERIFIED));
        EmailClockEvents.verified(user("fictional-subject","new@example.test",false,attrs),"new@example.test",5000);check(!attrs.containsKey(EmailClockEvents.VERIFIED));
        EmailClockEvents.verified(member,"Case@Example.test",6000);check(!attrs.containsKey(EmailClockEvents.REQUESTED));
        System.out.println("PASS registration admission reply contract: refusals, missing/malformed fields and unavailable authority.");
    }
}
