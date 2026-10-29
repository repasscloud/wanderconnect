using Swoovr.Domain;

static class EmailRetirementChecks
{
    public static void Run()
    {
        var count=0;void Check(bool value,string message){if(!value)throw new Exception("Email retirement: "+message);count++;}
        var now=DateTimeOffset.Parse("2026-10-30T00:00:00Z");
        var operation=new EmailRetirementOperation{Id=Guid.NewGuid(),AccountId=Guid.NewGuid(),RegistryId=Guid.NewGuid(),CaseId=Guid.NewGuid(),RequestedBy=Guid.NewGuid(),RequestedAt=now,UpdatedAt=now,NextAttemptAt=now,EncryptedCommand=[1,2,3]};
        Check(EmailRetirementPolicy.BlocksAccess(operation),"durable intent refuses original subject before remote effects");
        EmailRetirementPolicy.Observe(operation,IdentityContactState.Unavailable,now);
        Check(operation.State==EmailRetirementState.Pending&&operation.CompletedAt is null,"unavailable attempt cannot complete");
        Check(operation.NextAttemptAt>now&&operation.Attempts==1,"ambiguous work remains durably due after bounded delay");
        EmailRetirementPolicy.Observe(operation,IdentityContactState.NotRecorded,now.AddMinutes(1));
        Check(operation.State==EmailRetirementState.Pending,"missing receipt is not no-effect or completion proof");
        EmailRetirementPolicy.Observe(operation,IdentityContactState.BindingChanged,now.AddMinutes(2));
        Check(operation.State==EmailRetirementState.NeedsReview&&EmailRetirementPolicy.BlocksAccess(operation),"changed binding requires review and retains refusal");
        EmailRetirementPolicy.Observe(operation,IdentityContactState.Retired,now.AddMinutes(3));
        Check(operation.State==EmailRetirementState.Completed&&operation.CompletedAt==now.AddMinutes(3),"known committed provider effect may reconcile after interrupted review");
        var completed=operation.CompletedAt;EmailRetirementPolicy.Observe(operation,IdentityContactState.Unavailable,now.AddMinutes(4));
        Check(operation.State==EmailRetirementState.Completed&&operation.CompletedAt==completed,"later transport uncertainty cannot reverse confirmed completion");
        Check(operation.EncryptedCommand.Length==3,"observation does not silently erase possibly held command evidence");
        Check(!EmailRetirementPolicy.BlocksAccess(operation),"completed intent ends pending refusal; retired-email policy remains independently required");
        Console.WriteLine($"PASS: durable email retirement state ({count} checks; no provider/authorization proof).");
    }
}
