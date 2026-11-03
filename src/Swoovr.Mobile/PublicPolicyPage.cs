using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>Offline reader for the approved terms and community guidelines. No sign-in, API call, acceptance checkbox or write.</summary>
public sealed class PublicPolicyPage : ContentPage
{
    public PublicPolicyPage(string id)
    {
        var document=PublicPolicies.Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id));
        Title=document.Title;
        var body=new VerticalStackLayout { Padding=new Thickness(24,30), Spacing=16 };
        body.Children.Add(Ui.Text(document.Title,"PageTitle"));
        body.Children.Add(Ui.Text("Last updated 2 November 2026 · Version "+PublicPolicies.Version.Split('-')[^1],"FooterClaims","PolicyVersion"));
        foreach(var rule in document.Rules)body.Children.Add(Ui.Text("• "+rule));
        foreach(var paragraph in document.Paragraphs)body.Children.Add(Ui.Text(paragraph));
        body.Children.Add(Ui.Button("Contact Support","PolicySupport",()=>Launcher.Default.OpenAsync(new Uri(StandingText.SupportUrl)),primary:false));
        body.Children.Add(Ui.Button("Privacy notice","PolicyPrivacy",()=>Ui.PushAsync(new PrivacyPage()),primary:false));
        Content=new ScrollView { Content=body };
    }
}
