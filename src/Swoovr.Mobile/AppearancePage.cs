using Swoovr.Client;
using Swoovr.Contracts;
namespace Swoovr.Mobile;

public sealed class AppearancePage:ContentPage
{
    private readonly Dictionary<string,RadioButton> schemes=[],modes=[];
    private readonly Dictionary<string,Label> selected=[];
    private readonly Label status=Ui.Text("Loading appearance…",id:"AppearanceStatus");
    private readonly Button reset,retry,local,remote;
    private bool rendering;
    public AppearancePage()
    {
        Title="Appearance";var body=new VerticalStackLayout{Padding=new Thickness(24,30),Spacing=16};Content=new ScrollView{Content=body};
        body.Children.Add(Ui.Text("Make yourself at home.","PageTitle"));body.Children.Add(Ui.Text("Eight personal schemes, all free. This changes only your own interface.","Subtitle"));
        var group="schemes-"+Guid.NewGuid().ToString("N");
        foreach(var section in AppearancePreferences.Schemes.GroupBy(s=>s.Group))
        {
            body.Children.Add(Ui.Text(section.Key,"SectionHeading"));
            foreach(var scheme in section)
            {
                var radio=new RadioButton{Content=scheme.Name,Value=scheme.Id,GroupName=group,AutomationId="AppearanceScheme_"+scheme.Id};schemes[scheme.Id]=radio;
                radio.CheckedChanged+=async(_,e)=>{if(e.Value&&!rendering)await NativeAppearance.ChooseAsync(new(scheme.Id,NativeAppearance.Current.Mode));};
                var marker=Ui.Text("",id:"AppearanceSelected_"+scheme.Id);selected[scheme.Id]=marker;
                body.Children.Add(Ui.Card(radio,marker,new HorizontalStackLayout{Spacing=12,Children={Preview("Light",scheme.Light),Preview("Dark",scheme.Dark)}}));
            }
        }
        body.Children.Add(Ui.Text("Display mode","SectionHeading"));var modeGroup="mode-"+Guid.NewGuid().ToString("N");
        foreach(var mode in new[]{"system","light","dark"})
        {
            var radio=new RadioButton{Content=char.ToUpperInvariant(mode[0])+mode[1..],Value=mode,GroupName=modeGroup,AutomationId="AppearanceMode_"+mode};modes[mode]=radio;
            radio.CheckedChanged+=async(_,e)=>{if(e.Value&&!rendering)await NativeAppearance.ChooseAsync(new(NativeAppearance.Current.Scheme,mode));};body.Children.Add(radio);
        }
        body.Children.Add(status);reset=Ui.Button("Reset appearance","AppearanceReset",()=>NativeAppearance.ChooseAsync(AppearancePreferences.Default),false);body.Children.Add(reset);
        retry=Ui.Button("Retry sync","AppearanceRetry",()=>NativeAppearance.EnsureAccountAsync(MainPage.Auth),false);body.Children.Add(retry);
        local=Ui.Button("Keep this device’s choice","AppearanceKeepLocal",()=>NativeAppearance.Coordinator?.ResolveAsync(true)??Task.CompletedTask,false);body.Children.Add(local);
        remote=Ui.Button("Use the account choice","AppearanceUseAccount",()=>NativeAppearance.Coordinator?.ResolveAsync(false)??Task.CompletedTask,false);body.Children.Add(remote);
        body.Children.Add(Ui.Card(Ui.Text("Preview — fictional Alex,32","SectionHeading"),Ui.Text("Good coffee, coastal walks and conversation."),Ui.Button("Sample message — nothing is sent","AppearancePreview",()=>Task.CompletedTask)));
        body.Children.Add(Ui.Text("Your scheme is private. It does not change matching, visibility, permissions or benefits.","FooterClaims"));Update();
    }
    private static View Preview(string name,AppearancePalette palette)
    {
        var row=new HorizontalStackLayout{Spacing=4,Children={new BoxView{Color=Color.FromArgb(palette.Bg),WidthRequest=32,HeightRequest=24},new BoxView{Color=Color.FromArgb(palette.Primary),WidthRequest=32,HeightRequest=24}}};
        return new VerticalStackLayout{Spacing=4,Children={Ui.Text(name,"FooterClaims"),row}};
    }
    protected override async void OnAppearing(){base.OnAppearing();NativeAppearance.Changed+=Update;Update();await NativeAppearance.EnsureAccountAsync(MainPage.Auth);Update();}
    protected override void OnDisappearing(){NativeAppearance.Changed-=Update;base.OnDisappearing();}
    private static void Tint(RadioButton radio)
    {
#if ANDROID
        if(radio.Handler?.PlatformView is Android.Widget.RadioButton control)
        {
            var value=NativeAppearance.Current;var scheme=AppearancePreferences.Schemes.First(s=>s.Id==value.Scheme);
            var dark=value.Mode=="dark"||value.Mode=="system"&&Application.Current?.RequestedTheme==AppTheme.Dark;
            control.ButtonTintList=Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.ParseColor((dark?scheme.Dark:scheme.Light).Primary));
        }
#endif
    }
    private void Update()
    {
        if(!MainThread.IsMainThread){MainThread.BeginInvokeOnMainThread(Update);return;}
        rendering=true;
        try
        {
            var sync=NativeAppearance.Coordinator;var current=NativeAppearance.Current;var editable=sync is null?!NativeAppearance.HasAccountOwner:!sync.Blocked&&!sync.State.IsLoading&&!sync.State.IsRevisionExhausted;
            foreach(var pair in schemes){pair.Value.IsChecked=pair.Key==current.Scheme;pair.Value.IsEnabled=editable;Tint(pair.Value);selected[pair.Key].Text=pair.Value.IsChecked?"Selected":"";}
            foreach(var pair in modes){pair.Value.IsChecked=pair.Key==current.Mode;pair.Value.IsEnabled=editable;Tint(pair.Value);}
            reset.IsEnabled=editable;retry.IsVisible=sync is not null||NativeAppearance.NeedsReload;retry.IsEnabled=sync?.Busy!=true&&!NativeAppearance.Probing;
            local.IsVisible=remote.IsVisible=sync is not null&&(sync.State.Conflict is not null||sync.MissingConflict);
            status.Text=NativeAppearance.OfflineSession?"Account sync is waiting for a connection. Your device choice still applies.":NativeAppearance.NeedsReload?"Your account session changed. Retry sync to reload it.":sync?.Message??(NativeAppearance.StorageAvailable?"Saved on this device. Sign in to sync your account choice.":"Applied for this visit. Device storage is unavailable.");
            if(sync is{CacheAvailable:false,Blocked:false})status.Text+=" Device cache is unavailable.";
        }
        finally{rendering=false;}
    }
}
