using Swoovr.Client;
namespace Swoovr.Mobile;

/// <summary>Shared first-use/meeting advice; no legal acceptance, hidden analytics or forced acknowledgement.</summary>
public sealed class SafetyAdvicePage : ContentPage
{
    public SafetyAdvicePage()
    {
        Title = "Connection and meeting advice";
        var body = new VerticalStackLayout { Padding = new Thickness(24, 30), Spacing = 16 };
        body.Children.Add(Ui.Text(SafetyAdvice.Title, "PageTitle"));
        body.Children.Add(Ui.Text(SafetyAdvice.Introduction));
        body.Children.Add(Ui.Text("Before connecting", "SectionHeading"));
        foreach(var advice in SafetyAdvice.BeforeConnecting) body.Children.Add(Ui.Text(advice));
        body.Children.Add(Ui.Text("Before meeting", "SectionHeading"));
        foreach(var advice in SafetyAdvice.BeforeMeeting) body.Children.Add(Ui.Text(advice));
        body.Children.Add(Ui.Text(SafetyAdvice.Responsibility));
        body.Children.Add(Ui.Button("Safety & Support", "AdviceSupport", () => Ui.PushAsync(new SafetyPage()), primary:false));
        body.Children.Add(Ui.Button("Optional meeting safety", "AdviceMeetingSafety", () => Ui.PushAsync(new MeetingSafetyPage()), primary:false));
        body.Children.Add(Ui.Text("Advice version " + SafetyAdvice.Version + ". Draft legal documents remain drafts and are not in force.", "FooterClaims"));
        Content = new ScrollView { Content = body };
    }
}
