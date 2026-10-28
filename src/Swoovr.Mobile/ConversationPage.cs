using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// One conversation (D143), as on the web: text and photos (#41), sent through MemberClient (so every send passes the
/// API's checks) and received live from the API's hub with this phone's token. Delete for both within five minutes, hide
/// for oneself. Photos go through the quarantine: the other person sees one only once approved, the sender sees its
/// state. Starters from shared intention chips while the chat is empty; ending politely without blocking (Q85).
/// </summary>
public sealed class ConversationPage : ContentPage
{
	private readonly Guid id;
	private readonly VerticalStackLayout body = new() { Padding = new Thickness(24, 30), Spacing = 12 };
	private readonly VerticalStackLayout thread = new() { Spacing = 8, AutomationId = "Thread" };
	private readonly Entry draft = Ui.Entry("MessageInput", "Message", MessagingPolicyLimits.TextMax);
	private readonly Label status = Ui.Text(null, id: "ChatStatus");
	private readonly List<MessageView> messages = [];
	private MessagesHubClient? hub;
	private readonly SemaphoreSlim refreshGate = new(1, 1);

	public ConversationPage(Guid id)
	{
		this.id = id;
		Title = "Chat";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		await refreshGate.WaitAsync();
		try
		{
			body.Children.Clear();
			var conversation = (await Ui.Members!.ConversationAsync(id)).Value;
			if (conversation is null)
			{
				body.Children.Add(Ui.Text("Chat not available", "PageTitle", "ChatUnavailable"));
				body.Children.Add(Ui.Button("Report a serious safety incident", "SeriousAfterClose", () => Ui.PushAsync(new SafetyPage("conversation", id.ToString(), "blocked")), primary: false));
				return;
			}
			if (conversation.Closed)
				body.Children.Add(Ui.Card(Ui.Text(PhotoText.ClosedTitle, "PageTitle", "ChatClosed"), Ui.Text(PhotoText.ClosedBody), Ui.Text(PhotoText.ClosedSafety, "FooterClaims")));
			body.Children.Add(Ui.Text(conversation.Person.DisplayName ?? "Someone", "PageTitle", "ChatName"));
			body.Children.Add(Ui.Detach(thread));
			var page = await Ui.Members.MessagesAsync(id);
			messages.Clear();
			messages.AddRange(page.Value?.Items.Reverse() ?? []);
			if (conversation.CanSend && messages.Count == 0 && (await Ui.Members.StartersAsync(id)).Value is { Count: > 0 } starters)
				body.Children.Add(Ui.Card([Ui.Text("Something to start with:", "FooterClaims"), .. starters.Select((s, i) => (View)Ui.Button(s.Text, $"Starter_{i}", () =>
				{
					draft.Text = s.Text;
					return Task.CompletedTask;
				}, primary: false))]));
			if (conversation.CanSend)
				body.Children.Add(Ui.Card(Ui.Labelled("Message", draft), Ui.Button("Send", "SendMessage", SendAsync), Ui.Button("Send a photo", "SendPhoto", SendPhotoAsync, primary: false),
					Ui.Text(PhotoText.ChatRules, "FooterClaims")));
			else if (!conversation.Closed) body.Children.Add(Ui.Text("You can't send messages here right now.", id: "ChatCannotSend"));
			body.Children.Add(Ui.Detach(status));
			body.Children.Add(Ui.Button("Hide this chat", "HideChat", async () =>
			{
				if (await Ui.Members!.HideConversationAsync(id) == 204) await Navigation.PopAsync();
			}, primary: false));
			// Policy §7: Report and Block straight from the conversation; the Serious Safety Incident pathway (D190).
			body.Children.Add(Ui.Button("Report this chat", "ReportChat", () => Ui.PushAsync(new SafetyPage("conversation", id.ToString())), primary: false));
			if (conversation.Person.Id != Guid.Empty)
				body.Children.Add(Ui.Button($"Block {conversation.Person.DisplayName}", "BlockChat", async () =>
				{
					if (await Ui.Members!.BlockAsync(conversation.Person.Id) == 204) Closed();
					else status.Text = "That didn't work. Try again.";
				}, primary: false));
			if (conversation.CanSend) body.Children.Add(Ui.Button("End conversation", "EndChat", () => EndAsync(), primary: false));
			Draw();
			if (hub is null && MainPage.Auth.MessagesHub() is { } live)
			{
				hub = live;
				// A photo's state changes arrive as the same message again (#41): replace it in place.
				hub.Message += m => MainThread.BeginInvokeOnMainThread(() => { if (m.ConversationId == id) { Upsert(m); Draw(); } });
				hub.Deleted += d => MainThread.BeginInvokeOnMainThread(() => { if (d.ConversationId == id && MarkDeleted(d.MessageId)) Draw(); });
				// #82: a block, disconnect or deletion closes the chat at once.
				hub.Closed += c => MainThread.BeginInvokeOnMainThread(() => { if (c.ConversationId == id) Closed(); });
				try { await hub.StartAsync(); status.Text = null; }
				catch (Exception error) when (error is HttpRequestException or InvalidOperationException) { status.Text = "Live updates are off. Reopen the chat to see new messages."; }
			}
		}
		catch (Exception error) when (error is HttpRequestException or OperationCanceledException or InvalidOperationException)
		{
			body.Children.Add(Ui.Text("The conversation couldn't be refreshed. Reopen it to try again.", id: "ChatRefreshFailed"));
		}
		finally { refreshGate.Release(); }
	}

	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		if (hub is not null) { await hub.DisposeAsync(); hub = null; }
	}

	private void Draw()
	{
		thread.Children.Clear();
		foreach (var m in messages)
		{
			if (m.Kind == "system")
			{
				thread.Children.Add(Ui.Card(Ui.Text("Swoovr", "FieldLabel"), Ui.Text(m.Text, "FooterClaims", $"SystemEvent_{m.Id}")));
				continue;
			}
			// Q59, D167: a message deleted for both stays in the thread as deleted.
			View content = new Label { Text = m.Deleted ? "This message was deleted" : m.Kind == "farewell" ? "Predefined farewell\n" + m.Text : m.Text, Style = Ui.Style(m.Mine && !m.Deleted ? "BubbleMineText" : "BubbleText"),
				FontAttributes = m.Deleted ? FontAttributes.Italic : FontAttributes.None };
			if (!m.Deleted && m.Kind == "photo")
			{
				var photo = new VerticalStackLayout { Spacing = 4 };
				if (m.Photo is { } p) photo.Children.Add(Ui.Photo(p.Link, $"ChatPhoto_{p.Id}", 200));
				if (m.Mine && m.PhotoState is { } state && state != "approved")
					photo.Children.Add(new Label { Text = PhotoText.ChatState(state), Style = Ui.Style("BubbleMineText"), AutomationId = $"PhotoState_{m.Id}" });
				content = photo;
			}
			var bubble = new Border { Style = Ui.Style(m.Mine && !m.Deleted ? "BubbleMine" : "BubbleTheirs"), AutomationId = $"Message_{m.Id}",
				HorizontalOptions = m.Mine ? LayoutOptions.End : LayoutOptions.Start, Content = content };
			var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = bubble.HorizontalOptions };
			if (m.CanDelete)
				actions.Children.Add(Ui.Button("Delete for both", $"Delete_{m.Id}", async () =>
				{
					var code = await Ui.Members!.DeleteMessageAsync(m.Id);
					if (code == 204 && MarkDeleted(m.Id)) Draw();
					status.Text = code == 204 ? "Deleted for both of you." : "Messages can be deleted for both of you within five minutes.";
				}, primary: false));
			actions.Children.Add(Ui.Button("Hide", $"Hide_{m.Id}", async () =>
			{
				if (await Ui.Members!.HideMessageAsync(m.Id) == 204) { messages.RemoveAll(x => x.Id == m.Id); Draw(); }
			}, primary: false));
			thread.Children.Add(new VerticalStackLayout { Spacing = 2, Children = { bubble, actions } });
		}
	}

	private void Closed() => _ = RefreshAsync();

	private void Upsert(MessageView m)
	{
		var index = messages.FindIndex(x => x.Id == m.Id);
		if (index >= 0) messages[index] = m; else messages.Add(m);
	}

	private async Task SendPhotoAsync()
	{
		var picked = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))?.FirstOrDefault();
		if (picked is null) return;
		await using var stream = await picked.OpenReadAsync();
		using var buffer = new MemoryStream();
		await stream.CopyToAsync(buffer);
		var result = await Ui.Members!.SendChatPhotoAsync(id, buffer.ToArray(), picked.FileName, picked.ContentType);
		// A live screening/deletion event may precede the older upload response. Preserve that event.
        if (result.Value is { } sent) { if (messages.All(m => m.Id != sent.Id)) messages.Add(sent); Draw(); status.Text = null; }
		else status.Text = PhotoText.Refusal(result.Code);
	}

	/// <summary>Q85: an optional fixed closing message, then every mode disconnected and the chat closed for both.</summary>
	private async Task EndAsync()
	{
		var choices = PhotoText.Closings.Select(c => c.Label).Append("No farewell message").ToArray();
		var choice = await DisplayActionSheetAsync(PhotoText.EndHelp, "Keep chatting", null, choices);
		if (choice is null || choice == "Keep chatting") return;
		var closing = PhotoText.Closings.FirstOrDefault(c => c.Label == choice).Id;
		var result = await Ui.Members!.EndConversationAsync(id, closing);
		if (!result.Ok) { status.Text = "That didn't work. Try again."; return; }
		Closed();
		status.Text = "You ended this conversation.";
	}

	private bool MarkDeleted(Guid message)
	{
		var index = messages.FindIndex(x => x.Id == message);
		if (index < 0) return false;
		messages[index] = messages[index] with { Deleted = true, Text = "", CanDelete = false };
		return true;
	}

	private async Task SendAsync()
	{
		var result = await Ui.Members!.SendMessageAsync(id, draft.Text ?? "");
		if (result.Value is { } sent)
		{
			if (messages.All(x => x.Id != sent.Id)) { messages.Add(sent); Draw(); }
			(draft.Text, status.Text) = (null, null);
			return;
		}
		status.Text = result.Code switch
		{
			ProblemCodes.ArrivalRequired => "You can reply, or write first once you've confirmed you've arrived.",
			ProblemCodes.BotCannotInitiate => "This account can't start conversations.",
			ProblemCodes.InvalidMessage => "Write 1 to 2,000 characters.",
			ProblemCodes.TooManyRequests => "That's a lot of messages. Slow down a little.",
			_ => "That message couldn't be sent.",
		};
	}
}

/// <summary>The message length limit the API enforces (MessagingPolicy.TextMax in Domain, which the app doesn't reference).</summary>
internal static class MessagingPolicyLimits
{
	public const int TextMax = 2000;
}
