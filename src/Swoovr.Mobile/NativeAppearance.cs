using System.Text;
using System.Text.Json;
using Swoovr.Client;
using Swoovr.Contracts;
namespace Swoovr.Mobile;

/// <summary>Personal palette and owner-scoped cosmetic cache only. Authentication remains in AppSignIn/Keycloak.</summary>
public static class NativeAppearance
{
    private const string GuestKey="swoovr.appearance.guest.v1",AccountKey="swoovr.appearance.account.v1";
    private static readonly JsonSerializerOptions JsonOptions=new(JsonSerializerDefaults.Web);
    private static Application? app;
    private static string? owner;
    private static bool applying;
    private static readonly SemaphoreSlim accountGate=new(1,1);
    public static bool Probing { get; private set; }
    public static bool OfflineSession { get; private set; }
    public static bool NeedsReload { get; private set; }
    public static bool HasAccountOwner=>owner is not null;
    public static AppearancePreference Guest { get; private set; }=AppearancePreferences.Default;
    public static AppearancePreference Current { get; private set; }=AppearancePreferences.Default;
    public static AppearanceAccountCoordinator? Coordinator { get; private set; }
    public static bool StorageAvailable { get; private set; }=true;
    public static event Action? Changed;
    public static void Initialize(Application application)
    {
        app=application;Guest=ReadGuest();Apply(Guest);
        application.RequestedThemeChanged+=(_,_)=>{if(!applying&&Current.Mode=="system"){Apply(Current);Changed?.Invoke();}};
        Connectivity.Current.ConnectivityChanged+=(_,_)=>MainThread.BeginInvokeOnMainThread(async()=>{await EnsureAccountAsync(MainPage.Auth);});
    }
    private static AppearancePreference ReadGuest()
    {
        try{var text=Preferences.Default.Get(GuestKey,"");var value=text.Length is >0 and <4096?JsonSerializer.Deserialize<AppearancePreference>(text,JsonOptions):null;return AppearancePreferences.IsValid(value)?value!:AppearancePreferences.Default;}
        catch(Exception){StorageAvailable=false;return AppearancePreferences.Default;}
    }
    private static AppearanceAccountCache? ReadCache(string scope)
    {
        try
        {
            var text=Preferences.Default.Get(AccountKey,"");if(text.Length is 0 or >32768)return null;
            var record=JsonSerializer.Deserialize<DeviceCache>(text,JsonOptions);
            if(record is not{Version:1}||record.Owner!=scope||record.Json is null||record.Json.Length>16384){Preferences.Default.Remove(AccountKey);return null;}
            return JsonSerializer.Deserialize<AppearanceAccountCache>(record.Json,JsonOptions);
        }
        catch(Exception){StorageAvailable=false;return null;}
    }
    /// <summary>Only namespaces a cosmetic cache using stored token fields. This performs no JWT authentication.</summary>
    public static async Task<string?> StoredOwnerAsync()
    {
        if(MobileEndpoints.SignIn is not{}options)return null;
        try
        {
            var text=await new SecureTokenStore().ReadAsync();if(text is null||text.Length>131072)return null;
            var tokens=JsonSerializer.Deserialize<NativeTokens>(text);var parts=tokens?.AccessToken.Split('.');if(parts?.Length!=3||parts[1].Length>32768)return null;
            var payload=parts[1].Replace('-','+').Replace('_','/');payload=payload.PadRight((payload.Length+3)/4*4,'=');
            using var doc=JsonDocument.Parse(Convert.FromBase64String(payload));
            if(!doc.RootElement.TryGetProperty("iss",out var iss)||iss.ValueKind!=JsonValueKind.String||!doc.RootElement.TryGetProperty("sub",out var sub)||sub.ValueKind!=JsonValueKind.String)return null;
            var issuer=iss.GetString();var subject=sub.GetString();
            return string.IsNullOrWhiteSpace(issuer)||string.IsNullOrWhiteSpace(subject)||issuer.TrimEnd('/')!=options.Authority.AbsoluteUri.TrimEnd('/')?null:AppearanceOwner.ForIdentity(issuer,subject);
        }
        catch(Exception){return null;}
    }
    private static string? CachedOwner()
    {
        try{var text=Preferences.Default.Get(AccountKey,"");if(text.Length is 0 or >32768)return null;var record=JsonSerializer.Deserialize<DeviceCache>(text,JsonOptions);return record is{Version:1}&&AppearanceOwner.Valid(record.Owner)?record.Owner:null;}catch(Exception){return null;}
    }
    public static async Task RestoreAsync()
    {
        var scope=await StoredOwnerAsync()??CachedOwner();if(scope is null){ForgetAccount();return;}
        owner=scope;var cached=ReadCache(scope);var state=new AppearanceSyncState(Guest);state.BeginAccount(cached);
        if(!state.IsLoading)Apply(state.Current);
    }
    private static void Activate(AppSignIn auth,string scope)
    {
        if(owner==scope&&Coordinator is not null)return;
        Coordinator?.Dispose();Coordinator=null;
        if(owner!=scope){RemoveCache();Apply(Guest);}owner=scope;NeedsReload=false;
        Coordinator=new(auth.Members!,scope,Guest,ReadCache(scope),
            preference=>MainThread.InvokeOnMainThreadAsync(()=>{if(owner==scope)Apply(preference);}),
            (value,_)=>Task.FromResult(new AppearanceCacheWrite(SaveCache(scope,value))),
            ()=>MainThread.InvokeOnMainThreadAsync(()=>ForgetAccount(scope)),
            ()=>MainThread.InvokeOnMainThreadAsync(()=>Changed?.Invoke()));
        if(!Coordinator.State.IsLoading)Apply(Coordinator.State.Current);
    }
    public static async Task EnsureAccountAsync(AppSignIn auth)
    {
        if(!await accountGate.WaitAsync(0))return;
        try
        {
            Probing=true;Changed?.Invoke();
            if(!auth.Available||auth.Members is null){ForgetAccount();return;}
            var scope=await StoredOwnerAsync();if(scope is null){var cachedScope=owner??CachedOwner();if(cachedScope is not null){Activate(auth,cachedScope);OfflineSession=true;}else ForgetAccount();return;}
            Activate(auth,scope);
            bool usable;
            try{usable=await auth.IsSignedInAsync();}catch(Exception error)when(error is HttpRequestException or TaskCanceledException or NativeSignInException){usable=false;}
            var after=await StoredOwnerAsync();
            if(after is null){OfflineSession=true;return;}
            if(after!=scope){Activate(auth,after);scope=after;}
            OfflineSession=!usable;
            if(usable&&Coordinator is{}sync)await sync.SyncAsync(refresh:true);
        }
        finally{Probing=false;accountGate.Release();Changed?.Invoke();}
    }
    public static async Task ChooseAsync(AppearancePreference preference)
    {
        if(!AppearancePreferences.IsValid(preference))return;
        if(Coordinator is{}sync){await sync.ChangeAsync(preference,syncNow:false);await EnsureAccountAsync(MainPage.Auth);return;}
        if(owner is not null)return;
        Guest=preference;try{Preferences.Default.Set(GuestKey,JsonSerializer.Serialize(preference,JsonOptions));StorageAvailable=true;}catch(Exception){StorageAvailable=false;}
        Apply(preference);Changed?.Invoke();
    }
    private static bool SaveCache(string scope,AppearanceAccountCache value)
    {
        if(owner!=scope)return false;
        try{Preferences.Default.Set(AccountKey,JsonSerializer.Serialize(new DeviceCache(1,scope,JsonSerializer.Serialize(value,JsonOptions)),JsonOptions));return StorageAvailable=true;}
        catch(Exception){return StorageAvailable=false;}
    }
    private static void RemoveCache(){try{Preferences.Default.Remove(AccountKey);}catch(Exception){StorageAvailable=false;}}
    public static void ForgetAccount(string? expected=null)
    {
        if(expected is not null&&owner!=expected)return;
        Coordinator?.Dispose();Coordinator=null;owner=null;NeedsReload=expected is not null;OfflineSession=false;RemoveCache();Apply(Guest);Changed?.Invoke();
    }
    private static void Apply(AppearancePreference preference)
    {
        if(app is null)return;
        if(!MainThread.IsMainThread){MainThread.BeginInvokeOnMainThread(()=>Apply(preference));return;}
        Current=AppearancePreferences.IsValid(preference)?preference:AppearancePreferences.Default;
        if(applying)return;applying=true;
        try
        {
            app.UserAppTheme=Current.Mode switch{"light"=>AppTheme.Light,"dark"=>AppTheme.Dark,_=>AppTheme.Unspecified};
            var scheme=AppearancePreferences.Schemes.First(s=>s.Id==Current.Scheme);
            var dark=Current.Mode=="dark"||Current.Mode=="system"&&app.RequestedTheme==AppTheme.Dark;var palette=dark?scheme.Dark:scheme.Light;
            foreach(var pair in new Dictionary<string,string>{{"ThemeBackground",palette.Bg},{"ThemeSurface",palette.Surface},{"ThemeText",palette.Text},{"ThemeMuted",palette.Muted},{"ThemePrimary",palette.Primary},{"ThemeOnPrimary",palette.OnPrimary},{"ThemeAccent",palette.Accent},{"ThemeSoft",palette.Soft},{"ThemeSoftText",palette.SoftText},{"ThemeBorder",palette.Border}})app.Resources[pair.Key]=Color.FromArgb(pair.Value);
        }
        finally{applying=false;}
    }
    private sealed record DeviceCache(int Version,string Owner,string Json);
}
