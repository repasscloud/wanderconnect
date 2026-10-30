using Swoovr.Client;
using Swoovr.Contracts;
static class AppearanceCacheChecks
{
    public static void Run()
    {
        var count=0;void Check(bool ok,string name){count++;if(!ok)throw new Exception("Appearance cache: "+name);}
        var guest=new AppearancePreference("standard","system");var state=new AppearanceSyncState(guest);
        Check(state.ExportCache() is null,"guest never exports account cache");
        var ticket=state.BeginAccount();Check(state.ExportCache() is null,"unconfirmed baseline never cached as account preference");
        state.CompleteLoad(ticket,new(new("eucalyptus","system"),12));state.Select(new("pride","dark"));var first=state.BeginSave()!;
        state.Select(new("coral","light"));var cache=state.ExportCache()!;
        var restored=new AppearanceSyncState(guest);var next=restored.BeginAccount(cache);
        Check(!restored.IsLoading&&restored.Current==new AppearancePreference("coral","light"),"latest unsaved selection restored without wrong-account load");
        var retry=restored.BeginSave()!;
        Check(retry.Update==first.Update&&retry.Activation!=first.Activation,"reload retains exact retry identity and refreshes activation");
        Check(!restored.AcceptSaved(first,new(first.Update.Preference,13)),"stale activation response cannot mutate restored account");
        Check(restored.AcceptSaved(retry,new(first.Update.Preference,13))&&restored.Current==cache.Current,"retry acknowledgement preserves newer local choice");
        var latest=restored.BeginSave()!;Check(latest.Update.ExpectedRevision==13&&latest.Update.Preference==cache.Current&&latest.Update.ChangeId!=first.Update.ChangeId,"newer choice gets next version and separate identity");
        Check(restored.AcceptConflict(latest,new(new("trans","system"),15)),"conflict captured");
        var conflict=new AppearanceSyncState(guest);conflict.BeginAccount(restored.ExportCache());
        Check(conflict.Conflict?.Revision==15&&conflict.BeginSave() is null,"reload cannot silently overwrite known conflict");
        conflict.ResolveConflict(false);Check(conflict.Current==new AppearancePreference("trans","system")&&!conflict.HasPendingSave,"explicit remote choice uses account baseline");
        Check(conflict.RefreshAccount(next,new(new("violet","dark"),16))==false,"other activation cannot refresh cache");
        var active=conflict.BeginAccount(conflict.ExportCache());Check(conflict.RefreshAccount(active,new(new("violet","dark"),16))&&conflict.Current==new AppearancePreference("violet","dark"),"clean cache can refresh remote baseline");
        conflict.Select(new("union","system"));Check(!conflict.RefreshAccount(active,new(new("lounge","light"),17))&&conflict.Current==new AppearancePreference("union","system"),"refresh cannot overwrite pending local edit");
        var bad=cache with{Pending=first.Update with{ChangeId=Guid.Empty}};var invalid=new AppearanceSyncState(guest);invalid.BeginAccount(bad);
        Check(invalid.IsLoading&&invalid.Current==AppearancePreferences.Default,"malformed cached retry is ignored without a write");
        conflict.SignOut();Check(conflict.ExportCache() is null&&conflict.Current==guest,"sign-out restores guest and clears cache export");
        Console.WriteLine($"PASS: persisted appearance client state ({count} checks; no storage/authentication/transport claim).");
    }
}
