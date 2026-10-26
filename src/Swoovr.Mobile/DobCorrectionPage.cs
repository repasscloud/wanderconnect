using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// The one-time date-of-birth correction (owner, Q58, D166), shown over the Me tab when an approved ID check reported a
/// different date: the member sets their date to the one on their ID, the API reads Didit's decision again, and either
/// verifies it or sends it to an admin's review. The document's date is never shown or stored.
/// </summary>
public sealed class DobCorrectionPage : ContentPage
{
	private readonly DatePicker date = new() { AutomationId = "DobCorrectionDate", MaximumDate = DateTime.Today, MinimumDate = DateTime.Today.AddYears(-120) };
	private readonly Label status = Ui.Text(null, id: "DobCorrectionStatus");
	private readonly Func<MemberAccount, Task> changed;

	public DobCorrectionPage(DateOnly? current, Func<MemberAccount, Task> changed)
	{
		this.changed = changed;
		Title = "Date of birth";
		if (current is { } dob) date.Date = dob.ToDateTime(TimeOnly.MinValue);
		Content = new ScrollView
		{
			Content = new VerticalStackLayout
			{
				Padding = new Thickness(24, 40), Spacing = 16,
				Children =
				{
					Ui.Text("Your date of birth doesn't match your ID", "PageTitle", "DobCorrectionHeading"),
					Ui.Text("Your ID check passed, but the date of birth on your ID is different from the one on your Swoovr account. Set your date of birth to the one shown on your ID to finish verification. You can do this once; if it still doesn't match, Swoovr's team will check it."),
					Ui.Labelled("Date of birth on your ID", date),
					Ui.Button("Confirm", "DobCorrectionConfirm", ConfirmAsync),
					status,
				},
			},
		};
	}

	// Asked once: the hardware back button doesn't skip it.
	protected override bool OnBackButtonPressed() => true;

	private async Task ConfirmAsync()
	{
		if (date.Date is not { } picked) return;
		var result = await Ui.Members!.CorrectDateOfBirthAsync(DateOnly.FromDateTime(picked));
		if (result.Value is { } updated)
		{
			await Navigation.PopModalAsync();
			await changed(updated);
			return;
		}
		status.Text = result.Code switch
		{
			ProblemCodes.InvalidAge => "Enter the date of birth shown on your ID.",
			ProblemCodes.VerificationProviderFailed => "We couldn't check your ID just now. Try again shortly.",
			ProblemCodes.DobCorrectionNotOpen => "There's nothing to correct now.",
			_ => "That didn't work. Try again.",
		};
		if (result.Code == ProblemCodes.DobCorrectionNotOpen) await Navigation.PopModalAsync();
	}
}
