using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Account standing (#50), as on the web: verification, what to do, actions in force with their end dates and
/// appeal deadlines, decisions about the account, and the member's own reports with support references. Open while
/// restricted or suspended.
/// </summary>
public sealed class StandingPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };

	public StandingPage()
	{
		Title = StandingText.Title;
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		body.Children.Add(Ui.Text(StandingText.Title, "PageTitle"));
		body.Children.Add(Ui.Text(StandingText.Intro, "FooterClaims"));
        body.Children.Add(Ui.Card(Ui.Text("Contact Swoovr support", "SectionHeading"),
            Ui.Button("Contact Support", "StandingSupportWeb", () => Launcher.Default.OpenAsync(new Uri(StandingText.SupportUrl)), primary: false),
            Ui.Button("Email Support", "StandingSupportEmail", () => Launcher.Default.OpenAsync(new Uri("mailto:" + StandingText.SupportEmail)), primary: false),
            Ui.Text(StandingText.SupportHelp, "FooterClaims")));
		if ((await Ui.Members!.StandingAsync()).Value is not { } s)
		{
			body.Children.Add(Ui.Text("Your account standing isn't available right now.", id: "StandingRefused"));
			return;
		}
		var status = new List<View> { Ui.Text($"Status: {StandingText.State(s.State)}", "SectionHeading", "StandingState") };
		if (s.Actions.Count == 0) status.Add(Ui.Text("Nothing to do.", "FooterClaims", "StandingNoActions"));
		foreach (var a in s.Actions)
			status.Add(Ui.Button(StandingText.Action(a.Code), $"StandingAction_{a.Code}", () => a.Code is "age-check" or "dob-correction"
				? Ui.PushAsync(new VerificationPage()) : a.Code == "phone" ? Ui.PushAsync(new PhonePage()) : Shell.Current.Navigation.PopToRootAsync(), primary: false));
		body.Children.Add(Ui.Card([.. status]));
		var v = s.Verification;
		body.Children.Add(Ui.Card(Ui.Text("Verification", "SectionHeading"),
			Ui.Text($"Email: {(v.EmailVerified ? "Verified" : "Not verified")}"),
			Ui.Text($"Mobile number: {(v.PhoneVerified ? "Verified" : v.PhoneRequired ? "Needed" : "Not added")}", id: "StandingPhone"),
			Ui.Text($"Age: {StandingText.Age(v.AgeStatus)}"),
			Ui.Text($"Date of birth: {StandingText.Dob(v)}"),
			Ui.Text($"ID: {StandingText.Id(v.IdStatus)}")));
		var force = new List<View> { Ui.Text("Actions in force", "SectionHeading") };
		if (s.InForce.Count == 0) force.Add(Ui.Text(StandingText.NothingInForce, "FooterClaims", "StandingNothingInForce"));
		foreach (var x in s.InForce)
		{
			force.Add(Ui.Text(StandingText.Sanction(x), "TileName", $"StandingSanction_{x.Id}"));
			force.Add(Ui.Text(x.Reason));
			force.Add(Ui.Text(x.AppealStatus is { } a ? $"Appeal {MemberText.AppealStatus(a)}." : x.AppealBy is { } by ? $"You can appeal until {by:d MMM yyyy}." : "This can't be appealed any more.", "FooterClaims"));
			if (x.AppealStatus is null && x.AppealBy is not null) force.Add(Ui.Button("Appeal", $"StandingAppeal_{x.Id}", () => Ui.PushAsync(new NoticesPage()), primary: false));
		}
		body.Children.Add(Ui.Card([.. force]));
		var decisions = new List<View> { Ui.Text("Decisions about your account", "SectionHeading") };
		if (s.Decisions.Count == 0) decisions.Add(Ui.Text(StandingText.NoDecisions, "FooterClaims", "StandingNoDecisions"));
		decisions.AddRange(s.Decisions.Select((d, i) => (View)Ui.Text(StandingText.Decision(d), id: $"StandingDecision_{i}")));
		body.Children.Add(Ui.Card([.. decisions]));
		var reports = new List<View> { Ui.Text("Your reports", "SectionHeading"), Ui.Text(StandingText.ReferenceHelp, "FooterClaims") };
		if (s.Reports.Count == 0) reports.Add(Ui.Text(StandingText.NoReports, "FooterClaims", "StandingNoReports"));
		foreach (var r in s.Reports)
        {
            reports.Add(Ui.Text(StandingText.Report(r), id: $"StandingReport_{r.Reference}"));
            reports.Add(Ui.Button($"Contact support about {r.Reference}", $"StandingSupport_{r.Reference}",
                () => Launcher.Default.OpenAsync(new Uri(StandingText.SupportLink(r.Reference))), primary: false));
        }
		reports.Add(Ui.Text(StandingText.Privacy, "FooterClaims"));
		body.Children.Add(Ui.Card([.. reports]));
	}
}
