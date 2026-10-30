using Swoovr.Client;
namespace Swoovr.Mobile;
public sealed class PolicyAcceptancePage:ContentPage
{
    private readonly VerticalStackLayout body=new(){Padding=new Thickness(24,30),Spacing=16};
    public PolicyAcceptancePage(){Title="Required policies";Content=new ScrollView{Content=body};}
    protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
    private async Task LoadAsync()
    {
        body.Children.Clear();body.Children.Add(Ui.Text("Required policies","PageTitle"));
        if(!await Ui.SignedInAsync()){body.Children.Add(Ui.Text("Sign in on Me to review required policies."));return;}
        body.Children.Add(Ui.Button("Your data and account","PolicyReviewData",()=>Ui.PushAsync(new DataPage()),primary:false));
        body.Children.Add(Ui.Button("Notices and appeals","PolicyReviewNotices",()=>Ui.PushAsync(new NoticesPage()),primary:false));
        body.Children.Add(Ui.Button("Safety & Support","PolicyReviewSafety",()=>Ui.PushAsync(new SafetyPage()),primary:false));
        var state=(await Ui.Members!.PoliciesAsync()).Value;
        if(state is null){body.Children.Add(Ui.Text("Policy information is unavailable. Safety & Support and your account controls remain available."));return;}
        if(state.RequiredVersion is null){body.Children.Add(Ui.Text("No approved policy version is currently required. Public drafts remain drafts; reading does not record acceptance.",id:"PolicyNone"));return;}
        body.Children.Add(Ui.Text("Version "+state.RequiredVersion));
        if(state.Accepted){body.Children.Add(Ui.Text("Your explicit acceptance is recorded.",id:"PolicyAccepted"));return;}
        var document=(await Ui.Members.ApprovedPoliciesAsync(state.RequiredVersion)).Value;
        if(document is null){body.Children.Add(Ui.Text("The approved source is unavailable; acceptance is disabled."));return;}
        body.Children.Add(Ui.Text("Terms of use","SectionHeading"));body.Children.Add(Ui.Text(document.Terms));
        body.Children.Add(Ui.Text("Community guidelines","SectionHeading"));body.Children.Add(Ui.Text(document.CommunityGuidelines));
        var confirmation=new CheckBox{IsChecked=false,AutomationId="PolicyConfirmation"};
        SemanticProperties.SetDescription(confirmation,"I have read and explicitly accept approved version "+state.RequiredVersion);
        body.Children.Add(Ui.Text("I have read and explicitly accept approved version "+state.RequiredVersion));body.Children.Add(confirmation);
        var status=Ui.Text("",id:"PolicyResult");Button? save=null;
        save=Ui.Button("Accept this version","PolicyAccept",async()=>
        {
            if(!confirmation.IsChecked)return;save!.IsEnabled=false;
            var result=await Ui.Members.AcceptPoliciesAsync(state.RequiredVersion,true);
            confirmation.IsChecked=false;status.Text=result.Ok?"Your explicit acceptance was recorded.":"Acceptance was not recorded. Reload and review the current version.";
            if(result.Ok)await Navigation.PopAsync();
        });
        save.IsEnabled=false;confirmation.CheckedChanged+=(_,e)=>save.IsEnabled=e.Value;
        body.Children.Add(save);body.Children.Add(status);
        body.Children.Add(Ui.Text("Acceptance does not remove account restrictions. Deletion, export, notices, appeals and support remain available."));
    }
}
