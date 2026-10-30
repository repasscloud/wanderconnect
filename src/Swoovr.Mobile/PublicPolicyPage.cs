using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>Offline public draft reader. No sign-in, API call, acceptance checkbox or write.</summary>
public sealed class PublicPolicyPage : ContentPage
{
    public PublicPolicyPage(string id)
    {
        var document=PublicPolicyDrafts.Find(id) ?? throw new ArgumentOutOfRangeException(nameof(id));
        Title=document.Title;
        var body=new VerticalStackLayout { Padding=new Thickness(24,30), Spacing=16 };
        body.Children.Add(Ui.Text(PublicPolicyDrafts.DraftLabel,"FieldLabel","PolicyDraft"));
        body.Children.Add(Ui.Text(document.Title,"PageTitle"));
        body.Children.Add(Ui.Text(PublicPolicyDrafts.Version,"FooterClaims","PolicyVersion"));
        foreach(var rule in document.Rules)body.Children.Add(Ui.Text("• "+rule));
        foreach(var paragraph in document.Paragraphs)body.Children.Add(Ui.Text(paragraph));
        body.Children.Add(Ui.Button("Contact Support","PolicySupport",()=>Launcher.Default.OpenAsync(new Uri(StandingText.SupportUrl)),primary:false));
        body.Children.Add(Ui.Button("Privacy notice (draft)","PolicyPrivacy",()=>Ui.PushAsync(new PrivacyPage()),primary:false));
        body.Children.Add(Ui.Text(PublicPolicyDrafts.DraftLabel,"FieldLabel"));
        Content=new ScrollView { Content=body };
    }
}
