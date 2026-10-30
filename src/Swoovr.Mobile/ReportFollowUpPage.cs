using Swoovr.Client;
namespace Swoovr.Mobile;
/// <summary>Routine and serious reports always offer the same approved support/complaint channels.</summary>
public sealed class ReportFollowUpPage:ContentPage
{
    public ReportFollowUpPage()
    {
        Title="Report submitted";
        Content=new ScrollView{Content=new VerticalStackLayout{Padding=new Thickness(24,30),Spacing=16,Children={
            Ui.Text("Report submitted","PageTitle"),Ui.Text(ReportFollowUpText.Confirmation,id:"ReportFollowUp"),
            Ui.Button("Notices and reports","ReportNotices",()=>Ui.PushAsync(new NoticesPage()),primary:false),
            Ui.Button("Account standing and support reference","ReportStanding",()=>Ui.PushAsync(new StandingPage()),primary:false),SupportCard()}}};
    }
    public static View SupportCard()=>Ui.Card(Ui.Text("Support and complaints","SectionHeading"),
        Ui.Text(ReportFollowUpText.SafetyHelp),Ui.Button("Safety & Support","ReportSafetySupport",()=>Ui.PushAsync(new SafetyPage()),primary:false),
        Ui.Text(ReportFollowUpText.ComplaintHelp),Ui.Button("Contact Support","ReportComplaintWeb",()=>Launcher.Default.OpenAsync(new Uri(StandingText.SupportUrl)),primary:false),
        Ui.Button("Email Support","ReportComplaintEmail",()=>Launcher.Default.OpenAsync(new Uri("mailto:"+StandingText.SupportEmail)),primary:false),Ui.Text(ReportFollowUpText.Availability,"FooterClaims"));
}
