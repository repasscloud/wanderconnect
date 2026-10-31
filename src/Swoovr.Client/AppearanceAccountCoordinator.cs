using Swoovr.Contracts;
namespace Swoovr.Client;

/// <summary>One UI-thread account activation. Storage/application callbacks must also reject a changed owner.</summary>
public sealed record AppearanceCacheWrite(bool Available,bool Superseded=false);

public sealed class AppearanceAccountCoordinator : IDisposable
{
    private readonly MemberClient members;
    private readonly Func<AppearancePreference,Task> apply;
    private readonly Func<AppearanceAccountCache,bool,Task<AppearanceCacheWrite>> save;
    private readonly Func<Task> clear,changed;
    private readonly SemaphoreSlim gate=new(1,1);
    private readonly CancellationTokenSource cancellation=new();
    private Guid activation;
    private bool disposed,refreshQueued;
    private AppearanceSnapshot? resetSnapshot;
    public string Owner { get; }
    public AppearanceSyncState State { get; }
    public string Message { get; private set; }="Loading account appearance…";
    public bool CacheAvailable { get; private set; }=true;
    public bool Blocked { get; private set; }
    public bool NeedsSignIn { get; private set; }
    public bool MissingConflict { get; private set; }
    public bool Busy { get; private set; }
    public AppearanceAccountCoordinator(MemberClient members,string owner,AppearancePreference guest,AppearanceAccountCache? cache,
        Func<AppearancePreference,Task> apply,Func<AppearanceAccountCache,bool,Task<AppearanceCacheWrite>> save,Func<Task> clear,Func<Task> changed)
    {
        if(!AppearanceOwner.Valid(owner))throw new ArgumentException("A confirmed appearance owner is required.");
        this.members=members;Owner=owner;this.apply=apply;this.save=save;this.clear=clear;this.changed=changed;
        State=new(guest);activation=State.BeginAccount(cache);
    }
    public async Task ChangeAsync(AppearancePreference preference,bool syncNow=true)
    {
        if(disposed||Blocked||State.IsLoading||State.IsRevisionExhausted)return;
        State.Select(preference);Message="Applied on this device. Waiting to sync.";await PublishAsync(userEdit:true);if(syncNow)await SyncAsync();
    }
    public async Task ResolveAsync(bool keepLocal)
    {
        if(disposed||Blocked)return;
        if(MissingConflict)
        {
            var choice=keepLocal?State.Current:resetSnapshot is{}remote&&AppearancePreferences.IsValid(remote.Preference)?remote.Preference:AppearancePreferences.Default;
            activation=State.BeginAccount();State.CompleteLoad(activation,resetSnapshot);State.Select(choice);MissingConflict=false;resetSnapshot=null;
        }
        else if(State.Conflict is not null)State.ResolveConflict(keepLocal);else return;
        Message=keepLocal?"Keeping this device's choice.":"Using the account choice.";await PublishAsync(userEdit:true);await SyncAsync();
    }
    public async Task SyncAsync(bool refresh=false)
    {
        if(disposed||Blocked)return;
        if(!await gate.WaitAsync(0)){refreshQueued|=refresh;return;}
        try
        {
            Busy=true;await changed();if(disposed)return;
            if(State.IsLoading||refresh&&!State.HasPendingSave)
            {
                using var loadDeadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);loadDeadline.CancelAfter(TimeSpan.FromSeconds(15));
                var loaded=await members.AppearanceAsync(Owner,loadDeadline.Token);if(disposed)return;
                if(await EndedAsync(loaded))return;
                if(loaded.Status==401){NeedsSignIn=true;Message="Sign in again before syncing. Your local choice still applies.";await changed();return;}
                NeedsSignIn=false;
                if(loaded.Status!=204&&!loaded.Ok){Message="We couldn't load your account appearance. Your local choice still applies. Retry when connected.";await changed();return;}
                var snapshot=loaded.Status==204?null:loaded.Value;
                if(State.IsLoading)State.CompleteLoad(activation,snapshot);else State.RefreshAccount(activation,snapshot);
                Message="Account appearance loaded.";await PublishAsync();
            }
            if(State.IsRevisionExhausted){Message="Your account appearance is read-only. No further changes can be synced.";await changed();return;}
            while(!disposed&&!Blocked&&!MissingConflict&&State.HasPendingSave&&State.Conflict is null)
            {
                var attempt=State.BeginSave();if(attempt is null)break;
                Message="Saving appearance…";await PublishAsync();if(disposed)return;
                using var sendDeadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token);sendDeadline.CancelAfter(TimeSpan.FromSeconds(15));
                var response=await members.SaveAppearanceAsync(attempt.Update,Owner,sendDeadline.Token);if(disposed)return;
                if(await EndedAsync(response))return;
                if(response.Status==401){NeedsSignIn=true;Message="Sign in again before syncing. Your local choice still applies.";await PublishAsync();return;}
                NeedsSignIn=false;
                if(response.Ok&&response.Value is{}saved)
                {
                    if(!State.AcceptSaved(attempt,saved))return;
                    Message="Saved to your account.";await PublishAsync();
                }
                else if(response.Status==409&&response.Code is "appearance-conflict" or "appearance-revision-exhausted")
                {
                    if(response.Value is not{}current||!State.AcceptConflict(attempt,current)){MissingConflict=true;resetSnapshot=response.Value;}
                    Message="Your account appearance changed elsewhere. Choose which version to use.";await PublishAsync();return;
                }
                else{Message="Your device choice is waiting to sync. Retry when connected.";await PublishAsync();return;}
            }
            if(!State.HasPendingSave&&!State.IsLoading&&State.Conflict is null)Message="Using your saved account appearance.";
        }
        catch(Exception error)when(error is HttpRequestException or TaskCanceledException)
        {
            if(!disposed&&!Blocked){Message="Your local choice still applies. Sync is unavailable; retry when connected.";await PublishAsync();}
        }
        finally{Busy=false;gate.Release();if(disposed)cancellation.Dispose();else{await changed();if(refreshQueued&&!Blocked){refreshQueued=false;await SyncAsync(refresh:true);}}}
    }
    private async Task<bool> EndedAsync(AppearanceResponse response)
    {
        if(response.Code!="appearance-account-changed")return false;
        await AccountChangedAsync();return true;
    }
    public async Task AccountChangedAsync()
    {
        if(disposed||Blocked)return;
        Blocked=true;NeedsSignIn=false;cancellation.Cancel();State.SignOut();Message="Your account session changed. Reload settings before syncing.";
        await clear();await changed();
    }
    private async Task PublishAsync(bool userEdit=false)
    {
        if(disposed)return;
        AppearanceCacheWrite? written=null;
        if(State.ExportCache() is{}cache){written=await save(cache,userEdit);CacheAvailable=written.Available;}
        if(disposed)return;
        if(!State.IsLoading&&written?.Superseded!=true)await apply(State.Current);
        if(!disposed)await changed();
    }
    public void Dispose(){if(disposed)return;disposed=true;cancellation.Cancel();State.SignOut();if(!Busy)cancellation.Dispose();}
}
