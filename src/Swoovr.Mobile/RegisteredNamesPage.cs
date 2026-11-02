using Swoovr.Contracts;

namespace Swoovr.Mobile;

public sealed class RegisteredNamesPage : ContentPage
{
    private readonly Entry first=new(){Placeholder="First or only name",MaxLength=100,AutomationId="RegisteredFirstName"};
    private readonly Entry last=new(){Placeholder="Last name",MaxLength=100,AutomationId="RegisteredLastName"};
    private readonly CheckBox mononym=new(){AutomationId="RegisteredMononym"},confirm=new(){AutomationId="RegisteredNamesConfirm"};
    private readonly Label status=Ui.Text(null,id:"RegisteredNamesStatus");
    private RegisteredNamesView? view;
    public RegisteredNamesPage()
    {
        Title="Registered names";
        mononym.CheckedChanged+=(_,e)=>last.IsEnabled=!e.Value;
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=24,Spacing=16,Children={
            Ui.Text("Confirm your registered names","PageTitle"),
            Ui.Text("These self-declared account names are separate from your public name and verified identity. Confirm or fill names supplied by Apple or Google before continuing."),
            Ui.Text("First or only name"),first,new HorizontalStackLayout{Children={mononym,Ui.Text("I have one name")}},Ui.Text("Last name"),last,
            new HorizontalStackLayout{Children={confirm,Ui.Text("I confirm these names. Changing them removes ID Verified until I submit ID again.")}},
            Ui.Button("Confirm registered names","RegisteredNamesSave",SaveAsync),status,
            Ui.Button("Your data and account","RegisteredNamesData",()=>Ui.PushAsync(new DataPage()),primary:false),
            Ui.Button("Privacy notice","RegisteredNamesPrivacy",()=>Ui.PushAsync(new PrivacyPage()),primary:false),
            Ui.Button("Contact Support","RegisteredNamesSupport",()=>Launcher.Default.OpenAsync(new Uri("https://swoovr.com/support")),primary:false)
        }}};
    }
    protected override async void OnAppearing()
    {
        base.OnAppearing();var result=await Ui.Members!.RegisteredNamesAsync();view=result.Value;
        if(view is null){status.Text="Registered names are unavailable. Try again.";return;}
        first.Text=view.FirstName;last.Text=view.LastName;mononym.IsChecked=view.Mononym;
    }
    private async Task SaveAsync()
    {
        if(view is null)return;
        var result=await Ui.Members!.SaveRegisteredNamesAsync(new(first.Text,mononym.IsChecked?null:last.Text,mononym.IsChecked,view.Revision,confirm.IsChecked));
        if(!result.Ok){status.Text=result.Status==409?"Your names changed in another session. Reopen this page.":"Enter both names or choose one name, and explicitly confirm.";return;}
        await Navigation.PopAsync();
    }
}
