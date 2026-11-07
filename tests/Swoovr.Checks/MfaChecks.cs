using Swoovr.Domain;

static class MfaChecks
{
    public static void Run()
    {
        var n=0;void Check(bool ok,string name){if(!ok)throw new Exception("MFA: "+name);n++;}
        var now=DateTimeOffset.UtcNow;var actor=Guid.NewGuid();var session=MfaPolicy.Digest("fictional-session");
        var challenge=new MfaElevation{Id=Guid.NewGuid(),AccountId=actor,SessionDigest=session,ClientId="swoovr-admin-web",InitialAuthenticationAt=now.AddHours(-1),RequestedAt=now.AddSeconds(-20),ChallengeExpiresAt=now.AddMinutes(4)};
        Check(MfaPolicy.CanComplete(challenge,actor,session,"swoovr-admin-web",now.AddSeconds(-5),true,now),"fresh MFA completes only requested elevation");
        Check(!MfaPolicy.CanComplete(challenge,actor,session,"swoovr-admin-web",challenge.InitialAuthenticationAt,true,now),"ordinary initial login cannot elevate");
        Check(!MfaPolicy.CanComplete(challenge,actor,session,"swoovr-admin-web",now.AddSeconds(-30),true,now),"reauthentication must follow challenge");
        Check(!MfaPolicy.CanComplete(challenge,actor,session,"swoovr-admin-web",now,false,now),"no MFA no elevation");
        Check(!MfaPolicy.CanComplete(challenge,Guid.NewGuid(),session,"swoovr-admin-web",now,true,now),"another user cannot consume");
        Check(!MfaPolicy.CanComplete(challenge,actor,MfaPolicy.Digest("other-device"),"swoovr-admin-web",now,true,now),"another session cannot consume");
        Check(!MfaPolicy.CanComplete(challenge,actor,session,"other-client",now,true,now),"another application cannot consume");
        Check(!MfaPolicy.CanComplete(challenge,actor,session,"swoovr-admin-web",now.AddMinutes(1),true,now),"future authentication denied");
        challenge.CompletedAt=now;
        Check(!MfaPolicy.CanComplete(challenge,actor,session,"swoovr-admin-web",now,true,now),"challenge consumed once");
        Check(MfaPolicy.RequirementMayChange(AccountRole.Support,AccountRole.Support)&&!MfaPolicy.RequirementMayChange(AccountRole.Support,AccountRole.Admin),"Support cannot remove stronger enforcement");
        Check(MfaPolicy.RequirementMayChange(AccountRole.TrustSafety,AccountRole.Support)&&MfaPolicy.RequirementMayChange(AccountRole.Admin,AccountRole.TrustSafety),"higher authorized intervention");
        Check(!MfaPolicy.RequirementMayChange(AccountRole.Business,AccountRole.Support)&&!MfaPolicy.RequirementMayChange(AccountRole.Legal,AccountRole.Support),"no unrelated role authority");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/staff/accounts/{id:guid}/security"),"staff credential operations require elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/cases/{id:guid}/actions"),"high-impact account actions require elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/appeals/{id:guid}/decide"),"administrative appeal mutation requires elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("GET","/api/admin/cases/{id:guid}/content"),"private content elevation independent of navigation");
        Check(!Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/mfa/elevation/begin"),"requesting elevation needs no existing elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("GET","/api/admin/cases/{id:guid}/text-candidates/{review:guid}"),"private screened text requires elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/accounts/{id:guid}/identity"),"actual identity route requires elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/media/{id:guid}/case-view"),"actual private media issuance requires elevation");
        Check(Swoovr.Api.Accounts.MfaPrivilegedMiddleware.Requires("POST","/api/admin/media/{id:guid}/view"),"actual review media issuance requires elevation");
        Check(StaffAccessPolicy.Classify("POST","/api/admin/mfa/"+actor+"/reset",403) is { Resource: "mfa", Action: "refused" },"MFA refusals enter the sensitive audit chain");
        var facts=new ValidatedSessionFacts(AccountRealm.Staff,"fictional-root",true,["pwd","recovery-code"],null,now,null);
        var root=new Swoovr.Api.Accounts.CurrentAccount(facts,new(SignInMethod.EmailPassword,true,true),null,new Account{Id=actor,Realm=AccountRealm.Staff,IsRootAdmin=true,Status=AccountStatus.Active},[],null,BetaAccess.NotRequired);
        Check(!root.Allows(AccountRole.Admin),"Root requires OTP rather than recovery-code assurance");
        Check((root with{Facts=facts with{AuthenticationMethods=["pwd","otp"]}}).Allows(AccountRole.Admin),"Root password plus OTP remains accepted");
        Console.WriteLine($"PASS: MFA policy ({n} checks).");
    }
}
