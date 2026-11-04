using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// Me → Support, as on the web: the member's own support requests and a new request. Open while restricted or suspended,
/// like notices and standing. The API decides who the member is; nothing here names a helpdesk customer. A draft keeps one
/// operation key until the API confirms it, so a double tap or a lost response never sends twice.
/// </summary>
public sealed class SupportPage : ContentPage
{
    private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
    private readonly string? reference;
    private readonly bool compose;
    private IReadOnlyList<TicketOption> states = [];
    private int page = 1;

    public SupportPage(bool compose = false, string? reference = null)
    {
        (this.compose, this.reference) = (compose, SupportTicketText.ValidReference(reference) ? reference : null);
        Title = compose ? SupportTicketText.NewTitle : SupportTicketText.Title;
        Content = new ScrollView { Content = body };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (compose && body.Children.Count > 0) return;
        states = (await Ui.Members!.TicketOptionsAsync()).Value?.States ?? [];
        if (compose) Compose(); else await LoadAsync();
    }

    private async Task LoadAsync()
    {
        body.Children.Clear();
        body.Children.Add(Ui.Text(SupportTicketText.Title, "PageTitle"));
        body.Children.Add(Ui.Text(SupportTicketText.Intro, "FooterClaims"));
        body.Children.Add(Ui.Button("New request", "SupportNew", () => Ui.PushAsync(new SupportPage(compose: true))));
        body.Children.Add(Ui.Button("Email Support", "SupportEmail", () => Launcher.Default.OpenAsync(new Uri("mailto:" + StandingText.SupportEmail)), primary: false));
        var result = await Ui.Members!.TicketsAsync(page);
        if (!result.Ok) { body.Children.Add(Ui.Text(SupportTicketText.Problem(result.Status, result.Code), id: "SupportProblem")); return; }
        var rows = new List<View> { Ui.Text("Your requests", "SectionHeading") };
        if (result.Value!.Items.Count == 0) rows.Add(Ui.Text(SupportTicketText.NoTickets, "FooterClaims", "SupportEmpty"));
        foreach (var t in result.Value.Items)
        {
            rows.Add(Ui.Button(t.Title, "SupportTicket_" + t.Reference, () => Ui.PushAsync(new SupportTicketPage(t.Id, states)), primary: false));
            rows.Add(Ui.Text($"{SupportTicketText.StateName(states, t.StateId)} · {t.Reference} · Updated {t.UpdatedAt.ToLocalTime():d MMM yyyy HH:mm}", "FooterClaims"));
        }
        if (page > 1) rows.Add(Ui.Button("Newer", "SupportNewer", async () => { page--; await LoadAsync(); }, primary: false));
        if (result.Value.HasMore) rows.Add(Ui.Button("Older", "SupportOlder", async () => { page++; await LoadAsync(); }, primary: false));
        body.Children.Add(Ui.Card([.. rows]));
        body.Children.Add(Ui.Text(StandingText.SupportHelp, "FooterClaims"));
    }

    private void Compose()
    {
        var operation = Guid.NewGuid();
        var subject = Ui.Entry("SupportTitle", SupportTicketText.TitleLabel, 200);
        subject.Text = SupportTicketText.Subject(reference);
        var message = new Editor { AutomationId = "SupportBody", Placeholder = SupportTicketText.BodyLabel, MaxLength = 16000, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 160 };
        var status = Ui.Text(null, id: "SupportStatus");
        Button? send = null;
        send = Ui.Button("Send request", "SupportSend", async () =>
        {
            if (string.IsNullOrWhiteSpace(subject.Text) || string.IsNullOrWhiteSpace(message.Text)) { status.Text = SupportTicketText.Problem(400, "invalid-request"); return; }
            status.Text = "Sending…";
            var result = await Ui.Members!.CreateTicketAsync(new TicketCreate(operation, subject.Text.Trim(), message.Text.Trim()));
            if (result.Ok) { Navigation.InsertPageBefore(new SupportTicketPage(result.Value!.Id, states), this); await Navigation.PopAsync(); return; }
            // Uncertain: keep the same key and lock the text, so "Send again" is reconciled by the API instead of duplicated.
            if (result.Code == "ticket-reconciliation-required" || result.Status is 0 or >= 500) { subject.IsReadOnly = message.IsReadOnly = true; send!.Text = "Send again"; }
            status.Text = SupportTicketText.Problem(result.Status, result.Code);
        });
        body.Children.Add(Ui.Text(SupportTicketText.NewTitle, "PageTitle"));
        body.Children.Add(Ui.Card(Ui.Labelled(SupportTicketText.TitleLabel, subject), Ui.Labelled(SupportTicketText.BodyLabel, message),
            Ui.Text(SupportTicketText.SensitiveHelp, "FooterClaims"), send, status));
    }
}

/// <summary>One support request: messages from the member and the support team, and a reply. Never private notes.</summary>
public sealed class SupportTicketPage(long id, IReadOnlyList<TicketOption> states) : ContentPage
{
    private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 16 };
    private Guid operation = Guid.NewGuid();
    private readonly List<TicketUpload> files = [];
    private string? draft;
    private bool locked;

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Title = SupportTicketText.Title;
        Content = new ScrollView { Content = body };
        await LoadAsync();
    }

    private async Task LoadAsync(TicketDetail? fresh = null, string? notice = null)
    {
        var result = fresh is null ? await Ui.Members!.TicketAsync(id) : new ApiResult<TicketDetail>(200, fresh, null);
        body.Children.Clear();
        // A refusal removes what was shown: access may have been withdrawn since the last load.
        if (!result.Ok) { body.Children.Add(Ui.Text(SupportTicketText.Problem(result.Status, result.Code), id: "SupportTicketProblem")); return; }
        var d = result.Value!;
        body.Children.Add(Ui.Text(d.Ticket.Title, "PageTitle", "SupportTicketTitle"));
        body.Children.Add(Ui.Text($"{d.Ticket.Reference} · {SupportTicketText.StateName(states, d.Ticket.StateId)}", "FooterClaims", "SupportTicketState"));
        body.Children.Add(Ui.Text(SupportTicketText.OutcomeHelp, "FooterClaims"));
        if (notice is not null) body.Children.Add(Ui.Text(notice, id: "SupportTicketNotice"));
        foreach (var a in d.Articles)
        {
            var rows = new List<View> { Ui.Text($"{SupportTicketText.Sender(a.Sender, true)} · {a.CreatedAt.ToLocalTime():d MMM yyyy HH:mm}", "FooterClaims"), Ui.Text(a.Body, id: "SupportArticle_" + a.Id) };
            foreach (var x in a.Attachments)
            {
                var shown = Ui.Text(null, id: "SupportAttachmentText_" + x.Id);
                rows.Add(Ui.Button($"View {x.Filename}", "SupportAttachment_" + x.Id, async () =>
                {
                    var file = await Ui.Members!.TicketAttachmentAsync(id, a.Id, x.Id);
                    shown.Text = file.Ok ? new System.Text.UTF8Encoding(false, false).GetString(file.Value!.Data) : SupportTicketText.Problem(file.Status, file.Code);
                }, primary: false));
                rows.Add(shown);
            }
            body.Children.Add(Ui.Card([.. rows]));
        }
        if (!d.CanReply) { body.Children.Add(Ui.Text("This request is closed. Start a new request if you still need help.", id: "SupportTicketClosed")); return; }
        var reply = new Editor { AutomationId = "SupportReply", Placeholder = SupportTicketText.ReplyLabel, MaxLength = 16000, AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 120, Text = draft, IsReadOnly = locked };
        reply.TextChanged += (_, e) => draft = e.NewTextValue;
        var status = Ui.Text(null, id: "SupportReplyStatus");
        var attached = Ui.Text(files.Count == 0 ? null : string.Join(", ", files.Select(f => f.Filename)), "FooterClaims", "SupportReplyFiles");
        var card = new List<View> { Ui.Text("Reply", "SectionHeading"), reply, attached };
        if (!locked) card.Add(Ui.Button("Attach text files", "SupportAttach", async () =>
        {
            var picked = await FilePicker.Default.PickMultipleAsync(new PickOptions { PickerTitle = SupportTicketText.AttachmentHelp });
            foreach (var p in picked ?? [])
            {
                if (p is null || files.Count >= 3 || !p.FileName.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) { status.Text = SupportTicketText.AttachmentHelp; continue; }
                await using var stream = await p.OpenReadAsync(); using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                if (memory.Length > 65536) { status.Text = SupportTicketText.AttachmentHelp; continue; }
                files.Add(new TicketUpload(p.FileName, "text/plain", memory.ToArray()));
            }
            attached.Text = string.Join(", ", files.Select(f => f.Filename));
        }, primary: false));
        card.Add(Ui.Text(SupportTicketText.SensitiveHelp, "FooterClaims"));
        card.Add(Ui.Button(locked ? "Send again" : "Send reply", "SupportReplySend", async () =>
        {
            if (string.IsNullOrWhiteSpace(draft)) { status.Text = SupportTicketText.Problem(400, "invalid-request"); return; }
            status.Text = "Sending…";
            var sent = await Ui.Members!.ReplyTicketAsync(id, new TicketReply(operation, draft.Trim(), false, files.Count == 0 ? null : files.ToArray()));
            if (sent.Ok) { (draft, locked, operation) = (null, false, Guid.NewGuid()); files.Clear(); await LoadAsync(sent.Value, "Reply sent."); return; }
            locked = sent.Code == "ticket-reconciliation-required" || sent.Status is 0 or >= 500;
            if (sent.Status is 403 or 404 || sent.Code is "ticket-follow-up-unavailable" or "ticket-changed") { await LoadAsync(notice: SupportTicketText.Problem(sent.Status, sent.Code)); return; }
            reply.IsReadOnly = locked;
            status.Text = SupportTicketText.Problem(sent.Status, sent.Code);
        }));
        card.Add(status);
        body.Children.Add(Ui.Card([.. card]));
    }
}
