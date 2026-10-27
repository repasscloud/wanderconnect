using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Notices and reports (D131, D135), as on the web: notices about actions on the account with an appeal (once per
/// action, within 30 days) and the status of the member's own reports (never the outcome). Open even while restricted
/// or suspended (D160), so a member can read why and appeal.
/// </summary>
public sealed class NoticesPage : ContentPage
{
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
	private readonly Label status = Ui.Text(null, id: "NoticesStatus");
	private Guid? appealing;

	public NoticesPage()
	{
		Title = "Notices and reports";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync();
		foreach (var notice in shown.Where(n => !n.Read)) await Ui.Members!.ReadNoticeAsync(notice.Id);
	}

	private List<NoticeView> shown = [];

	private async Task LoadAsync()
	{
		body.Children.Clear();
		body.Children.Add(Ui.Text("Notices and reports", "PageTitle"));
		shown = (await Ui.Members!.NoticesAsync()).Value ?? [];
		// A barred member can't list their reports, so this may be empty.
		var reports = (await Ui.Members.MyReportsAsync()).Value ?? [];
		var rows = new List<View> { Ui.Text("About your account", "SectionHeading") };
		if (shown.Count == 0) rows.Add(Ui.Text("No notices.", "FooterClaims", "NoticesEmpty"));
		foreach (var notice in shown)
		{
			rows.Add(Ui.Text($"{MemberText.NoticeTitle(notice.Kind)} · {notice.CreatedAt:d MMM yyyy}", "TileName", $"Notice_{notice.Id}"));
			if (notice.Sanction is not { } sanction) continue;
			rows.Add(Ui.Text(sanction.Reason, id: $"NoticeReason_{notice.Id}"));
			if (sanction.EndsAt is { } end && sanction.Live) rows.Add(Ui.Text($"Until {end:d MMM yyyy}.", "FooterClaims"));
			if (sanction.AppealStatus is { } appeal) rows.Add(Ui.Text($"Appeal: {MemberText.AppealStatus(appeal)}", "FooterClaims", $"AppealStatus_{notice.Id}"));
			else if (MemberText.Appealable(notice)) rows.Add(appealing == sanction.Id ? AppealForm(sanction.Id)
				: Ui.Button("Appeal", $"AppealOpen_{sanction.Id}", async () => { appealing = sanction.Id; await LoadAsync(); }, primary: false));
		}
		rows.Add(Ui.Detach(status));
		body.Children.Add(Ui.Card([.. rows]));
		var mine = new List<View> { Ui.Text("Your reports", "SectionHeading") };
		if (reports.Count == 0) mine.Add(Ui.Text("You haven't reported anything.", "FooterClaims", "MyReportsEmpty"));
		foreach (var report in reports) mine.Add(Ui.Text(MemberText.ReportLine(report), id: $"MyReport_{report.Id}"));
		body.Children.Add(Ui.Card([.. mine]));
	}

	private View AppealForm(Guid sanctionId)
	{
		var text = new Editor { AutomationId = "AppealText", MaxLength = 2000, AutoSize = EditorAutoSizeOption.TextChanges };
		return new VerticalStackLayout
		{
			Spacing = 8,
			Children =
			{
				Ui.Labelled("Why should we look again? (optional)", text),
				Ui.Button("Send appeal", "AppealSend", async () =>
				{
					var result = await Ui.Members!.AppealAsync(sanctionId, text.Text);					appealing = null;
					await LoadAsync();
					status.Text = result.Ok ? "Your appeal was sent. Someone who wasn't involved will review it." : "That appeal couldn't be sent.";
				}),
			},
		};
	}
}
