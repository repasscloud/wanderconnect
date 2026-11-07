using Swoovr.Contracts;
using Swoovr.Client;
namespace Swoovr.Mobile;

/// <summary>Native security controls; all secrets, OTPs and recovery codes stay in Keycloak's system-browser flow.</summary>
public sealed class MfaPage : ContentPage
{
    private readonly VerticalStackLayout body = new() { Padding = new Thickness(24,30), Spacing = 16 };
    public MfaPage() { Title="Multi-factor authentication"; Content=new ScrollView { Content=body }; }
    protected override async void OnAppearing(){base.OnAppearing();await LoadAsync();}
    private async Task LoadAsync()
    {
        body.Children.Clear();body.Children.Add(Ui.Text("Multi-factor authentication","PageTitle"));
        if(Ui.Members is null)return;
        var result=await Ui.Members.MfaAsync();
        if(result.Value is not{}view){body.Children.Add(Ui.Text("MFA status is unavailable. Try again or contact Support."));return;}
        body.Children.Add(Ui.Text(MfaText.Status(view.Status)+(view.Required?" · Required for this account":" · Optional")));
        if(view.ResetPending){body.Children.Add(Ui.Text("Contact Support to complete your account recovery review."));return;}
        body.Children.Add(Ui.Button(view.Methods.Contains("otp")?"Replace authenticator":"Set up authenticator","MfaSetup",()=>ActionAsync("CONFIGURE_TOTP")));
        if(view.Methods.Contains("otp"))
        {
            if(view.RecoveryCodesAvailable)body.Children.Add(Ui.Button("Generate recovery codes","MfaRecovery",()=>ActionAsync("CONFIGURE_RECOVERY_AUTHN_CODES"),primary:false));
            if(!view.Required)body.Children.Add(Ui.Button("Disable MFA","MfaDisable",DisableAsync,primary:false));
        }
    }
    private async Task ActionAsync(string action)
    {
        try{await MainPage.Auth.SignInAsync(reauthenticate:true,securityAction:action);await LoadAsync();}
        catch(Exception error)when(error is HttpRequestException or Swoovr.Client.NativeSignInException){await DisplayAlertAsync("Account security","Verification could not be completed. Try again.","OK");}
    }
    private async Task DisableAsync()
    {
        if(!await DisplayAlertAsync("Disable MFA?","Your existing sessions will end. A recent MFA sign-in is required.","Disable MFA","Cancel"))return;
        var result=await Ui.Members!.DisableMfaAsync();
        if(result.Ok){MainPage.Auth.ForgetLocally();await DisplayAlertAsync("MFA disabled","Sign in again to continue.","OK");await Shell.Current.Navigation.PopToRootAsync();}
        else if(result.Code=="stale-session"){await MainPage.Auth.SignInAsync(reauthenticate:true);await LoadAsync();}
        else await DisplayAlertAsync("MFA unchanged","Review your account security or contact Support.","OK");
    }
}
