using System.Net;
using System.Net.Http.Json;
using Swoovr.Client;
using Swoovr.Contracts;
static class AppearanceCoordinatorChecks
{
    public static async Task Run()
    {
        var count=0;void Check(bool ok,string name){count++;if(!ok)throw new Exception("Appearance coordinator: "+name);}
        var owner=new string('a',64);var backend=new Backend(owner);using var http=new HttpClient(backend){BaseAddress=new("https://appearance.example.test/")};
        AppearanceAccountCache? cache=null;AppearancePreference? displayed=null;var cleared=false;
        using var sync=new AppearanceAccountCoordinator(new MemberClient(http),owner,new("coral","system"),null,
            preference=>{displayed=preference;return Task.CompletedTask;},(value,_)=>{cache=value;return Task.FromResult(new AppearanceCacheWrite(true));},()=>{cache=null;cleared=true;return Task.CompletedTask;},()=>Task.CompletedTask);
        await sync.SyncAsync();Check(backend.Snapshot==new AppearanceSnapshot(new("coral","system"),1)&&!sync.State.HasPendingSave,"known absence seeds guest choice once");
        backend.Offline=true;await sync.ChangeAsync(new("pride","dark"));var pending=cache!.Pending!;Check(sync.State.HasPendingSave&&displayed==new AppearancePreference("pride","dark"),"offline edit applies and retains pending retry");
        await sync.ChangeAsync(new("eucalyptus","light"));Check(cache!.Pending!.ChangeId==pending.ChangeId&&cache.Current==new AppearancePreference("eucalyptus","light"),"newer offline edit keeps original unresolved attempt");
        backend.Offline=false;await sync.SyncAsync();
        Check(backend.Snapshot==new AppearanceSnapshot(new("eucalyptus","light"),3)&&!sync.State.HasPendingSave,"recovery acknowledges old attempt then sends newer choice");
        Check(backend.Updates.Count(u=>u.ChangeId==pending.ChangeId)>=3,"unknown retry identity survives each attempt");
        backend.Offline=true;await sync.ChangeAsync(new("union","system"));backend.Snapshot=new(new("trans","dark"),4);backend.Offline=false;await sync.SyncAsync();
        Check(sync.State.Conflict?.Revision==4&&sync.State.Current==new AppearancePreference("union","system"),"remote conflict never silently overwrites local edit");
        await sync.ResolveAsync(true);Check(backend.Snapshot==new AppearanceSnapshot(new("union","system"),5),"explicit local resolution writes against observed current revision");
        backend.Unauthorized=true;await sync.ChangeAsync(new("violet","dark"));Check(sync.NeedsSignIn&&!sync.Blocked&&cache?.Pending is not null,"unconfirmed authentication preserves the pending owner-bound edit");backend.Unauthorized=false;
        backend.Owner=new string('b',64);await sync.ChangeAsync(new("violet","dark"));
        Check(sync.Blocked&&cleared&&cache is null&&backend.Snapshot==new AppearanceSnapshot(new("union","system"),5),"switched session clears old cache without mutating another owner");
        Console.WriteLine($"PASS: shared appearance coordinator ({count} fake HTTP/local-state checks; no device/storage/authentication claim).");
    }
    private sealed class Backend(string owner):HttpMessageHandler
    {
        public string Owner=owner;public bool Offline,Unauthorized;public AppearanceSnapshot? Snapshot;public Guid? LastId;public readonly List<AppearanceUpdate> Updates=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            AppearanceUpdate? update=request.Method==HttpMethod.Put?await request.Content!.ReadFromJsonAsync<AppearanceUpdate>(token):null;if(update is not null)Updates.Add(update);
            if(Unauthorized)return new(HttpStatusCode.Unauthorized);
            if(Offline)throw new HttpRequestException("Fictional offline");
            if(request.Headers.GetValues(AppearanceOwner.Header).Single()!=Owner)return new(HttpStatusCode.Conflict){Content=JsonContent.Create(new AppearanceConflict("appearance-account-changed",null))};
            HttpResponseMessage response;
            if(update is null)response=Snapshot is null?new(HttpStatusCode.NoContent):new(HttpStatusCode.OK){Content=JsonContent.Create(Snapshot)};
            else if(update.ChangeId==LastId)response=new(HttpStatusCode.OK){Content=JsonContent.Create(Snapshot)};
            else if(update.ExpectedRevision!=(Snapshot?.Revision??0))response=new(HttpStatusCode.Conflict){Content=JsonContent.Create(new AppearanceConflict("appearance-conflict",Snapshot))};
            else{LastId=update.ChangeId;Snapshot=new(update.Preference,update.ExpectedRevision+1);response=new(HttpStatusCode.OK){Content=JsonContent.Create(Snapshot)};}
            response.Headers.Add(AppearanceOwner.Header,Owner);return response;
        }
    }
}
