using Swoovr.Domain;

static class RegisteredNameChecks
{
    public static void Run()
    {
        static void Check(string label,bool ok){if(!ok)throw new Exception("Registered names: "+label);}
        Check("international two-part names",RegisteredNamePolicy.Validate("  Zoë  ","李",false) is{FirstName:"Zoë",LastName:"李"});
        Check("explicit mononym",RegisteredNamePolicy.Validate("Sukarno",null,true) is{LastName:null,Mononym:true});
        Check("missing surname needs explicit exception",RegisteredNamePolicy.Validate("Ari",null,false)is null);
        Check("mononym cannot smuggle ignored surname",RegisteredNamePolicy.Validate("Ari","Unexpected",true)is null);
        Check("markup rejected",RegisteredNamePolicy.Validate("<script>","Example",false)is null);
        Check("controls rejected",RegisteredNamePolicy.Validate("Ari\n","Example",false)is null);
        Check("bidi format control refused",RegisteredNamePolicy.Validate("Ari\u202E","Example",false)is null);
        Check("apostrophes and hyphens accepted",RegisteredNamePolicy.Validate("Jean-Luc","O’Connor",false)is not null);
        Check("no surname in initial public name",RegisteredNamePolicy.InitialPublicName(null,new("Ari","Example",false))=="Ari");
        Check("custom public name preserved",RegisteredNamePolicy.InitialPublicName("Custom",new("Ari","Example",false))=="Custom");
        Check("match compares declared fields only",RegisteredNamePolicy.Matches(new("Zoë","Example",false),"ZOË","Example"));
        Check("no fuzzy identity match",!RegisteredNamePolicy.Matches(new("Ari","Example",false),"Ariana","Example"));
        Check("mononym match requires absent document surname",!RegisteredNamePolicy.Matches(new("Ari",null,true),"Ari","Example"));
        Console.WriteLine("PASS registered names: international validation, explicit mononym, public-name privacy and strict declared/ID comparison.");
    }
}
