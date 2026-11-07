using Swoovr.Domain;

static class StaffManagementChecks
{
    public static void Run()
    {
        void Check(bool value, string name) { if (!value) throw new Exception("staff management: " + name); }
        var actor = Guid.NewGuid(); var target = new Account { Id = Guid.NewGuid(), Realm = AccountRealm.Staff, Status = AccountStatus.Active };
        Check(StaffManagementPolicy.CanManage(actor, false, target, [AccountRole.Support], [AccountRole.Support, AccountRole.Business]), "multiple internal roles");
        Check(!StaffManagementPolicy.CanManage(actor, false, target, [], [AccountRole.Legal]), "only Root appoints Legal");
        Check(!StaffManagementPolicy.CanManage(actor, false, target, [AccountRole.Legal], []), "only Root removes Legal");
        Check(StaffManagementPolicy.CanManage(actor, true, target, [], [AccountRole.Legal]), "Root can appoint Legal");
        Check(!StaffManagementPolicy.CanManage(target.Id, true, target, [], [AccountRole.Admin]), "no self-management escalation or recovery");
        Check(!StaffManagementPolicy.CanManage(actor, true, withRoot(), [], []), "Root is operator controlled");
        Check(!StaffManagementPolicy.CanManage(actor, true, new Account { Id = Guid.NewGuid(), Realm = AccountRealm.Members }, [], [AccountRole.Admin]), "members never become staff through a role edit");
        Check(!StaffManagementPolicy.ValidRoles([AccountRole.VenuePartner]) && !StaffManagementPolicy.ValidRoles([AccountRole.Member]), "external partner/member roles are not internal");
        Check(!StaffManagementPolicy.ValidRoles([AccountRole.Support, AccountRole.Support]), "duplicate role rejected");
        Console.WriteLine("PASS: staff management policy (9 checks).");
        Account withRoot() => new() { Id = target.Id, Realm = AccountRealm.Staff, IsRootAdmin = true };
    }
}
