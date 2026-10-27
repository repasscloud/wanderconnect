using Swoovr.Client;

namespace Swoovr.Mobile;

/// <summary>
/// The privacy notice (#99, #22), the same text as the web's /privacy (PrivacyNotice), labelled DRAFT until legal review
/// (#57). Reached from Me, Your data and account, and the deletion screen. Needs no sign-in or network.
/// </summary>
public sealed class PrivacyPage : ContentPage
{
	public PrivacyPage()
	{
		Title = "Privacy notice";
		var body = new VerticalStackLayout { Padding = new Thickness(24, 30), Spacing = 16 };
		body.Children.Add(Ui.Text(PrivacyNotice.DraftLabel, "FieldLabel", "PrivacyDraft"));
		body.Children.Add(Ui.Text("Privacy notice", "PageTitle"));
		body.Children.Add(Ui.Text(PrivacyNotice.Version, "FooterClaims"));
		foreach (var section in PrivacyNotice.Sections)
			body.Children.Add(Ui.Card([Ui.Text(section.Title, "SectionHeading", $"Privacy_{section.Id}"), .. section.Paragraphs.Select(p => (View)Ui.Text(p))]));
		body.Children.Add(Ui.Text(PrivacyNotice.DraftLabel, "FieldLabel"));
		Content = new ScrollView { Content = body };
	}
}
