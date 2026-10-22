using Swoovr.Client;
using Swoovr.Contracts;

static class AppearanceSyncChecks
{
    public static void Run()
    {
        var count = 0;
        void Check(string name, bool valid) { if (!valid) throw new Exception("Appearance sync: " + name); count++; }
        void Reject(string name, Action action) { try { action(); } catch (ArgumentException) { count++; return; } catch (InvalidOperationException) { count++; return; } throw new Exception("Appearance sync: " + name); }
        var guest = new AppearancePreference("coral", "system");
        var remote = new AppearancePreference("pride", "dark");
        var next = new AppearancePreference("eucalyptus", "light");
        var last = new AppearancePreference("union", "system");
        var state = new AppearanceSyncState(guest);
        Check("guest starts local", state.Current == guest && !state.HasPendingSave);
        var first = state.BeginAccount();
        Check("account loading hides guest and old account", state.IsLoading && state.Current == AppearancePreferences.Default);
        Reject("account edits wait for authoritative read", () => state.Select(next));
        Check("existing account wins", state.CompleteLoad(first, new AppearanceSnapshot(remote, 4)) && state.Current == remote && !state.HasPendingSave);
        state.Select(next); var attempt = state.BeginSave()!;
        Check("save is account-free revisioned DTO", attempt.Update.Preference == next && attempt.Update.ExpectedRevision == 4 && attempt.Update.ChangeId != Guid.Empty);
        Check("offline retry uses exact attempt", state.BeginSave() == attempt);
        state.Select(last);
        Check("new selection while offline stays visible", state.Current == last && state.BeginSave() == attempt);
        Check("older acknowledgement preserves newer choice", state.AcceptSaved(attempt, new AppearanceSnapshot(next,5)) && state.Current == last && state.HasPendingSave);
        Check("duplicate acknowledgement ignored", !state.AcceptSaved(attempt,new AppearanceSnapshot(next,5)));
        var pending = state.BeginSave()!;
        Check("next save rebases on accepted revision", pending.Update.ExpectedRevision == 5 && pending.Update.Preference == last && pending.Update.ChangeId != attempt.Update.ChangeId);
        Check("malformed success rejected", !state.AcceptSaved(pending,new AppearanceSnapshot(last,7)) && state.BeginSave() == pending);
        Check("success content mismatch rejected", !state.AcceptSaved(pending,new AppearanceSnapshot(remote,6)));
        Check("latest acknowledgement clears dirty", state.AcceptSaved(pending,new AppearanceSnapshot(last,6)) && !state.HasPendingSave);
        state.Select(last); Check("same choice stays clean", !state.HasPendingSave && state.BeginSave() is null);
        state.Select(next); var conflicting = state.BeginSave()!;
        Check("conflict retains local and surfaces authoritative choice", state.AcceptConflict(conflicting,new AppearanceSnapshot(remote,9)) && state.Current == next && state.Conflict?.Preference == remote);
        Check("conflict pauses automatic saves", state.BeginSave() is null);
        state.Select(last); state.ResolveConflict(keepLocal: true);
        var rebased = state.BeginSave()!;
        Check("explicit local conflict resolution uses latest local and server revision", rebased.Update.Preference == last && rebased.Update.ExpectedRevision == 9);
        state.AcceptConflict(rebased,new AppearanceSnapshot(remote,10)); state.ResolveConflict(keepLocal: false);
        Check("explicit server resolution uses server preference", state.Current == remote && !state.HasPendingSave && state.Conflict is null);
        state.Select(next); var stale = state.BeginSave()!;
        var second = state.BeginAccount();
        Check("account switch clears account state", state.Current == AppearancePreferences.Default && state.IsLoading && state.Conflict is null && !state.HasPendingSave);
        Check("prior account load ignored", !state.CompleteLoad(first,new AppearanceSnapshot(last,100)));
        Check("prior account save ignored", !state.AcceptSaved(stale,new AppearanceSnapshot(next,11)));
        Check("prior account conflict ignored", !state.AcceptConflict(stale,new AppearanceSnapshot(last,100)));
        Check("new account adopts guest only when absent", state.CompleteLoad(second,null) && state.Current == guest && state.HasPendingSave);
        var create = state.BeginSave()!;
        Check("new account creation revision zero", create.Update.ExpectedRevision == 0);
        state.SignOut();
        Check("sign-out clears all account state", state.Current == guest && !state.IsLoading && !state.HasPendingSave && state.Conflict is null);
        Check("signed-out save response ignored", !state.AcceptSaved(create,new AppearanceSnapshot(guest,1)));
        var sameAccountAgain = state.BeginAccount();
        Check("reactivation uses a fresh generation", sameAccountAgain != first && sameAccountAgain != second);
        Check("signed-out load ignored after reactivation", !state.CompleteLoad(second,new AppearanceSnapshot(last,1)));
        Check("invalid server revision rejected", !state.CompleteLoad(sameAccountAgain,new AppearanceSnapshot(remote,0)));
        Check("unsupported account preference falls back and completes loading", state.CompleteLoad(sameAccountAgain,new AppearanceSnapshot(new("unknown","dark"),17)) && !state.IsLoading && state.Current==AppearancePreferences.Default && !state.HasPendingSave);
        state.Select(next);Check("fallback preserves authoritative revision for next save",state.BeginSave()!.Update.ExpectedRevision==17);
        state.SignOut();sameAccountAgain=state.BeginAccount();
        Check("valid load remains possible", state.CompleteLoad(sameAccountAgain,new AppearanceSnapshot(remote,1)));
        Check("duplicate load cannot overwrite edits", !state.CompleteLoad(sameAccountAgain,new AppearanceSnapshot(last,2)));
        Reject("invalid local choice rejected", () => state.Select(new("eucalyptus","invalid")));
        Check("invalid local choice leaves current", state.Current == remote);
        state.SignOut(); state.Select(last);
        Check("guest changes never queue account save", state.Current == last && !state.HasPendingSave);
        Check("all 24 portable preferences supported", new[] { "standard", "lounge", "pride", "trans", "union", "violet", "coral", "eucalyptus" }.All(scheme => new[] { "system", "light", "dark" }.All(mode => AppearancePreferences.IsValid(new(scheme,mode)))));
        Reject("conflict resolution requires a conflict", () => state.ResolveConflict(true));
        var conflictState = new AppearanceSyncState(guest);
        var conflictActivation = conflictState.BeginAccount();
        conflictState.CompleteLoad(conflictActivation,new AppearanceSnapshot(remote,1));
        conflictState.Select(next); var conflictAttempt = conflictState.BeginSave()!;
        conflictState.Select(remote);
        conflictState.AcceptConflict(conflictAttempt,new AppearanceSnapshot(last,2));
        Check("unresolved conflict never appears saved", conflictState.HasPendingSave);
        conflictState.SignOut();
        Check("signout clears unresolved conflict", conflictState.Conflict is null && !conflictState.HasPendingSave && conflictState.Current == guest);
        foreach(var invalidPreference in new AppearancePreference?[]{new("unknown","dark"),new("eucalyptus","removed"),null,new(null!,"dark"),new("union",null!),new("electric","dark"),new("solar","light")})
        {
            var fallback=new AppearanceSyncState(guest);var ticket=fallback.BeginAccount();
            Check("malformed server preference normalizes complete pair",fallback.CompleteLoad(ticket,new(invalidPreference!,23)) && !fallback.IsLoading && fallback.Current==AppearancePreferences.Default && !fallback.HasPendingSave);
            fallback.Select(next);var save=fallback.BeginSave()!;
            Check("normalized load allows edits and preserves revision",save.Update.ExpectedRevision==23);
            Check("unsupported conflict accepted as safe fallback",fallback.AcceptConflict(save,new(invalidPreference!,25)) && fallback.Conflict?.Preference==AppearancePreferences.Default);
            fallback.ResolveConflict(false);
            Check("remote fallback conflict resolution completes",fallback.Current==AppearancePreferences.Default && !fallback.HasPendingSave);
        }
        var localDefault=new AppearanceSyncState(guest);var localTicket=localDefault.BeginAccount();
        localDefault.CompleteLoad(localTicket,new(remote,1));localDefault.Select(AppearancePreferences.Default);
        var localAttempt=localDefault.BeginSave()!;
        Check("normalized conflict displayed",localDefault.AcceptConflict(localAttempt,new(new("removed","dark"),7)));
        localDefault.ResolveConflict(true);
        Check("explicit keep-local of normalized default still queues save",localDefault.HasPendingSave);
        var repair=localDefault.BeginSave()!;
        Check("explicit normalized conflict save rebases",repair.Update.ExpectedRevision==7 && repair.Update.Preference==AppearancePreferences.Default);
        Check("explicit save retains retry identity",localDefault.BeginSave()==repair);
        Check("repair acknowledgement clears pending",localDefault.AcceptSaved(repair,new(AppearancePreferences.Default,8)) && !localDefault.HasPendingSave);
        localDefault.Select(next);var another=localDefault.BeginSave()!;
        localDefault.AcceptConflict(another,new(new("removed","dark"),9));localDefault.ResolveConflict(true);
        localDefault.SignOut();var cleanTicket=localDefault.BeginAccount();localDefault.CompleteLoad(cleanTicket,new(AppearancePreferences.Default,10));
        Check("activation clears forced normalized save",!localDefault.HasPendingSave && localDefault.BeginSave() is null);
        var terminal=new AppearanceSyncState(guest);var terminalTicket=terminal.BeginAccount();
        Check("penultimate revision loads",terminal.CompleteLoad(terminalTicket,new(remote,long.MaxValue-1)));
        terminal.Select(next);var finalWrite=terminal.BeginSave()!;
        Check("last write uses penultimate revision",finalWrite.Update.ExpectedRevision==long.MaxValue-1);
        terminal.Select(last);
        Check("terminal acknowledgement is accepted",terminal.AcceptSaved(finalWrite,new(next,long.MaxValue)));
        Check("terminal clears retry but exposes newer unsaved choice",terminal.IsRevisionExhausted && terminal.HasPendingSave && terminal.Current==last && terminal.BeginSave() is null);
        Reject("terminal account edits are explicitly read-only",()=>terminal.Select(next));
        Check("terminal duplicate acknowledgement rejected",!terminal.AcceptSaved(finalWrite,new(next,long.MaxValue)));
        terminal.SignOut();terminalTicket=terminal.BeginAccount();
        Check("terminal load is explicit read-only state",terminal.CompleteLoad(terminalTicket,new(remote,long.MaxValue)) && terminal.IsRevisionExhausted && !terminal.HasPendingSave && terminal.BeginSave() is null);
        terminal.SignOut();terminalTicket=terminal.BeginAccount();terminal.CompleteLoad(terminalTicket,new(remote,1));terminal.Select(next);var limitConflict=terminal.BeginSave()!;
        Check("terminal conflict is resolvable",terminal.AcceptConflict(limitConflict,new(last,long.MaxValue)));
        terminal.ResolveConflict(false);
        Check("terminal remote resolution clean and read-only",terminal.IsRevisionExhausted && terminal.Current==last && !terminal.HasPendingSave && terminal.BeginSave() is null);
        Console.WriteLine($"PASS: {count} appearance sync cases (pure client state; no account transport/persistence).");
    }
}
