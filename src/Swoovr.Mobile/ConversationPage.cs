using Swoovr.Client;
using Swoovr.Contracts;

namespace Swoovr.Mobile;

/// <summary>
/// One conversation (D143), as on the web: text only, sent through MemberClient (so every send passes the API's checks)
/// and received live from the API's hub with this phone's token. Delete for both within five minutes, hide for oneself.
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

	public ConversationPage(Guid id)
	{
		this.id = id;
		Title = "Chat";
		Content = new ScrollView { Content = body };
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		body.Children.Clear();
		var conversation = (await Ui.Members!.ConversationAsync(id)).Value;
		if (conversation is null) { body.Children.Add(Ui.Text("Chat not available", "PageTitle", "ChatUnavailable")); return; }
		body.Children.Add(Ui.Text(conversation.Person.DisplayName ?? "Someone", "PageTitle", "ChatName"));
		body.Children.Add(Ui.Detach(thread));
		if (conversation.CanSend)
			body.Children.Add(Ui.Card(Ui.Labelled("Message", draft), Ui.Button("Send", "SendMessage", SendAsync)));
		else body.Children.Add(Ui.Text("You can't send messages here right now.", id: "ChatCannotSend"));
		body.Children.Add(Ui.Detach(status));
		body.Children.Add(Ui.Button("Hide this chat", "HideChat", async () =>
		{
			if (await Ui.Members!.HideConversationAsync(id) == 204) await Navigation.PopAsync();
		}, primary: false));
		messages.Clear();
		messages.AddRange((await Ui.Members.MessagesAsync(id)).Value?.Items.Reverse() ?? []);
		Draw();
		if (hub is null && MainPage.Auth.MessagesHub() is { } live)
		{
			hub = live;
			hub.Message += m => MainThread.BeginInvokeOnMainThread(() => { if (m.ConversationId == id && messages.All(x => x.Id != m.Id)) { messages.Add(m); Draw(); } });
			hub.Deleted += d => MainThread.BeginInvokeOnMainThread(() => { if (d.ConversationId == id && messages.RemoveAll(x => x.Id == d.MessageId) > 0) Draw(); });
			try { await hub.StartAsync(); status.Text = null; }
			catch (Exception error) when (error is HttpRequestException or InvalidOperationException) { status.Text = "Live updates are off. Reopen the chat to see new messages."; }
		}
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
			var bubble = new Border { Style = Ui.Style(m.Mine ? "BubbleMine" : "BubbleTheirs"), AutomationId = $"Message_{m.Id}",
				HorizontalOptions = m.Mine ? LayoutOptions.End : LayoutOptions.Start,
				Content = new Label { Text = m.Text, Style = Ui.Style(m.Mine ? "BubbleMineText" : "BubbleText") } };
			var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = bubble.HorizontalOptions };
			if (m.Mine && m.SentAt > DateTimeOffset.UtcNow.AddMinutes(-5))
				actions.Children.Add(Ui.Button("Delete for both", $"Delete_{m.Id}", async () =>
				{
					var code = await Ui.Members!.DeleteMessageAsync(m.Id);
					if (code == 204) { messages.RemoveAll(x => x.Id == m.Id); Draw(); }
					status.Text = code == 204 ? "Deleted for both of you." : "Messages can be deleted for both of you within five minutes.";
				}, primary: false));
			actions.Children.Add(Ui.Button("Hide", $"Hide_{m.Id}", async () =>
			{
				if (await Ui.Members!.HideMessageAsync(m.Id) == 204) { messages.RemoveAll(x => x.Id == m.Id); Draw(); }
			}, primary: false));
			thread.Children.Add(new VerticalStackLayout { Spacing = 2, Children = { bubble, actions } });
		}
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
