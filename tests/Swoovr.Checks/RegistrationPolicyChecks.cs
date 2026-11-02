using Swoovr.Domain;

static class RegistrationPolicyChecks
{
    public static void Run()
    {
        static void Check(string name,bool condition){if(!condition)throw new Exception("Registration policy: "+name);}
        Check("unreadable authority closes new registration",!RegistrationAvailability.Allows(null,false));
        foreach(var beta in new[]{false,true})foreach(var publicOpen in new[]{false,true})
        {
            var policy=new RegistrationAvailability(beta,publicOpen,7);
            Check("public permits non-invited registration",RegistrationAvailability.Allows(policy,false)==publicOpen);
            Check("invite requires beta or public",RegistrationAvailability.Allows(policy,true)==(beta||publicOpen));
            Check("effective state distinguishes four combinations",policy.State==(publicOpen?(beta?"public-with-beta-tracking":"public"):(beta?"invite-only":"closed")));
        }
        Check("invalid revision closes",!RegistrationAvailability.Allows(new(true,true,-1),true));
        Check("reason required",!RegistrationAvailability.ValidChange(0,true," "));
        Check("confirmation required",!RegistrationAvailability.ValidChange(0,false,"Reviewed launch control"));
        Check("revision overflow refused",!RegistrationAvailability.ValidChange(long.MaxValue,true,"Reviewed launch control"));
        Check("valid confirmed change",RegistrationAvailability.ValidChange(4,true,"Reviewed launch control"));
        Console.WriteLine("PASS registration availability: four states, missing policy and change validation.");
    }
}
