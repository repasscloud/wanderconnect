package app.swoovr.identity;
public final class MfaRequirementChecks {
    public static void main(String[] args){
        if(MfaRequirement.required(null,null)||MfaRequirement.required("false","false")||MfaRequirement.required("TRUE",null))throw new AssertionError("unconfigured is optional");
        if(!MfaRequirement.required("true",null)||!MfaRequirement.required(null,"true")||!MfaRequirement.required("true","false"))throw new AssertionError("strictest policy wins");
        System.out.println("PASS: provider MFA requirement (6 policy checks; native credential handling unchanged).");
    }
}
